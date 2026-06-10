import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { WebPubSubStream } from '../core/web-pubsub-stream';

// ── Lobby events ────────────────────────────────────────────────────────────
// All lobby events carry a full GameDto as their `data`.
export type LobbyEventType =
  | 'lobby-updated'
  | 'settings-updated'
  | 'ready-updated'
  | 'hunter-designated'
  | 'hunter-changed'
  | 'game-started';

// ── In-game events ───────────────────────────────────────────────────────────
export type GameEventType =
  | 'state-changed'
  | 'player-location-updated'
  | 'player-status-changed'
  | 'participant-status-changed'
  | 'player-penalized'
  | 'game-ended';

export type AnyEventType = LobbyEventType | GameEventType;

export interface StateChangedPayload         { gameId: string; newState: string; }
export interface PlayerLocationUpdatedPayload { gameId: string; userId: string; latitude: number; longitude: number; participantState: string; }
export interface PlayerStatusChangedPayload  { gameId: string; userId: string; role: string; newState: string; }
export interface ParticipantStatusChangedPayload { gameId: string; participantId: string; participantRole: string; newState: string; }
export interface PlayerPenalizedPayload      { gameId: string; userId: string; penaltyEndsAt: string; reason: string; }
export interface GameEndedPayload            { gameId: string; outcome?: string; survivorCount?: number; }

type HandlerMap = Map<string, (payload: unknown) => void>;

/**
 * Typed real-time event dispatcher backed by Azure Web PubSub.
 *
 * Drop-in replacement for the old SSE-based `GameStreamService`. The public API
 * (`connect`, `on`, `onReconnected`, `disconnect`) is identical so pages need
 * minimal changes. The transport is now a WebSocket managed by the
 * `@azure/web-pubsub-client` SDK, which auto-reconnects and re-negotiates a
 * fresh access URL on every reconnect attempt.
 *
 * Event envelope from the server:
 *   `{ "type": "<event-name>", "data": { ...payload... } }`
 *
 * The service dispatches `data` to the registered handler for `type`.
 */
@Injectable({ providedIn: 'root' })
export class GameStreamService {
  private readonly http = inject(HttpClient);

  private stream: WebPubSubStream | null = null;
  private handlers: HandlerMap = new Map();
  private reconnectedHandler: (() => void) | null = null;

  /**
   * Opens the Web PubSub WebSocket for the given game. The negotiate endpoint is
   * called automatically (and re-called on every SDK reconnect) so the access URL
   * is always fresh.
   */
  connect(gameId: string): void {
    this.disconnect();
    this.stream = new WebPubSubStream({
      gameId,
      http: this.http,
      onMessage: (envelope) => this.dispatch(envelope.type, envelope.data),
      onConnected: () => {
        console.info(`[GameStream] connected — gameId=${gameId}`);
      },
      onReconnected: () => {
        console.info(`[GameStream] reconnected — gameId=${gameId}; triggering missed-event recovery`);
        this.reconnectedHandler?.();
      },
      log: (msg) => console.info(`[GameStream] ${msg}`),
      logError: (msg, ...args) => console.error(`[GameStream] ${msg}`, ...args),
    });
    void this.stream.start();
  }

  /** Register a handler for a specific event type. */
  on<T>(eventType: string, handler: (payload: T) => void): void {
    this.handlers.set(eventType, handler as (p: unknown) => void);
  }

  /** Register a callback fired after the WebSocket re-opens following a drop. */
  onReconnected(handler: () => void): void {
    this.reconnectedHandler = handler;
  }

  disconnect(): void {
    this.stream?.stop();
    this.stream = null;
    this.handlers.clear();
    this.reconnectedHandler = null;
  }

  private dispatch(type: string, data: unknown): void {
    const handler = this.handlers.get(type);
    if (!handler) return;
    try {
      handler(data);
    } catch (err) {
      console.error(`[GameStream] handler for '${type}' threw`, err);
    }
  }
}
