import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';

import { GameStateService } from './game-state.service';
import { GameDto, GamesService, GameStatusDto } from './games.service';
import { GameStreamService, RealtimeEnvelope } from './game-stream.service';
import { UserStateService } from '../users/user-state.service';

/** Captures the handlers GameStateService registers so tests can simulate the wire. */
class FakeGameStreamService {
  readonly connectCalls: string[] = [];
  disconnectCalls = 0;
  private messageHandler?: (envelope: RealtimeEnvelope) => void;
  private connectedHandler?: () => void;
  private reconnectedHandler?: () => void;
  private unavailableHandler?: () => void;

  connect(gameId: string): void {
    this.connectCalls.push(gameId);
  }

  onMessage(handler: (envelope: RealtimeEnvelope) => void): void {
    this.messageHandler = handler;
  }

  onConnected(handler: () => void): void {
    this.connectedHandler = handler;
  }

  onReconnected(handler: () => void): void {
    this.reconnectedHandler = handler;
  }

  onUnavailable(handler: () => void): void {
    this.unavailableHandler = handler;
  }

  disconnect(): void {
    this.disconnectCalls++;
  }

  emit(envelope: RealtimeEnvelope): void {
    this.messageHandler?.(envelope);
  }

  fireUnavailable(): void {
    this.unavailableHandler?.();
  }
}

function envelope<T>(type: string, seq: number, data: T, v = 1): RealtimeEnvelope<T> {
  return { v, type, gameId: 'game-1', seq, data };
}

function createGame(overrides: Partial<GameDto> = {}): GameDto {
  return {
    id: 'game-1',
    gameCode: '1234',
    playfieldId: 'playfield-1',
    ownerUserId: 'owner-1',
    status: 'Lobby',
    configuration: {
      gameDuration: 60,
      hunterDelayTime: 10,
      finalStageDuration: 10,
      defaultLocationInterval: 300,
      finalLocationInterval: 180,
      enablePreyBoundaryPenalties: false,
      enableHunterBoundaryPenalty: false,
    },
    participants: [
      { userId: 'p1', displayName: 'Alpha', profilePictureUrl: null, isReady: false, state: 'Active', lastKnownLocation: null, hasActivePenalty: false },
      { userId: 'p2', displayName: 'Bravo', profilePictureUrl: null, isReady: false, state: 'Active', lastKnownLocation: null, hasActivePenalty: false },
    ],
    hunterUserId: 'p1',
    preys: ['p2'],
    startedAt: null,
    createdAt: '2026-06-11T00:00:00Z',
    endsAt: null,
    cleanUpAfter: '2026-06-12T00:00:00Z',
    outcome: 'Pending',
    completedAt: null,
    isOwnerPlayer: false,
    isReadyToStart: false,
    ...overrides,
  };
}

describe('GameStateService', () => {
  let service: GameStateService;
  let stream: FakeGameStreamService;
  let gamesService: jasmine.SpyObj<GamesService>;

  beforeEach(() => {
    stream = new FakeGameStreamService();
    gamesService = jasmine.createSpyObj<GamesService>('GamesService', [
      'getGame',
      'getGameStatus',
      'getGameState',
    ]);
    gamesService.getGame.and.resolveTo(createGame());

    TestBed.configureTestingModule({
      providers: [
        GameStateService,
        { provide: GameStreamService, useValue: stream },
        { provide: GamesService, useValue: gamesService },
        { provide: UserStateService, useValue: { profile: () => ({ userId: 'user-1' }) } },
      ],
    });

    service = TestBed.inject(GameStateService);
  });

  afterEach(() => {
    service.stop();
  });

  it('loads a full snapshot on start without status/state while not InProgress', async () => {
    await service.start('game-1');

    expect(gamesService.getGame).toHaveBeenCalledWith('game-1');
    expect(gamesService.getGameStatus).not.toHaveBeenCalled();
    expect(service.state()?.game.id).toBe('game-1');
    expect(service.state()?.status).toBeNull();
    expect(stream.connectCalls).toEqual(['game-1']);
  });

  it('also loads status and role state while InProgress', async () => {
    gamesService.getGame.and.resolveTo(createGame({ status: 'InProgress' }));
    gamesService.getGameStatus.and.resolveTo({ gameDurationLeft: 100 } as GameStatusDto);
    gamesService.getGameState.and.resolveTo({ hunterDistanceMeters: 42, preyLocations: [] });

    await service.start('game-1');

    expect(gamesService.getGameStatus).toHaveBeenCalledWith('game-1');
    expect(gamesService.getGameState).toHaveBeenCalledWith('game-1');
    expect(service.state()?.status?.gameDurationLeft).toBe(100);
    expect(service.state()?.roleState?.hunterDistanceMeters).toBe(42);
  });

  it('adds a participant on participant-joined without disturbing others', async () => {
    await service.start('game-1');

    stream.emit(envelope('participant-joined', 1, {
      userId: 'p3', displayName: 'Charlie', profilePictureUrl: null, isReady: false, state: 'Active', lastKnownLocation: null, hasActivePenalty: false,
    }));

    const participants = service.state()!.game.participants;
    expect(participants.map(p => p.userId)).toEqual(['p1', 'p2', 'p3']);
  });

  it('replaces a participant wholesale on participant-changed', async () => {
    await service.start('game-1');

    stream.emit(envelope('participant-changed', 1, {
      userId: 'p2', displayName: 'Bravo', profilePictureUrl: null, isReady: true, state: 'Active', lastKnownLocation: null, hasActivePenalty: false,
    }));

    const p2 = service.state()!.game.participants.find(p => p.userId === 'p2');
    expect(p2?.isReady).toBeTrue();
    expect(service.state()!.game.participants.length).toBe(2);
  });

  it('removes the named participant on participant-removed', async () => {
    await service.start('game-1');

    stream.emit(envelope('participant-removed', 1, { userId: 'p2' }));

    expect(service.state()!.game.participants.map(p => p.userId)).toEqual(['p1']);
  });

  it('updates game-level fields on configuration-changed while preserving participants', async () => {
    await service.start('game-1');

    stream.emit(envelope('configuration-changed', 1, {
      id: 'game-1',
      gameCode: '1234',
      playfieldId: 'playfield-1',
      ownerUserId: 'owner-1',
      status: 'InProgress',
      configuration: createGame().configuration,
      hunterUserId: 'p1',
      preys: ['p2'],
      startedAt: '2026-06-11T01:00:00Z',
      createdAt: '2026-06-11T00:00:00Z',
      endsAt: '2026-06-11T02:00:00Z',
      cleanUpAfter: '2026-06-12T00:00:00Z',
      outcome: 'Pending',
      completedAt: null,
    }));

    const state = service.state()!;
    expect(state.game.status).toBe('InProgress');
    expect(state.game.startedAt).toBe('2026-06-11T01:00:00Z');
    expect(state.game.participants.map(p => p.userId)).toEqual(['p1', 'p2']);
  });

  it('overlays only the named participants on locations-updated', async () => {
    await service.start('game-1');

    stream.emit(envelope('locations-updated', 1, {
      locations: [{ userId: 'p2', role: 'Prey', latitude: 1.1, longitude: 2.2, state: 'Active' }],
    }));

    const state = service.state()!;
    const p1 = state.game.participants.find(p => p.userId === 'p1');
    const p2 = state.game.participants.find(p => p.userId === 'p2');
    expect(p1?.lastKnownLocation).toBeNull();
    expect(p2?.lastKnownLocation).toEqual({ latitude: 1.1, longitude: 2.2 });
    expect(state.lastLocationsUpdateAt).not.toBeNull();
  });

  it('applies tagged/penalized/penalty-cleared on prey-updated', async () => {
    await service.start('game-1');

    stream.emit(envelope('prey-updated', 1, { userId: 'p2', event: 'penalized', penaltyEndsAt: '2026-06-11T00:05:00Z', reason: 'left-playfield' }));
    expect(service.state()!.game.participants.find(p => p.userId === 'p2')?.hasActivePenalty).toBeTrue();

    stream.emit(envelope('prey-updated', 2, { userId: 'p2', event: 'penalty-cleared' }));
    expect(service.state()!.game.participants.find(p => p.userId === 'p2')?.hasActivePenalty).toBeFalse();

    stream.emit(envelope('prey-updated', 3, { userId: 'p2', event: 'tagged', state: 'Tagged' }));
    expect(service.state()!.game.participants.find(p => p.userId === 'p2')?.state).toBe('Tagged');
  });

  it('marks the game Completed on game-ended', async () => {
    await service.start('game-1');

    stream.emit(envelope('game-ended', 1, { outcome: 'HuntersWin', survivorCount: 0, completedAt: '2026-06-11T02:00:00Z' }));

    expect(service.state()!.game.status).toBe('Completed');
    expect(service.state()!.game.outcome).toBe('HuntersWin');
  });

  it('resyncs (re-fetches) instead of applying a message with an unsupported version', async () => {
    await service.start('game-1');
    gamesService.getGame.calls.reset();

    stream.emit(envelope('participant-removed', 1, { userId: 'p2' }, /* v */ 2));
    await Promise.resolve();
    await Promise.resolve();

    expect(gamesService.getGame).toHaveBeenCalled();
    // The out-of-version delta itself must not have been applied.
    expect(service.state()!.game.participants.map(p => p.userId)).toEqual(['p1', 'p2']);
  });

  it('resyncs instead of applying an out-of-order (gapped) message', async () => {
    await service.start('game-1');
    stream.emit(envelope('participant-removed', 1, { userId: 'never-applied' }));
    gamesService.getGame.calls.reset();

    // seq should be 2 next; 5 is a gap.
    stream.emit(envelope('participant-removed', 5, { userId: 'p2' }));
    await Promise.resolve();
    await Promise.resolve();

    expect(gamesService.getGame).toHaveBeenCalled();
    expect(service.state()!.game.participants.map(p => p.userId)).toEqual(['p1', 'p2']);
  });

  it('resyncs on a resync-requested control message', async () => {
    await service.start('game-1');
    gamesService.getGame.calls.reset();

    stream.emit(envelope('resync-requested', 1, { reason: 'server-hint' }));
    await Promise.resolve();
    await Promise.resolve();

    expect(gamesService.getGame).toHaveBeenCalled();
  });

  it('notifies every subscriber and isolates one that throws', async () => {
    await service.start('game-1');
    const good1 = jasmine.createSpy('good1');
    const good2 = jasmine.createSpy('good2');
    service.subscribe(() => { throw new Error('boom'); });
    service.subscribe(good1);
    service.subscribe(good2);

    stream.emit(envelope('participant-removed', 1, { userId: 'p2' }));

    expect(good1).toHaveBeenCalled();
    expect(good2).toHaveBeenCalled();
  });

  it('stops notifying a consumer after it unsubscribes', async () => {
    await service.start('game-1');
    const handler = jasmine.createSpy('handler');
    const unsubscribe = service.subscribe(handler);

    stream.emit(envelope('participant-removed', 1, { userId: 'p2' }));
    expect(handler).toHaveBeenCalledTimes(1);

    unsubscribe();
    stream.emit(envelope('participant-changed', 2, { ...createGame().participants[0], isReady: true }));
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('reports unavailable and disconnects on a terminal 403', async () => {
    gamesService.getGame.and.rejectWith(new HttpErrorResponse({ status: 403 }));

    await service.start('game-1');

    expect(service.unavailable()).toBeTrue();
    expect(stream.disconnectCalls).toBeGreaterThan(0);
  });

  it('reports unavailable when the underlying stream reports 403 after connecting', async () => {
    await service.start('game-1');

    stream.fireUnavailable();

    expect(service.unavailable()).toBeTrue();
  });

  it('keeps ownership sticky across a configuration-changed that omits isOwnerPlayer', async () => {
    gamesService.getGame.and.resolveTo(createGame({ ownerUserId: 'user-1', isOwnerPlayer: true }));
    await service.start('game-1');
    expect(service.state()!.isOwner).toBeTrue();

    stream.emit(envelope('configuration-changed', 1, {
      id: 'game-1',
      gameCode: '1234',
      playfieldId: 'playfield-1',
      ownerUserId: 'user-1',
      status: 'Ready',
      configuration: createGame().configuration,
      hunterUserId: 'p1',
      preys: ['p2'],
      startedAt: null,
      createdAt: '2026-06-11T00:00:00Z',
      endsAt: null,
      cleanUpAfter: '2026-06-12T00:00:00Z',
      outcome: 'Pending',
      completedAt: null,
    }));

    expect(service.state()!.isOwner).toBeTrue();
  });

  it('excludes the caller and filters by role in visibleParticipants', async () => {
    gamesService.getGame.and.resolveTo(createGame({ hunterUserId: 'p1', preys: ['p2', 'p3'] }));
    const game = createGame({ hunterUserId: 'p1', preys: ['p2', 'p3'] });
    game.participants.push({ userId: 'p3', displayName: 'Charlie', profilePictureUrl: null, isReady: false, state: 'Active', lastKnownLocation: null, hasActivePenalty: false });
    gamesService.getGame.and.resolveTo(game);
    await service.start('game-1');

    // A prey (p2) sees only the hunter.
    expect(service.visibleParticipants('p2').map(p => p.userId)).toEqual(['p1']);
    // The hunter (p1) sees every prey.
    expect(service.visibleParticipants('p1').map(p => p.userId)).toEqual(['p2', 'p3']);
  });
});
