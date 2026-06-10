import { App } from '@capacitor/app';
import type { PluginListenerHandle } from '@capacitor/core';

/**
 * Resilient wrapper around EventSource for the game's SSE streams.
 *
 * Native EventSource alone is not reliable enough here:
 * - It gives up permanently when the server responds non-200 (e.g. 401 once the JWT in the
 *   query string expires, or during a server restart) — `readyState` goes CLOSED and no
 *   retry ever happens.
 * - Its automatic retries reuse the original URL, i.e. the original (possibly expired) token.
 * - Mobile networks and proxies silently sever idle connections, leaving a half-open stream
 *   that receives nothing and raises no error.
 * - Backgrounding the app on Android/iOS freezes the WebView; on resume the stream is dead.
 *
 * This class therefore owns the reconnect loop itself: a fresh token per attempt, exponential
 * backoff that resets on success, a heartbeat watchdog (the server emits a `heartbeat` event
 * every 15s — silence beyond ~3 beats means the connection is dead), and a forced reconnect
 * when the app returns to the foreground. After every reconnect the `onReconnected` callback
 * fires so callers can refetch state they may have missed while disconnected.
 */
export interface SseStreamOptions {
  /** Builds the stream URL for a given access token. */
  buildUrl: (token: string) => string;
  /** Named SSE events to dispatch to `onEvent` (`heartbeat` is handled internally). */
  events: readonly string[];
  /** Fetches a fresh access token; called for every (re)connect attempt. */
  getToken: () => Promise<string>;
  /** Called for every named event with the raw `data` payload. */
  onEvent: (type: string, data: string) => void;
  /** Called after a successful re-open following a drop — refetch missed state here. */
  onReconnected?: () => void;
  /**
   * Called after each failed connection attempt, with the number of consecutive failures.
   * Use it to refresh state out-of-band while the stream is down — the server may be
   * rejecting the stream for a reason a plain GET can reveal (game ended, removed, …).
   */
  onDisconnected?: (consecutiveFailures: number) => void;
  /**
   * When true, a failed attempt triggers a one-off `fetch` of the stream URL purely to log
   * the HTTP status. EventSource never exposes why a connection failed (readyState=2 only
   * says "the server responded with something unusable"), which makes 401-vs-403-vs-404
   * undebuggable on a device without this.
   */
  probeStatusOnError?: boolean;
  log?: (message: string) => void;
  logError?: (message: string, ...args: unknown[]) => void;
}

const INITIAL_BACKOFF_MS = 1_000;
const MAX_BACKOFF_MS = 30_000;
/** Server heartbeat is 15s; three missed beats ⇒ treat the connection as dead. */
const STALE_AFTER_MS = 45_000;
const WATCHDOG_INTERVAL_MS = 5_000;

export class SseStream {
  private source: EventSource | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private watchdogTimer: ReturnType<typeof setInterval> | null = null;
  private resumeListener: PluginListenerHandle | null = null;
  private backoffMs = INITIAL_BACKOFF_MS;
  private lastActivityAt = 0;
  private hadDrop = false;
  private running = false;
  private consecutiveFailures = 0;
  private probing = false;
  /** Invalidates callbacks from superseded connections (stop() or a newer open). */
  private generation = 0;

  constructor(private readonly options: SseStreamOptions) {}

  async start(): Promise<void> {
    if (this.running) return;
    this.running = true;
    this.backoffMs = INITIAL_BACKOFF_MS;
    this.hadDrop = false;
    this.watchdogTimer = setInterval(() => this.checkStaleness(), WATCHDOG_INTERVAL_MS);
    // While backgrounded the WebView (and this watchdog) is frozen and the stream usually
    // dies unnoticed. Reconnect unconditionally on resume: worst case we cycle a healthy
    // connection; best case we revive a dead one and onReconnected refetches missed state.
    // Registered fire-and-forget so a slow/unavailable plugin never delays the connection;
    // the watchdog covers staleness either way.
    App.addListener('appStateChange', ({ isActive }) => {
      if (isActive && this.running) {
        this.log('app resumed — reconnecting stream');
        this.forceReconnect();
      }
    }).then((handle) => {
      if (this.running) this.resumeListener = handle;
      else void handle.remove();
    }).catch(() => { /* plugin unavailable (plain browser) */ });
    await this.openConnection();
  }

  stop(): void {
    this.running = false;
    this.generation++;
    this.clearReconnect();
    if (this.watchdogTimer !== null) {
      clearInterval(this.watchdogTimer);
      this.watchdogTimer = null;
    }
    void this.resumeListener?.remove();
    this.resumeListener = null;
    if (this.source) {
      this.log(`closing stream (readyState=${this.source.readyState})`);
      this.source.close();
      this.source = null;
    }
  }

  private async openConnection(): Promise<void> {
    if (!this.running) return;
    const gen = ++this.generation;
    this.source?.close();
    this.source = null;
    // Restart the staleness clock so the watchdog measures from this attempt, not from the
    // last event of the previous (now closed) connection.
    this.lastActivityAt = Date.now();

    let token: string;
    try {
      token = await this.options.getToken();
    } catch (err) {
      this.logError('token acquisition failed — retrying with backoff', err);
      if (this.running && gen === this.generation) {
        this.consecutiveFailures++;
        this.scheduleReconnect();
        this.options.onDisconnected?.(this.consecutiveFailures);
      }
      return;
    }
    if (!this.running || gen !== this.generation) return;

    const es = new EventSource(this.options.buildUrl(token));
    this.source = es;
    this.lastActivityAt = Date.now();

    es.onopen = () => {
      if (gen !== this.generation) return;
      this.lastActivityAt = Date.now();
      this.backoffMs = INITIAL_BACKOFF_MS;
      this.consecutiveFailures = 0;
      this.log('connection open');
      if (this.hadDrop) {
        this.hadDrop = false;
        this.options.onReconnected?.();
      }
    };

    es.addEventListener('heartbeat', () => {
      if (gen === this.generation) this.lastActivityAt = Date.now();
    });

    for (const type of this.options.events) {
      es.addEventListener(type, (e: MessageEvent) => {
        if (gen !== this.generation) return;
        this.lastActivityAt = Date.now();
        this.options.onEvent(type, e.data);
      });
    }

    es.onerror = () => {
      if (gen !== this.generation) return;
      // Never rely on EventSource's own retry: it reuses the stale token and gives up for
      // good on non-200 responses. Close and run our own reconnect with a fresh token.
      // readyState hints at the failure mode: 2 (CLOSED) immediately after connecting means
      // the server RESPONDED but with a non-200 status or wrong content type; 0 (CONNECTING)
      // means a network-level failure (unreachable, DNS, TLS).
      this.logError(`stream error (readyState=${es.readyState}) — scheduling reconnect`);
      this.hadDrop = true;
      this.consecutiveFailures++;
      es.close();
      if (this.source === es) this.source = null;
      if (this.options.probeStatusOnError) void this.probeStatus();
      this.scheduleReconnect();
      this.options.onDisconnected?.(this.consecutiveFailures);
    };
  }

  /**
   * Fetches the stream URL once and logs the HTTP status, then aborts. EventSource hides the
   * response entirely, so without this a rejected stream (401 expired token, 403 no longer a
   * participant, 404 game gone) is indistinguishable from a flaky network on a device.
   */
  private async probeStatus(): Promise<void> {
    if (this.probing) return;
    this.probing = true;
    try {
      const token = await this.options.getToken();
      const controller = new AbortController();
      try {
        const response = await fetch(this.options.buildUrl(token), {
          headers: { Accept: 'text/event-stream' },
          signal: controller.signal,
        });
        this.logError(
          `probe: server responded ${response.status} ${response.statusText} ` +
          `(content-type=${response.headers.get('content-type') ?? '<none>'})`);
      } finally {
        controller.abort();
      }
    } catch (err) {
      this.logError('probe: request failed at network level (server unreachable, TLS or CORS)', err);
    } finally {
      this.probing = false;
    }
  }

  private checkStaleness(): void {
    if (!this.running || this.reconnectTimer !== null) return;
    if (Date.now() - this.lastActivityAt > STALE_AFTER_MS) {
      this.logError(`no event or heartbeat for ${STALE_AFTER_MS / 1000}s — connection presumed dead, reconnecting`);
      this.forceReconnect();
    }
  }

  private forceReconnect(): void {
    this.hadDrop = true;
    this.clearReconnect();
    void this.openConnection();
  }

  private scheduleReconnect(): void {
    if (!this.running) return;
    this.clearReconnect();
    const delay = this.backoffMs;
    this.backoffMs = Math.min(this.backoffMs * 2, MAX_BACKOFF_MS);
    this.log(`reconnecting in ${delay}ms`);
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      void this.openConnection();
    }, delay);
  }

  private clearReconnect(): void {
    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  private log(message: string): void {
    this.options.log?.(message);
  }

  private logError(message: string, ...args: unknown[]): void {
    this.options.logError?.(message, ...args);
  }
}
