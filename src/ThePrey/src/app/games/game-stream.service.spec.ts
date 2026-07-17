import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { GameStreamService } from './game-stream.service';
import * as WebPubSubStreamModule from '../core/web-pubsub-stream';
import { RealtimeEnvelope } from '../core/web-pubsub-stream';

/** Lightweight stand-in that captures callbacks without opening a real WebSocket. */
class FakeWebPubSubStream {
  started = false;
  stopped = false;

  private messageHandler?: (envelope: RealtimeEnvelope) => void;
  private reconnectedHandler?: () => void;
  private connectedHandler?: () => void;
  private unavailableHandler?: () => void;

  constructor(public options: WebPubSubStreamModule.WebPubSubStreamOptions) {
    this.messageHandler = options.onMessage;
    this.connectedHandler = options.onConnected;
    this.reconnectedHandler = options.onReconnected;
    this.unavailableHandler = options.onUnavailable;
  }

  async start(): Promise<void> {
    this.started = true;
    this.connectedHandler?.();
  }

  stop(): void {
    this.stopped = true;
  }

  /** Test helper: simulate a group-message arriving from the server. */
  emit(type: string, data: unknown, seq = 1, v = 1): void {
    this.messageHandler?.({ v, type, gameId: 'game-1', seq, data });
  }

  /** Test helper: simulate the SDK reconnecting after a drop. */
  simulateReconnect(): void {
    this.reconnectedHandler?.();
  }

  simulateUnavailable(): void {
    this.unavailableHandler?.();
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

  it('forwards the full versioned envelope to the registered message handler', async () => {
    const received: RealtimeEnvelope[] = [];
    service.connect('game-1');
    service.onMessage((envelope) => received.push(envelope));
    await Promise.resolve();

    lastFakeStream.emit('configuration-changed', { status: 'InProgress' }, 7);

    expect(received).toEqual([
      { v: 1, type: 'configuration-changed', gameId: 'game-1', seq: 7, data: { status: 'InProgress' } },
    ]);
  });

  it('fires onConnected once the socket joins the group', async () => {
    const handler = jasmine.createSpy('connected');
    service.connect('game-1');
    service.onConnected(handler);
    await Promise.resolve();

    expect(handler).toHaveBeenCalled();
  });

  it('fires onReconnected after a stream reconnect', async () => {
    let reconnects = 0;
    service.connect('game-1');
    service.onReconnected(() => reconnects++);
    await Promise.resolve();

    lastFakeStream.simulateReconnect();

    expect(reconnects).toBe(1);
  });

  it('fires onUnavailable when the stream reports a terminal 403', async () => {
    const handler = jasmine.createSpy('unavailable');
    service.connect('game-1');
    service.onUnavailable(handler);
    await Promise.resolve();

    lastFakeStream.simulateUnavailable();

    expect(handler).toHaveBeenCalled();
  });

  it('stops the stream and clears handlers on disconnect()', async () => {
    const handler = jasmine.createSpy('handler');
    service.connect('game-1');
    service.onMessage(handler);
    await Promise.resolve();

    service.disconnect();

    expect(lastFakeStream.stopped).toBeTrue();

    // After disconnect, no more handler calls
    lastFakeStream.emit('game-ended', { gameId: 'game-1' });
    expect(handler).not.toHaveBeenCalled();
  });

  it('does not throw when a message handler throws', async () => {
    service.connect('game-1');
    service.onMessage(() => { throw new Error('handler error'); });
    await Promise.resolve();

    expect(() => lastFakeStream.emit('game-ended', {})).not.toThrow();
  });
});
