import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RealtimeEnvelope, WebPubSubStream } from '../core/web-pubsub-stream';

export type { RealtimeEnvelope } from '../core/web-pubsub-stream';

/**
 * Thin Angular-injectable wrapper around exactly one {@link WebPubSubStream} for the
 * active game's real-time channel. It owns the transport lifecycle (token fetch,
 * `joinGroup`, exponential-backoff reconnect) and forwards every group message as the
 * full versioned envelope `{ v, type, gameId, seq, data }` — see `docs/api/realtime.md`.
 *
 * This service does not interpret message types or maintain any game state itself;
 * `GameStateService` is the sole consumer and the sole source of truth for the active
 * game. Only one caller should ever hold an active `connect()` for a given game, which
 * `GameStateService` guarantees by being the only page-facing entry point.
 */
@Injectable({ providedIn: 'root' })
export class GameStreamService {
  private readonly http = inject(HttpClient);

  private stream: WebPubSubStream | null = null;
  private messageHandler: ((envelope: RealtimeEnvelope) => void) | null = null;
  private connectedHandler: (() => void) | null = null;
  private reconnectedHandler: (() => void) | null = null;
  private unavailableHandler: (() => void) | null = null;

  /**
   * Opens the Web PubSub WebSocket for the given game. The Games API token endpoint
   * is called automatically (and re-called on every reconnect) so the access URL is
   * always fresh. Register handlers via `onMessage`/`onConnected`/`onReconnected`/
   * `onUnavailable` before (or after) calling this — they are read live on each event and,
   * unlike the transport itself, survive a later `connect()` call (e.g. `GameStateService`
   * forcing a fresh connection on app resume), so callers only need to register them once.
   */
  connect(gameId: string): void {
    this.stopTransport();
    this.stream = new WebPubSubStream({
      gameId,
      http: this.http,
      onMessage: (envelope) => {
        try {
          this.messageHandler?.(envelope);
        } catch (err) {
          console.error('[GameStream] message handler threw', err);
        }
      },
      onConnected: () => {
        console.info(`[GameStream] connected — gameId=${gameId}`);
        this.connectedHandler?.();
      },
      onReconnected: () => {
        console.info(`[GameStream] reconnected — gameId=${gameId}; triggering resync`);
        this.reconnectedHandler?.();
      },
      onUnavailable: () => {
        console.error(`[GameStream] unavailable (403) — gameId=${gameId}`);
        this.unavailableHandler?.();
      },
      log: (msg) => console.info(`[GameStream] ${msg}`),
      logError: (msg, ...args) => console.error(`[GameStream] ${msg}`, ...args),
    });
    void this.stream.start();
  }

  /** Register the handler invoked for every incoming envelope. */
  onMessage(handler: (envelope: RealtimeEnvelope) => void): void {
    this.messageHandler = handler;
  }

  /** Register a callback fired once the socket first opens and joins the group. */
  onConnected(handler: () => void): void {
    this.connectedHandler = handler;
  }

  /** Register a callback fired after the WebSocket re-opens following a drop. */
  onReconnected(handler: () => void): void {
    this.reconnectedHandler = handler;
  }

  /** Register a callback fired once the connection is terminally unavailable (403). */
  onUnavailable(handler: () => void): void {
    this.unavailableHandler = handler;
  }

  /** Stops the transport and clears every registered handler — call on final teardown. */
  disconnect(): void {
    this.stopTransport();
    this.messageHandler = null;
    this.connectedHandler = null;
    this.reconnectedHandler = null;
    this.unavailableHandler = null;
  }

  /** Stops just the transport, preserving registered handlers for a subsequent `connect()`. */
  private stopTransport(): void {
    this.stream?.stop();
    this.stream = null;
  }
}
