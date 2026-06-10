import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { WebPubSubClient } from '@azure/web-pubsub-client';
import { environment } from '../../environments/environment';

/** Shape returned by GET /notifications/games/{gameId}/negotiate */
interface NegotiateResponse {
  url: string;
}

/**
 * Options for WebPubSubStream.
 *
 * - `gameId` — the game to join; used to build the negotiate URL.
 * - `http` — Angular HttpClient that already has `authTokenInterceptor` attached,
 *   so the negotiate request carries the user's Bearer token automatically.
 * - `onMessage` — called for every group-message arriving from the server;
 *   receives the raw `{ type, data }` envelope already parsed.
 * - `onConnected` / `onReconnected` — called when the WebSocket is first open /
 *   re-opens after a drop. Pages call `pollStatus()` from here to catch events
 *   missed while the socket was down.
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
 * Thin wrapper around `@azure/web-pubsub-client` (`WebPubSubClient`) for the
 * in-game real-time channel.
 *
 * The SDK handles reconnection automatically: when the connection drops it
 * re-invokes the `getClientAccessUrl` callback (which re-negotiates a fresh URL
 * with the server, obtaining a new token) and reconnects. We surface that via
 * `onReconnected` so pages can do an immediate status poll to fill the gap.
 *
 * Lifecycle:
 *   `start()` → opens the WebSocket (awaitable).
 *   `stop()` → closes the connection and nulls the client.
 */
export class WebPubSubStream {
  private client: WebPubSubClient | null = null;
  private isFirstConnect = true;
  private stopped = false;

  constructor(private readonly options: WebPubSubStreamOptions) {}

  async start(): Promise<void> {
    if (this.client) return;
    this.stopped = false;
    this.isFirstConnect = true;

    this.client = new WebPubSubClient({
      getClientAccessUrl: () => this.negotiate(),
    });

    this.client.on('connected', () => {
      if (this.stopped) return;
      this.log('connected');
      if (this.isFirstConnect) {
        this.isFirstConnect = false;
        this.options.onConnected?.();
      } else {
        // SDK reconnected after a drop — pages refetch missed state here.
        this.options.onReconnected?.();
      }
    });

    this.client.on('group-message', (e) => {
      if (this.stopped) return;
      try {
        const raw = e.message.data;
        // The SDK may deliver data as a string (if the server sent text-frame JSON)
        // or as an already-parsed object (if the server sent a JSON data-type frame).
        const envelope = typeof raw === 'string' ? JSON.parse(raw) : raw;
        if (envelope && typeof envelope.type === 'string') {
          this.options.onMessage(envelope as { type: string; data: unknown });
        } else {
          this.logError('group-message without a type field — ignored', envelope);
        }
      } catch (err) {
        this.logError('failed to parse group-message', err);
      }
    });

    this.client.on('stopped', () => {
      this.log('client stopped');
    });

    try {
      await this.client.start();
    } catch (err) {
      this.logError('WebPubSubClient.start() failed', err);
    }
  }

  stop(): void {
    this.stopped = true;
    if (this.client) {
      try {
        void this.client.stop();
      } catch {
        // ignore errors on stop
      }
      this.client = null;
    }
  }

  private async negotiate(): Promise<string> {
    const url = `${environment.apiUrl}/notifications/games/${this.options.gameId}/negotiate`;
    this.log(`negotiating via ${url}`);
    try {
      const response = await firstValueFrom(
        this.options.http.get<NegotiateResponse>(url)
      );
      return response.url;
    } catch (err) {
      this.logError('negotiate request failed', err);
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
