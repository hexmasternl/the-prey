import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ToastController } from '@ionic/angular/standalone';
import { TranslateService } from '@ngx-translate/core';

import { GameLobbyPage } from './game-lobby.page';
import { GameDto, GamesService } from './games.service';
import { GameLocationService } from './game-location.service';
import { UserStateService } from '../users/user-state.service';

describe('GameLobbyPage', () => {
  let component: GameLobbyPage;

  beforeEach(() => {
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
        { provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['navigate']) },
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
        { provide: GameLocationService, useValue: jasmine.createSpyObj<GameLocationService>('GameLocationService', ['start']) },
        {
          provide: UserStateService,
          useValue: {
            profile: (() => ({ userId: 'host-1' })) as unknown as UserStateService['profile'],
          },
        },
        { provide: HttpClient, useValue: {} },
        { provide: ToastController, useValue: jasmine.createSpyObj<ToastController>('ToastController', ['create']) },
        { provide: TranslateService, useValue: jasmine.createSpyObj<TranslateService>('TranslateService', ['instant']) },
      ],
    });

    component = TestBed.runInInjectionContext(() => new GameLobbyPage());
  });

  it('keeps host controls when a realtime lobby update drops isOwnerPlayer', () => {
    component.game.set(createGame({ isOwnerPlayer: true }));

    ((component as unknown) as { onLobbyEvent(type: string, data: unknown): void }).onLobbyEvent(
      'lobby-updated',
      createGame({
        isOwnerPlayer: false,
        participants: [
          createParticipant('host-1', 'Host'),
          createParticipant('player-2', 'Scout', true),
          createParticipant('player-3', 'Rookie'),
        ],
      }),
    );

    expect(component.isOwner()).toBeTrue();
    expect(component.game()?.participants.length).toBe(3);
    expect(component.isReady('player-2')).toBeTrue();
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
