import { GameStreamService } from './game-stream.service';
import { environment } from '../../environments/environment';

/** Minimal EventSource stand-in so we can drive events and inspect connections in tests. */
class FakeEventSource {
  static instances: FakeEventSource[] = [];
  readonly listeners = new Map<string, (e: MessageEvent) => void>();
  onerror: ((e: Event) => void) | null = null;
  onopen: ((e: Event) => void) | null = null;
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

  open(): void {
    this.onopen?.(new Event('open'));
  }

  fail(): void {
    this.onerror?.(new Event('error'));
  }
}

/**
 * Connection opening is async (a fresh token is fetched per attempt), so tests must let the
 * microtask queue drain before the EventSource exists. Promise hops are unaffected by
 * jasmine.clock(), which only fakes timers.
 */
async function flushMicrotasks(): Promise<void> {
  for (let i = 0; i < 10; i++) await Promise.resolve();
}

describe('GameStreamService', () => {
  let service: GameStreamService;
  let originalEventSource: typeof EventSource;
  const getToken = () => Promise.resolve('tok-abc');

  beforeEach(() => {
    originalEventSource = (globalThis as { EventSource: typeof EventSource }).EventSource;
    (globalThis as unknown as { EventSource: unknown }).EventSource = FakeEventSource;
    FakeEventSource.instances = [];
    // The diagnostic status probe fetches the stream URL after a failure — stub it out so
    // tests never make real network calls.
    spyOn(globalThis, 'fetch').and.resolveTo(new Response(null, { status: 403, statusText: 'Forbidden' }));
    service = new GameStreamService();
  });

  afterEach(() => {
    service.disconnect();
    (globalThis as unknown as { EventSource: unknown }).EventSource = originalEventSource;
  });

  it('opens the stream with a freshly fetched JWT in the query string', async () => {
    service.connect('game-1', getToken);
    await flushMicrotasks();

    expect(FakeEventSource.instances.length).toBe(1);
    expect(FakeEventSource.instances[0].url)
      .toBe(`${environment.apiUrl}/games/game-1/stream?token=tok-abc`);
  });

  it('dispatches parsed payloads to the registered handler', async () => {
    const received: unknown[] = [];
    service.connect('game-1', getToken);
    service.on('state-changed', (p) => received.push(p));
    await flushMicrotasks();

    FakeEventSource.instances[0].emit('state-changed', JSON.stringify({ gameId: 'game-1', newState: 'InProgress' }));

    expect(received).toEqual([{ gameId: 'game-1', newState: 'InProgress' }]);
  });

  it('ignores malformed event data without throwing', async () => {
    service.connect('game-1', getToken);
    service.on('game-ended', () => fail('handler should not be called for malformed data'));
    await flushMicrotasks();

    expect(() => FakeEventSource.instances[0].emit('game-ended', '{not json')).not.toThrow();
  });

  it('reconnects with a fresh token after a connection error', async () => {
    jasmine.clock().install();
    try {
      let calls = 0;
      service.connect('game-1', () => Promise.resolve(`tok-${++calls}`));
      await flushMicrotasks();
      expect(FakeEventSource.instances.length).toBe(1);

      FakeEventSource.instances[0].fail();
      expect(FakeEventSource.instances[0].closed).toBeTrue();

      // First retry is scheduled ~1s out and must fetch a NEW token, not reuse the old one.
      // (The exact counter value is not asserted — the diagnostic probe also consumes one.)
      jasmine.clock().tick(1000);
      await flushMicrotasks();
      expect(FakeEventSource.instances.length).toBe(2);
      expect(FakeEventSource.instances[1].url).not.toContain('token=tok-1');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('fires onReconnected after the stream re-opens following a drop', async () => {
    jasmine.clock().install();
    try {
      let reconnected = 0;
      service.connect('game-1', getToken);
      service.onReconnected(() => reconnected++);
      await flushMicrotasks();

      FakeEventSource.instances[0].open();
      expect(reconnected).toBe(0); // first open is not a reconnect

      FakeEventSource.instances[0].fail();
      jasmine.clock().tick(1000);
      await flushMicrotasks();

      FakeEventSource.instances[1].open();
      expect(reconnected).toBe(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('reconnects when no event or heartbeat arrives within the staleness window', async () => {
    jasmine.clock().install();
    // The watchdog compares Date.now() timestamps; mockDate makes tick() advance them too.
    jasmine.clock().mockDate();
    try {
      service.connect('game-1', getToken);
      await flushMicrotasks();
      const first = FakeEventSource.instances[0];
      first.open();

      // Heartbeats keep the watchdog quiet…
      jasmine.clock().tick(40_000);
      first.emit('heartbeat', '{}');
      jasmine.clock().tick(40_000);
      first.emit('heartbeat', '{}');
      expect(FakeEventSource.instances.length).toBe(1);

      // …silence beyond the staleness window forces a reconnect.
      jasmine.clock().tick(50_000);
      await flushMicrotasks();
      expect(first.closed).toBeTrue();
      expect(FakeEventSource.instances.length).toBe(2);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('closes the connection and stops reconnecting on disconnect', async () => {
    jasmine.clock().install();
    try {
      service.connect('game-1', getToken);
      await flushMicrotasks();
      const source = FakeEventSource.instances[0];

      service.disconnect();
      expect(source.closed).toBeTrue();

      // A late error must not schedule a reconnect after disconnect.
      source.fail();
      jasmine.clock().tick(60_000);
      await flushMicrotasks();
      expect(FakeEventSource.instances.length).toBe(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
