import { GameStreamService } from './game-stream.service';
import { environment } from '../../environments/environment';

/** Minimal EventSource stand-in so we can drive events and inspect connections in tests. */
class FakeEventSource {
  static instances: FakeEventSource[] = [];
  readonly listeners = new Map<string, (e: MessageEvent) => void>();
  onerror: ((e: Event) => void) | null = null;
  closed = false;

  constructor(public readonly url: string) {
    FakeEventSource.instances.push(this);
  }

  addEventListener(type: string, cb: (e: MessageEvent) => void): void {
    this.listeners.set(type, cb);
  }

  close(): void {
    this.closed = true;
  }

  emit(type: string, data: string): void {
    this.listeners.get(type)?.({ data } as MessageEvent);
  }

  fail(): void {
    this.onerror?.(new Event('error'));
  }
}

describe('GameStreamService', () => {
  let service: GameStreamService;
  let originalEventSource: typeof EventSource;

  beforeEach(() => {
    originalEventSource = (globalThis as { EventSource: typeof EventSource }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource = FakeEventSource;
    FakeEventSource.instances = [];
    service = new GameStreamService();
  });

  afterEach(() => {
    service.disconnect();
    (globalThis as unknown as { EventSource: unknown }).EventSource = originalEventSource;
  });

  it('opens the stream with the JWT in the query string', () => {
    service.connect('game-1', 'tok-abc');

    expect(FakeEventSource.instances.length).toBe(1);
    expect(FakeEventSource.instances[0].url)
      .toBe(`${environment.apiUrl}/games/game-1/stream?token=tok-abc`);
  });

  it('dispatches parsed payloads to the registered handler', () => {
    const received: unknown[] = [];
    service.on('state-changed', (p) => received.push(p));
    service.connect('game-1', 'tok');

    FakeEventSource.instances[0].emit('state-changed', JSON.stringify({ gameId: 'game-1', newState: 'InProgress' }));

    expect(received).toEqual([{ gameId: 'game-1', newState: 'InProgress' }]);
  });

  it('ignores malformed event data without throwing', () => {
    service.on('game-ended', () => fail('handler should not be called for malformed data'));
    service.connect('game-1', 'tok');

    expect(() => FakeEventSource.instances[0].emit('game-ended', '{not json')).not.toThrow();
  });

  it('reconnects with backoff after a connection error', () => {
    jasmine.clock().install();
    try {
      service.connect('game-1', 'tok');
      expect(FakeEventSource.instances.length).toBe(1);

      FakeEventSource.instances[0].fail();
      expect(FakeEventSource.instances[0].closed).toBeTrue();

      // First retry is scheduled ~1s out.
      jasmine.clock().tick(1000);
      expect(FakeEventSource.instances.length).toBe(2);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('closes the connection and stops reconnecting on disconnect', () => {
    jasmine.clock().install();
    try {
      service.connect('game-1', 'tok');
      const source = FakeEventSource.instances[0];

      service.disconnect();
      expect(source.closed).toBeTrue();

      // A late error must not schedule a reconnect after disconnect.
      source.fail();
      jasmine.clock().tick(60_000);
      expect(FakeEventSource.instances.length).toBe(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
