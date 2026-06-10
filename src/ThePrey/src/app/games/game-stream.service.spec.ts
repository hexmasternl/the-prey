import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { GameStreamService } from './game-stream.service';
import * as WebPubSubStreamModule from '../core/web-pubsub-stream';

/** Lightweight stand-in that captures callbacks without opening a real WebSocket. */
class FakeWebPubSubStream {
  started = false;
  stopped = false;

  private messageHandler?: (envelope: { type: string; data: unknown }) => void;
  private reconnectedHandler?: () => void;
  private connectedHandler?: () => void;

  constructor(public options: WebPubSubStreamModule.WebPubSubStreamOptions) {
    this.messageHandler = options.onMessage;
    this.connectedHandler = options.onConnected;
    this.reconnectedHandler = options.onReconnected;
  }

  async start(): Promise<void> {
    this.started = true;
    this.connectedHandler?.();
  }

  stop(): void {
    this.stopped = true;
  }

  /** Test helper: simulate a group-message arriving from the server. */
  emit(type: string, data: unknown): void {
    this.messageHandler?.({ type, data });
  }

  /** Test helper: simulate the SDK reconnecting after a drop. */
  simulateReconnect(): void {
    this.reconnectedHandler?.();
  }
}

describe('GameStreamService', () => {
  let service: GameStreamService;
  let lastFakeStream: FakeWebPubSubStream;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        GameStreamService,
        { provide: HttpClient, useValue: {} },
      ],
    });

    // Intercept WebPubSubStream construction so no real WebSocket is created.
    spyOn(WebPubSubStreamModule, 'WebPubSubStream').and.callFake((opts: WebPubSubStreamModule.WebPubSubStreamOptions) => {
      lastFakeStream = new FakeWebPubSubStream(opts);
      return lastFakeStream as unknown as WebPubSubStreamModule.WebPubSubStream;
    });

    service = TestBed.inject(GameStreamService);
  });

  afterEach(() => {
    service.disconnect();
  });

  it('starts the stream on connect()', async () => {
    service.connect('game-1');
    await Promise.resolve(); // let start() settle

    expect(lastFakeStream.started).toBeTrue();
  });

  it('dispatches received messages to registered handlers', async () => {
    const received: unknown[] = [];
    service.connect('game-1');
    service.on<{ gameId: string; newState: string }>('state-changed', (p) => received.push(p));
    await Promise.resolve();

    lastFakeStream.emit('state-changed', { gameId: 'game-1', newState: 'InProgress' });

    expect(received).toEqual([{ gameId: 'game-1', newState: 'InProgress' }]);
  });

  it('does not call handlers for unregistered event types', async () => {
    const called = jasmine.createSpy('handler');
    service.connect('game-1');
    service.on('player-location-updated', called);
    await Promise.resolve();

    // Emit an event that has no handler registered
    lastFakeStream.emit('state-changed', { gameId: 'game-1' });

    expect(called).not.toHaveBeenCalled();
  });

  it('fires onReconnected after a stream reconnect', async () => {
    let reconnects = 0;
    service.connect('game-1');
    service.onReconnected(() => reconnects++);
    await Promise.resolve();

    lastFakeStream.simulateReconnect();

    expect(reconnects).toBe(1);
  });

  it('stops the stream and clears handlers on disconnect()', async () => {
    const handler = jasmine.createSpy('handler');
    service.connect('game-1');
    service.on('game-ended', handler);
    await Promise.resolve();

    service.disconnect();

    expect(lastFakeStream.stopped).toBeTrue();

    // After disconnect, no more handler calls
    lastFakeStream.emit('game-ended', { gameId: 'game-1' });
    expect(handler).not.toHaveBeenCalled();
  });

  it('does not throw when a handler throws', async () => {
    service.connect('game-1');
    service.on('game-ended', () => { throw new Error('handler error'); });
    await Promise.resolve();

    expect(() => lastFakeStream.emit('game-ended', {})).not.toThrow();
  });
});
