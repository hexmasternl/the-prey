import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { SseStream } from '../core/sse-stream';

export type GameEventType = 'state-changed' | 'participant-located' | 'participant-status-changed' | 'game-ended';

export interface StateChangedPayload { gameId: string; newState: string; }
export interface ParticipantLocatedPayload { gameId: string; userId: string; participantRole: string; latitude: number; longitude: number; participantState: string; }
export interface ParticipantStatusChangedPayload { gameId: string; participantId: string; participantRole: string; newState: string; }
export interface GameEndedPayload { gameId: string; }

type EventPayloadMap = {
  'state-changed': StateChangedPayload;
  'participant-located': ParticipantLocatedPayload;
  'participant-status-changed': ParticipantStatusChangedPayload;
  'game-ended': GameEndedPayload;
};

type HandlerMap = { [K in GameEventType]?: (payload: EventPayloadMap[K]) => void };

const GAME_EVENT_TYPES: readonly GameEventType[] = ['state-changed', 'participant-located', 'participant-status-changed', 'game-ended'];

/**
 * In-game SSE stream (/games/{id}/stream). Reconnection, token refresh, heartbeat watchdog
 * and app-resume recovery live in {@link SseStream}; this service adds typed event dispatch.
 */
@Injectable({ providedIn: 'root' })
export class GameStreamService {
  private stream: SseStream | null = null;
  private handlers: HandlerMap = {};
  private reconnectedHandler: (() => void) | null = null;

  /**
   * Opens the stream. `getToken` is invoked for every (re)connect attempt so reconnects never
   * reuse an expired JWT (the token rides in the URL and is fixed for a connection's lifetime).
   */
  connect(gameId: string, getToken: () => Promise<string>): void {
    this.disconnect();
    this.stream = new SseStream({
      buildUrl: (token) => `${environment.apiUrl}/games/${gameId}/stream?token=${encodeURIComponent(token)}`,
      events: GAME_EVENT_TYPES,
      getToken,
      onEvent: (type, data) => this.dispatch(type as GameEventType, data),
      onReconnected: () => this.reconnectedHandler?.(),
      probeStatusOnError: true,
      log: (msg) => console.info(`[GameStream] ${msg}`),
      logError: (msg, ...args) => console.error(`[GameStream] ${msg}`, ...args),
    });
    void this.stream.start();
  }

  on<K extends GameEventType>(eventType: K, handler: (payload: EventPayloadMap[K]) => void): void {
    (this.handlers as Record<string, unknown>)[eventType] = handler;
  }

  /** Registers a callback fired after the stream re-opens following a drop — refetch state there. */
  onReconnected(handler: () => void): void {
    this.reconnectedHandler = handler;
  }

  disconnect(): void {
    this.stream?.stop();
    this.stream = null;
    this.handlers = {};
    this.reconnectedHandler = null;
  }

  private dispatch(type: GameEventType, data: string): void {
    const handler = this.handlers[type];
    if (!handler) return;
    try {
      const payload = JSON.parse(data);
      (handler as (p: unknown) => void)(payload);
    } catch {
      // malformed event — ignore
    }
  }
}
