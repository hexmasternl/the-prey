import { signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { ToastController } from '@ionic/angular/standalone';
import { TranslateService } from '@ngx-translate/core';

import { GameLobbyPage } from './game-lobby.page';
import { GameDto, GamesService } from './games.service';
import { GameLiveState, GameStateService } from './game-state.service';
import { GameLocationService } from './game-location.service';
import { UserStateService } from '../users/user-state.service';

/** Minimal stand-in for the single source-of-truth service the page now reads from. */
class FakeGameStateService {
  private readonly _state = signal<GameLiveState | null>(null);
  private readonly _unavailable = signal(false);
  readonly state = this._state.asReadonly();
  readonly unavailable = this._unavailable.asReadonly();

  start = jasmine.createSpy('start').and.resolveTo();
  stop = jasmine.createSpy('stop');
  refreshNow = jasmine.createSpy('refreshNow').and.resolveTo();
  applyOwnMutation = jasmine.createSpy('applyOwnMutation').and.callFake((game: GameDto) => {
    this.setState(game, this._state()?.isOwner ?? false);
  });

  setState(game: GameDto, isOwner: boolean): void {
    this._state.set({ game, status: null, roleState: null, isOwner, lastLocationsUpdateAt: null });
  }

  setUnavailable(): void {
    this._unavailable.set(true);
  }
}

describe('GameLobbyPage', () => {
  let component: GameLobbyPage;
  let router: jasmine.SpyObj<Router>;
  let gameState: FakeGameStateService;

  beforeEach(() => {
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    gameState = new FakeGameStateService();

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: () => 'game-1',
              },
            },
          },
        },
        { provide: Router, useValue: router },
        {
          provide: GamesService,
          useValue: jasmine.createSpyObj<GamesService>('GamesService', [
            'getGame',
            'updateConfig',
            'setHunter',
            'removePlayer',
            'startGame',
            'setReady',
          ]),
        },
        { provide: GameStateService, useValue: gameState },
        { provide: GameLocationService, useValue: jasmine.createSpyObj<GameLocationService>('GameLocationService', ['start']) },
        {
          provide: UserStateService,
          useValue: {
            profile: (() => ({ userId: 'host-1' })) as unknown as UserStateService['profile'],
          },
        },
        {
          provide: ToastController,
          useValue: (() => {
            const toastCtrl = jasmine.createSpyObj<ToastController>('ToastController', ['create']);
            toastCtrl.create.and.resolveTo(jasmine.createSpyObj<HTMLIonToastElement>('toast', ['present']));
            return toastCtrl;
          })(),
        },
        { provide: TranslateService, useValue: jasmine.createSpyObj<TranslateService>('TranslateService', ['instant']) },
      ],
    });

    component = TestBed.runInInjectionContext(() => new GameLobbyPage());
  });

  it('starts the single source-of-truth service for this game on view enter', async () => {
    await component.ionViewWillEnter();

    expect(gameState.start).toHaveBeenCalledWith('game-1');
  });

  it('reads game and ownership from the shared GameStateService', () => {
    gameState.setState(createGame({ isOwnerPlayer: false }), true);

    expect(component.game()?.id).toBe('game-1');
    expect(component.isOwner()).toBeTrue();
  });

  it('navigates to the hunt view when the shared state transitions to InProgress for the hunter', () => {
    gameState.setState(
      createGame({ status: 'InProgress', hunterUserId: 'host-1', startedAt: '2026-06-11T00:00:00Z' }),
      true,
    );
    TestBed.tick();

    expect(router.navigate).toHaveBeenCalledWith(['/games', 'game-1', 'hunt'], { replaceUrl: true });
  });

  it('navigates to the play view when the shared state transitions to InProgress for a prey', () => {
    gameState.setState(
      createGame({
        status: 'InProgress',
        hunterUserId: 'player-2',
        preys: ['host-1'],
        startedAt: '2026-06-11T00:00:00Z',
      }),
      false,
    );
    TestBed.tick();

    expect(router.navigate).toHaveBeenCalledWith(['/games', 'game-1', 'play'], { replaceUrl: true });
  });

  it('leaves the lobby and stops the service when the state reports unavailable', async () => {
    gameState.setUnavailable();
    TestBed.tick();
    // leaveDeadLobby() is fire-and-forget from the effect and awaits a toast before
    // navigating — a macrotask flush lets every queued microtask (including those
    // scheduled by later `await`s in the chain) settle before asserting.
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(gameState.stop).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/home'], { replaceUrl: true });
  });

  it('isReady reflects the participant flag from the shared state', () => {
    gameState.setState(
      createGame({
        participants: [
          createParticipant('host-1', 'Host'),
          createParticipant('player-2', 'Scout', true),
        ],
      }),
      true,
    );

    expect(component.isReady('player-2')).toBeTrue();
    expect(component.isReady('host-1')).toBeFalse();
  });
});

function createGame(overrides: Partial<GameDto> = {}): GameDto {
  return {
    id: 'game-1',
    gameCode: '1234',
    playfieldId: 'playfield-1',
    ownerUserId: 'host-1',
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
      createParticipant('host-1', 'Host'),
      createParticipant('player-2', 'Scout'),
    ],
    hunterUserId: null,
    preys: [],
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

function createParticipant(userId: string, displayName: string, isReady = false) {
  return {
    userId,
    displayName,
    profilePictureUrl: null,
    isReady,
    state: 'Active',
    lastKnownLocation: null,
    hasActivePenalty: false,
  };
}
