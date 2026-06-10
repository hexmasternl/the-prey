import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

/** Shape returned by GET /games/{gameId}/notifications/token */
interface ConnectionTokenResponse {
  url: string;
}

/**
 * Options for WebPubSubStream.
 *
 * - `gameId` — the game to join; used to build the token URL and the group name.
 * - `http` — Angular HttpClient that already has `authTokenInterceptor` attached,
 *   so the token request carries the user's Bearer token automatically.
 * - `onMessage` — called for every group message arriving from the server;
 *   receives the raw `{ type, data }` envelope already parsed.
 * - `onConnected` / `onReconnected` — called once the socket is open AND we have
 *   joined the game's group (first connect / after a drop). Pages call
 *   `pollStatus()` from here to catch events missed while the socket was down.
 * - `log` / `logError` — optional debug sinks (console.info / console.error by default).
 */
export interface WebPubSubStreamOptions {
  gameId: string;
  http: HttpClient;
  onMessage: (envelope: { type: string; data: unknown }) => void;
  onConnected?: () => void;
  onReconnected?: () => void;
  log?: (msg: string) => void;
  logError?: (msg: string, ...args: unknown[]) => void;
}

/**
 * Web PubSub "json" protocol subprotocol. Connecting with it lets the client send
 * control frames (e.g. `joinGroup`) over the same WebSocket and receive structured
 * messages (`{ type: 'message' | 'ack' | 'system', ... }`).
 */
const WEB_PUBSUB_JSON_SUBPROTOCOL = 'json.webpubsub.azure.v1';

/** Reconnect backoff bounds (exponential between them). */
const RECONNECT_MIN_DELAY_MS = 1_000;
const RECONNECT_MAX_DELAY_MS = 30_000;

/**
 * Thin wrapper around a native browser `WebSocket` for the in-game real-time channel.
 *
 * Flow:
 *   1. `start()` requests a short-lived, group-scoped access URL from the Games API
 *      (`GET /games/{gameId}/notifications/token`).
 *   2. Opens a native WebSocket to that URL using the Web PubSub json subprotocol.
 *   3. On open, sends a `joinGroup` for the game's group (the access token grants the
 *      join role scoped to exactly this group).
 *   4. On the join `ack`, fires `onConnected` (first time) / `onReconnected` (after a drop).
 *
 * The native WebSocket does not auto-reconnect, so this class re-requests a fresh token
 * and reconnects with exponential backoff whenever the socket closes unexpectedly.
 *
 * Lifecycle:
 *   `start()` → opens the WebSocket (awaitable; resolves once the first attempt is kicked off).
 *   `stop()`  → closes the connection, cancels any pending reconnect, and nulls the socket.
 */
export class WebPubSubStream {
  private socket: WebSocket | null = null;
  private isFirstConnect = true;
  private stopped = false;
  private ackIdSeq = 0;
  private reconnectAttempts = 0;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private readonly options: WebPubSubStreamOptions) {}

  async start(): Promise<void> {
    if (this.socket) return;
    this.stopped = false;
    this.isFirstConnect = true;
    this.reconnectAttempts = 0;
    await this.connect();
  }

  stop(): void {
    this.stopped = true;
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    if (this.socket) {
      try {
        this.socket.close();
      } catch {
        // ignore errors on close
      }
      this.socket = null;
    }
  }

  private async connect(): Promise<void> {
    let url: string;
    try {
      url = await this.fetchConnectionUrl();
    } catch (err) {
      this.logError('failed to obtain Web PubSub connection url', err);
      this.scheduleReconnect();
      return;
    }

    // The caller may have stopped us while the token request was in flight.
    if (this.stopped) return;

    let socket: WebSocket;
    try {
      socket = new WebSocket(url, WEB_PUBSUB_JSON_SUBPROTOCOL);
    } catch (err) {
      this.logError('failed to open WebSocket', err);
      this.scheduleReconnect();
      return;
    }
    this.socket = socket;

    socket.onopen = () => {
      if (this.stopped) return;
      this.log('socket open; subscribing to group');
      this.joinGroup();
    };

    socket.onmessage = (event) => {
      if (this.stopped) return;
      this.handleRawMessage(event.data);
    };

    socket.onerror = (event) => {
      this.logError('WebSocket error', event);
    };

    socket.onclose = (event) => {
      if (this.socket === socket) this.socket = null;
      if (this.stopped) {
        this.log('socket closed (stopped)');
        return;
      }
      this.log(`socket closed (code=${event.code}); scheduling reconnect`);
      this.scheduleReconnect();
    };
  }

  /** Subscribe to this game's group so the server's SendToGroup broadcasts reach us. */
  private joinGroup(): void {
    this.send({ type: 'joinGroup', group: this.options.gameId, ackId: ++this.ackIdSeq });
  }

  private handleRawMessage(raw: unknown): void {
    let message: { type?: string; from?: string; data?: unknown; success?: boolean; error?: { name?: string }; event?: string };
    try {
      message = typeof raw === 'string' ? JSON.parse(raw) : raw;
    } catch (err) {
      this.logError('failed to parse message', err);
      return;
    }
    if (!message || typeof message !== 'object') return;

    switch (message.type) {
      case 'message':
        // We only ever subscribe to one group, so any group data is for this game.
        if (message.from === 'group') {
          this.dispatchGroupData(message.data);
        }
        break;
      case 'ack':
        // ack for our joinGroup. "Duplicate" means we were already in the group — also success.
        if (message.success || message.error?.name === 'Duplicate') {
          this.onJoined();
        } else {
          this.logError('joinGroup ack failed', message);
        }
        break;
      case 'system':
        // 'connected' / 'disconnected' — informational; reconnection is driven by onclose.
        this.log(`system event: ${message.event}`);
        break;
      default:
        break;
    }
  }

  private dispatchGroupData(data: unknown): void {
    // The server sends ApplicationJson, so with the json subprotocol `data` is already the
    // parsed envelope `{ type, data }`. Be defensive in case it arrives as a JSON string.
    let envelope: unknown;
    try {
      envelope = typeof data === 'string' ? JSON.parse(data) : data;
    } catch (err) {
      this.logError('failed to parse group message data', err);
      return;
    }
    if (envelope && typeof envelope === 'object' && typeof (envelope as { type?: unknown }).type === 'string') {
      this.options.onMessage(envelope as { type: string; data: unknown });
    } else {
      this.logError('group message without a type field — ignored', envelope);
    }
  }

  private onJoined(): void {
    this.reconnectAttempts = 0;
    if (this.isFirstConnect) {
      this.isFirstConnect = false;
      this.log('connected & joined group');
      this.options.onConnected?.();
    } else {
      this.log('reconnected & re-joined group');
      this.options.onReconnected?.();
    }
  }

  private scheduleReconnect(): void {
    if (this.stopped || this.reconnectTimer) return;
    const attempt = ++this.reconnectAttempts;
    const delay = Math.min(
      RECONNECT_MAX_DELAY_MS,
      RECONNECT_MIN_DELAY_MS * 2 ** Math.min(attempt - 1, 5)
    );
    this.log(`reconnecting in ${delay}ms (attempt ${attempt})`);
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      void this.connect();
    }, delay);
  }

  private send(message: object): void {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(JSON.stringify(message));
    }
  }

  private async fetchConnectionUrl(): Promise<string> {
    const url = `${environment.apiUrl}/games/${this.options.gameId}/notifications/token`;
    this.log(`requesting Web PubSub connection url via ${url}`);
    try {
      const response = await firstValueFrom(
        this.options.http.get<ConnectionTokenResponse>(url)
      );
      return response.url;
    } catch (err) {
      this.logError('connection token request failed', err);
      throw err;
    }
  }

  private log(msg: string): void {
    (this.options.log ?? console.info.bind(console))(`[WebPubSubStream] ${msg}`);
  }

  private logError(msg: string, ...args: unknown[]): void {
    (this.options.logError ?? console.error.bind(console))(`[WebPubSubStream] ${msg}`, ...args);
  }
}
