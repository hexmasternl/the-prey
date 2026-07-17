import { signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';

import { GamePreyPage } from './game-prey.page';
import { GamesService, ParticipantDto } from './games.service';
import { GameLiveState, GameStateService } from './game-state.service';
import { GameLocationService } from './game-location.service';
import { CompassService } from './compass.service';
import { UserStateService } from '../users/user-state.service';
import { TourService } from './tour.service';

/** Minimal stand-in for the single source-of-truth service the page now reads from. */
class FakeGameStateService {
  readonly state = signal<GameLiveState | null>(null);
  readonly unavailable = signal(false);
  start = jasmine.createSpy('start').and.resolveTo();
  stop = jasmine.createSpy('stop');
  refreshNow = jasmine.createSpy('refreshNow').and.resolveTo();
  visibleParticipants = (): ParticipantDto[] => [];
}

describe('GamePreyPage tour', () => {
  let component: GamePreyPage;
  let tour: jasmine.SpyObj<TourService>;

  beforeEach(() => {
    tour = jasmine.createSpyObj<TourService>('TourService', ['hasSeen', 'markSeen']);
    tour.hasSeen.and.resolveTo(false);
    tour.markSeen.and.resolveTo();

    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'g1' } } } },
        { provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['navigate']) },
        { provide: GamesService, useValue: {} },
        { provide: GameStateService, useValue: new FakeGameStateService() },
        {
          provide: GameLocationService,
          useValue: {
            isTracking: signal(false),
            gpsError: signal(null),
            reportingDegraded: signal(false),
          },
        },
        { provide: CompassService, useValue: { heading: signal<number | null>(null), start: () => {}, stop: () => {} } },
        { provide: UserStateService, useValue: { profile: () => null } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: TourService, useValue: tour },
      ],
    });

    component = TestBed.runInInjectionContext(() => new GamePreyPage());
  });

  function startTour(): Promise<void> {
    return (component as unknown as { maybeStartTour(): Promise<void> }).maybeStartTour();
  }

  it('has a single time-bar step', () => {
    const steps = component.tourSteps();
    expect(steps.length).toBe(1);
    expect(steps[0].titleKey).toBe('GAME_TOUR.TIME_BAR_TITLE');
  });

  it('shows the prey tour on first entry', async () => {
    await startTour();

    expect(tour.hasSeen).toHaveBeenCalledWith('prey');
    expect(component.tourActive()).toBeTrue();
  });

  it('does not show the tour when prey has already seen it', async () => {
    tour.hasSeen.and.resolveTo(true);

    await startTour();

    expect(component.tourActive()).toBeFalse();
    expect(tour.markSeen).not.toHaveBeenCalled();
  });

  it('marks the prey tour seen on completion', () => {
    component.tourActive.set(true);

    component.onTourCompleted();

    expect(component.tourActive()).toBeFalse();
    expect(tour.markSeen).toHaveBeenCalledWith('prey');
  });

  it('evaluates the tour only once', async () => {
    await startTour();
    tour.hasSeen.calls.reset();

    await startTour();

    expect(tour.hasSeen).not.toHaveBeenCalled();
  });
});
