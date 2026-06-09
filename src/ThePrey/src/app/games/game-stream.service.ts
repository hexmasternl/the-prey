import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

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

@Injectable({ providedIn: 'root' })
export class GameStreamService {
  private source: EventSource | null = null;
  private handlers: HandlerMap = {};
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private reconnectDelay = 1000;
  private gameId: string | null = null;
  private token: string | null = null;

  connect(gameId: string, token: string): void {
    this.gameId = gameId;
    this.token = token;
    this.reconnectDelay = 1000;
    this.openConnection();
  }

  on<K extends GameEventType>(eventType: K, handler: (payload: EventPayloadMap[K]) => void): void {
    (this.handlers as Record<string, unknown>)[eventType] = handler;
  }

  disconnect(): void {
    this.clearReconnect();
    if (this.source) {
      this.source.close();
      this.source = null;
    }
    this.handlers = {};
    this.gameId = null;
    this.token = null;
  }

  private openConnection(): void {
    if (!this.gameId || !this.token) return;

    // EventSource cannot send an Authorization header, so the JWT is passed as a query
    // parameter and validated server-side (JwtBearer OnMessageReceived). Without this the
    // authenticated /stream endpoint always responds 401 and the live game never connects.
    const url = `${environment.apiUrl}/games/${this.gameId}/stream?token=${encodeURIComponent(this.token)}`;
    this.source = new EventSource(url);

    const eventTypes: GameEventType[] = ['state-changed', 'participant-located', 'participant-status-changed', 'game-ended'];
    for (const type of eventTypes) {
      this.source.addEventListener(type, (e: MessageEvent) => {
        const handler = this.handlers[type];
        if (handler) {
          try {
            const payload = JSON.parse(e.data);
            (handler as (p: unknown) => void)(payload);
          } catch { /* malformed event — ignore */ }
        }
      });
    }

    this.source.onerror = () => {
      this.source?.close();
      this.source = null;
      if (this.gameId) {
        this.scheduleReconnect();
      }
    };
  }

  private scheduleReconnect(): void {
    this.clearReconnect();
    this.reconnectTimer = setTimeout(() => {
      this.openConnection();
      this.reconnectDelay = Math.min(this.reconnectDelay * 2, 30000);
    }, this.reconnectDelay);
  }

  private clearReconnect(): void {
    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }
}
