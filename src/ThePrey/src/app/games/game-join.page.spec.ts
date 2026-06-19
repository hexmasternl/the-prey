import { ActivatedRoute, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '@auth0/auth0-angular';

import { GameJoinPage } from './game-join.page';
import { GameDto, GamesService } from './games.service';
import { UserStateService } from '../users/user-state.service';

describe('GameJoinPage version gate', () => {
  let component: GameJoinPage;
  let gamesService: jasmine.SpyObj<GamesService>;

  beforeEach(() => {
    gamesService = jasmine.createSpyObj<GamesService>('GamesService', ['getGame', 'joinGame', 'checkAppVersion']);

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: { get: () => 'g1' } } },
        },
        { provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['navigate']) },
        // Auth is only used by restoreSession(), which these tests bypass by driving runVersionCheck directly.
        { provide: AuthService, useValue: {} },
        { provide: GamesService, useValue: gamesService },
        {
          provide: UserStateService,
          useValue: {
            profile: (() => ({ callsign: 'ALPHA' })) as unknown as UserStateService['profile'],
          },
        },
      ],
    });

    component = TestBed.runInInjectionContext(() => new GameJoinPage());

    // Preconditions for canJoin so the version gate is the only remaining factor.
    component.joinCode.set('1234');
    component.game.set(createGame());
  });

  /** Drives the private version check the way ionViewWillEnter does. */
  function runVersionCheck(): Promise<void> {
    return (component as unknown as { runVersionCheck(): Promise<void> }).runVersionCheck();
  }

  it('disables Join while the version check is in flight', () => {
    expect(component.versionChecked()).toBeFalse();
    expect(component.versionBlocked()).toBeTrue();
    expect(component.canJoin()).toBeFalse();
  });

  it('enables Join when the version check returns ok', async () => {
    gamesService.checkAppVersion.and.resolveTo('ok');

    await runVersionCheck();

    expect(component.updateRequired()).toBeFalse();
    expect(component.versionBlocked()).toBeFalse();
    expect(component.canJoin()).toBeTrue();
  });

  it('keeps Join disabled and flags update on a 409 result', async () => {
    gamesService.checkAppVersion.and.resolveTo('update-required');

    await runVersionCheck();

    expect(component.updateRequired()).toBeTrue();
    expect(component.versionBlocked()).toBeTrue();
    expect(component.canJoin()).toBeFalse();
  });
});

function createGame(overrides: Partial<GameDto> = {}): GameDto {
  return {
    id: 'g1',
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
    participants: [],
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
