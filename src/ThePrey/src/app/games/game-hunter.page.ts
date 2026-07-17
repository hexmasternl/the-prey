import {
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonContent,
  IonModal,
  IonHeader,
  IonToolbar,
  IonTitle,
  IonFooter,
  IonButton,
  IonCheckbox,
  IonLabel,
  IonItem,
  IonSpinner,
  IonRefresher,
  IonRefresherContent,
  RefresherCustomEvent,
  ToastController,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import * as L from 'leaflet';
import { Geolocation } from '@capacitor/geolocation';
import { Preferences } from '@capacitor/preferences';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  GameDto,
  GameStatusDto,
  GamesService,
  TagCandidateDto,
} from './games.service';
import { computeThreatState, ThreatState } from './threat-state';
import { MAP_COLORS } from '../shared/map-colors';
import { countActivePreys, GameStateService } from './game-state.service';
import { GameLocationService } from './game-location.service';
import { CompassService } from './compass.service';
import { HunterDelayOverlayComponent } from './hunter-delay-overlay.component';
import { GameTourComponent, TourStep } from './game-tour.component';
import { TourService } from './tour.service';
import { UserStateService } from '../users/user-state.service';

@Component({
  selector: 'app-game-hunter',
  templateUrl: 'game-hunter.page.html',
  styleUrls: ['game-hunter.page.scss'],
  imports: [
    DecimalPipe,
    IonContent,
    IonModal,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonFooter,
    IonButton,
    IonCheckbox,
    IonLabel,
    IonItem,
    IonSpinner,
    IonRefresher,
    IonRefresherContent,
    TranslatePipe,
    HunterDelayOverlayComponent,
    GameTourComponent,
  ],
})
export class GameHunterPage implements OnInit, OnDestroy, ViewWillEnter {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly gameState = inject(GameStateService);
  private readonly locationService = inject(GameLocationService);
  private readonly userState = inject(UserStateService);
  private readonly compass = inject(CompassService);
  private readonly toastCtrl = inject(ToastController);
  private readonly translate = inject(TranslateService);
  private readonly tour = inject(TourService);

  /** Device compass heading (degrees clockwise from north); rotates the self arrow. */
  readonly heading = this.compass.heading;

  /** The authenticated user's internal id (User.Id), same identity source as the lobby. */
  private currentUserId: string | null = null;

  readonly showSurroundingsWarning = signal(false);
  readonly warningAcknowledged = signal(false);

  /** Seconds left in the game, reseeded from every full resync and ticked down locally every second. */
  readonly secondsRemaining = signal<number | null>(null);
  readonly timeRemaining = computed(() =>
    this.formatDuration(this.secondsRemaining()),
  );

  /** True when the server has flagged the game as being in its final stage. */
  readonly isEndgame = signal(false);

  /**
   * Three-state threat level derived from remaining time and the endgame flag.
   * Drives [attr.data-threat] on ion-content so the shared HUD chrome escalates.
   *   normal   → signal green   (standard play)
   *   final    → caution amber  (endgame stage active)
   *   critical → hunter red     (≤60 seconds left)
   */
  readonly threatState = computed<ThreatState>(() =>
    computeThreatState(this.secondsRemaining(), this.isEndgame()),
  );

  /**
   * Status pill label — reflects the current threat phase so the label
   * escalates alongside the color change.
   *   normal   → 'GAME_PROGRESS.STATUS_LIVE'
   *   final    → 'GAME_PROGRESS.STATUS_ENDGAME'
   *   critical → 'GAME_PROGRESS.STATUS_CRITICAL'
   */
  readonly statusPillLabel = computed(() => {
    switch (this.threatState()) {
      case 'critical':
        return 'GAME_PROGRESS.STATUS_CRITICAL';
      case 'final':
        return 'GAME_PROGRESS.STATUS_ENDGAME';
      default:
        return 'GAME_PROGRESS.STATUS_LIVE';
    }
  });

  /**
   * True when the hunter is effectively on top of a prey (distance < 30 m).
   * Used to add a visual emphasis to the DISTANCE HUD cell. Falls back to false
   * when no prey is in range (nearestDistance() is '--').
   */
  readonly onTarget = computed(() => {
    const raw = this.nearestDistance();
    if (raw === '--') return false;
    const metres = parseInt(raw, 10);
    return !isNaN(metres) && metres < 30;
  });

  /** Active preys, derived live from the shared game state — never waits for a resync. */
  readonly preysLeft = computed(() => {
    const game = this.gameState.state()?.game;
    return game ? countActivePreys(game) : 0;
  });

  /** This player's own penalty flag (e.g. for leaving the playfield), read live. */
  readonly hasActivePenalty = computed(() => {
    const game = this.gameState.state()?.game;
    return game?.participants.find((p) => p.userId === this.currentUserId)?.hasActivePenalty ?? false;
  });

  readonly nearestDistance = signal<string>('--');
  /** Collapsed by default: only game time + next-ping show until the HUD is tapped. */
  readonly hudExpanded = signal(false);

  /** The collapsed time/HUD bar and the tag button — anchors for the first-time tour. */
  private readonly hudBarRef = viewChild<ElementRef<HTMLElement>>('hudBar');
  private readonly tagFabRef = viewChild<ElementRef<HTMLElement>>('tagFab');

  /** True while the one-time hunter tour is showing. */
  readonly tourActive = signal(false);

  /** Two tour steps: the time bar (tap to expand/collapse), then the tag button. */
  readonly tourSteps = computed<TourStep[]>(() => [
    {
      target: this.hudBarRef()?.nativeElement ?? null,
      titleKey: 'GAME_TOUR.TIME_BAR_TITLE',
      bodyKey: 'GAME_TOUR.TIME_BAR_BODY',
    },
    {
      target: this.tagFabRef()?.nativeElement ?? null,
      titleKey: 'GAME_TOUR.TAG_TITLE',
      bodyKey: 'GAME_TOUR.TAG_BODY',
    },
  ]);

  /** Guard so the tour is evaluated only once per page lifetime. */
  private tourResolved = false;

  /** Epoch ms at which the nearest prey's location was last received, or null when none. */
  readonly nearestUpdatedAt = signal<number | null>(null);
  /**
   * Whole seconds since the nearest prey's location was last received — the "Ns ago"
   * descriptor under the distance readout. null when no prey is in range. Recomputed
   * each second via nowTick.
   */
  readonly measuredAgo = computed<number | null>(() => {
    const t = this.nearestUpdatedAt();
    if (t === null) return null;
    return Math.max(0, Math.floor((this.nowTick() - t) / 1000));
  });
  readonly gpsAlert = signal<string | null>(null);
  readonly pingCountdown = signal(30);
  /** Latest known full ping interval (seconds) used as the NEXT UPDATE bar denominator. */
  currentPingInterval = 30;
  /** True while the game is in the Started state (armed by the owner but not yet committed by the sweep). */
  readonly waitingForStart = computed(() => this.gameState.state()?.game.status === 'Started');
  /** Fixed bar duration (seconds) used as MAX when the player is under a boundary penalty. */
  private readonly PENALTY_BAR_SECONDS = 30;
  /** NEXT UPDATE bar fill percentage: countdown / currentPingInterval, clamped 0–100. */
  readonly pingBarWidth = computed(() => {
    const pct = (this.pingCountdown() / (this.currentPingInterval || 30)) * 100;
    return Math.min(100, Math.max(0, isNaN(pct) ? 0 : pct));
  });
  readonly showTagModal = signal(false);
  /** Candidates fetched from the server when the tag drawer opens. */
  readonly tagCandidates = signal<TagCandidateDto[]>([]);
  readonly tagCandidatesLoading = signal(false);
  readonly tagCandidatesError = signal(false);
  readonly tagInFlight = signal(false);
  /** The prey selected in the list, awaiting confirmation; null shows the list step. */
  readonly pendingTag = signal<TagCandidateDto | null>(null);
  /** Whether the hunter ticked "I really tagged this person" — gates the confirm button. */
  readonly tagAcknowledged = signal(false);
  /** Drives the separate confirmation popup; open whenever a target is pending. */
  readonly tagConfirmOpen = computed(() => this.pendingTag() !== null);
  /** ISO timestamp at which the hunter may move; drives the countdown overlay. */
  readonly hunterMayMoveAt = signal<string | null>(null);
  /** Ticked every second by the duration timer so delay gating flips without a resync. */
  private readonly nowTick = signal(Date.now());
  /** True while the hunter head-start delay is still running — gates the Tag button. */
  readonly hunterDelayActive = computed(() => {
    const at = this.hunterMayMoveAt();
    return at !== null && new Date(at).getTime() > this.nowTick();
  });
  /** True when background location reporting could not be (re)started for this game. */
  readonly trackingInactive = signal(false);
  /** Live tracking state from the singleton service (true while broadcasting). */
  readonly isTracking = this.locationService.isTracking;
  /** Why background location reporting can't get a fix (denied/unavailable), or null. */
  readonly gpsError = this.locationService.gpsError;
  /** True when location posts have been failing — position isn't reaching the server. */
  readonly reportingDegraded = this.locationService.reportingDegraded;

  private gameId!: string;
  private map!: L.Map;
  private selfMarker: L.Marker | null = null;
  /** Inner element of the self arrow icon whose CSS rotation we drive from the compass. */
  private selfArrowEl: HTMLElement | null = null;
  /** Continuously accumulated rotation so the arrow always turns the short way round. */
  private renderedHeading = 0;
  private playfieldPolygon: L.Polygon | null = null;
  private preyMarkers = new Map<string, L.CircleMarker>();
  /** Epoch ms when each prey's location was last received, keyed by userId. */
  private preyLastUpdate = new Map<string, number>();
  private durationTimer: ReturnType<typeof setInterval> | null = null;
  private pingIntervalTimer: ReturnType<typeof setInterval> | null = null;
  private watchId: string | null = null;
  private autoFollow = true;
  private selfLatLng: L.LatLng | null = null;
  /** Detects a fresh full resync (vs. an incremental delta) by object-identity change. */
  private lastSeenStatus: GameStatusDto | null = null;
  private lastPenalised: boolean | null = null;
  /** Guard: prevent duplicate game-ended handling */
  private gameEndedHandled = false;

  constructor() {
    // Spin the self arrow to match the compass whenever a new heading arrives.
    effect(() => {
      const h = this.heading();
      if (h !== null) this.applyHeading(h);
    });

    // Offer the one-time hunter tour once the live view is interactive AND the tag button is
    // usable: warning dismissed, game in progress, and the head-start delay has cleared so both
    // the time bar and the (now-enabled) tag button are on screen to highlight.
    effect(() => {
      const ready =
        !this.showSurroundingsWarning() &&
        !this.waitingForStart() &&
        !this.hunterDelayActive() &&
        this.secondsRemaining() !== null;
      if (ready) void this.maybeStartTour();
    });

    // The single source of truth for everything gameplay-related: reseed the locally-ticked
    // clock/endgame/hunter-delay values on every fresh full resync (status object identity
    // change), and keep the NEXT UPDATE bar's cadence in step with the latest known interval
    // and this player's own penalty state.
    effect(() => {
      const status = this.gameState.state()?.status ?? null;
      const penalised = this.hasActivePenalty();

      const statusChanged = status !== this.lastSeenStatus;
      const penalisedChanged = penalised !== this.lastPenalised;
      this.lastSeenStatus = status;
      this.lastPenalised = penalised;

      if (statusChanged && status) {
        this.secondsRemaining.set(status.gameDurationLeft);
        this.isEndgame.set(status.isEndgame ?? false);
        this.hunterMayMoveAt.set(status.hunterMayMoveAt ?? null);
        this.drawPlayfield(status.playfieldCoordinates);
      }

      if (status && (statusChanged || penalisedChanged)) {
        this.syncPingCadence(status, penalised);
      }
    });

    // Render every prey visible to the hunter, sourced from the shared state — never a
    // separate copy. Skips self; the GPS watch draws the hunter's own marker.
    effect(() => {
      const state = this.gameState.state();
      if (!state || !this.currentUserId) return;
      for (const p of this.gameState.visibleParticipants(this.currentUserId)) {
        if (!p.lastKnownLocation) continue;
        this.upsertPreyBlip(p.userId, p.lastKnownLocation.latitude, p.lastKnownLocation.longitude, p.state);
      }
      this.updateNearestDistance();
    });

    // The game ended — hand off to the outcome screen exactly once.
    effect(() => {
      const game = this.gameState.state()?.game;
      if (game?.status === 'Completed') {
        this.handleGameEnded(game.outcome, countActivePreys(game));
      }
    });

    // A terminal authorization failure — nothing left to reconcile.
    effect(() => {
      if (this.gameState.unavailable()) {
        this.handleGameEnded();
      }
    });
  }

  /** Show the hunter tour once if it has not been seen before. */
  private async maybeStartTour(): Promise<void> {
    if (this.tourResolved) return;
    this.tourResolved = true;
    if (await this.tour.hasSeen('hunter')) return;
    // Ensure the HUD is collapsed so the bar anchor exists and the tap hint matches what's shown.
    this.hudExpanded.set(false);
    this.tourActive.set(true);
  }

  /** Tour finished (completed or skipped): hide it and record that hunter has now seen it. */
  onTourCompleted(): void {
    this.tourActive.set(false);
    void this.tour.markSeen('hunter');
  }

  dismissSurroundingsWarning(): void {
    localStorage.setItem('surroundings-warning', this.gameId);
    this.showSurroundingsWarning.set(false);
  }

  private checkSurroundingsWarning(): void {
    const seen = localStorage.getItem('surroundings-warning');
    if (seen !== this.gameId) {
      this.showSurroundingsWarning.set(true);
    }
  }

  async ngOnInit(): Promise<void> {
    this.gameId = this.route.snapshot.paramMap.get('id') ?? '';

    this.checkSurroundingsWarning();
    this.currentUserId = this.userState.profile()?.userId ?? null;

    this.initMap();
    void this.startGps();
    void this.compass.start();

    // The single source of truth: loads the full snapshot (idempotent if the lobby already
    // started it for this game) and keeps the one real-time connection alive.
    await this.gameState.start(this.gameId);
    await this.ensureTracking();
    this.startDurationTimer();
  }

  /**
   * Health-check the background tracking session every time the page is shown. The
   * hunter must keep reporting position (preys see hunter distance), so the singleton
   * GameLocationService is (re)started here if it is not already tracking this game.
   */
  async ionViewWillEnter(): Promise<void> {
    await this.ensureTracking();
  }

  ngOnDestroy(): void {
    this.clearPingInterval();
    this.clearDurationTimer();
    this.compass.stop();
    // NOTE: intentionally do NOT stop location tracking or the shared GameStateService here —
    // both must keep running while the game is in progress even if the player leaves this page.
    if (this.watchId !== null) {
      Geolocation.clearWatch({ id: this.watchId });
      this.watchId = null;
    }
    if (this.map) {
      this.map.remove();
    }
  }

  private async ensureTracking(): Promise<void> {
    if (this.isTracking()) {
      this.trackingInactive.set(false);
      return;
    }

    // 1. Recover from a persisted session (e.g. after the OS killed the app).
    const storedId = (await Preferences.get({ key: 'game.tracking.gameId' }))
      .value;
    const storedEnd = (
      await Preferences.get({ key: 'game.tracking.gameEndTime' })
    ).value;
    if (storedId === this.gameId && storedEnd) {
      const end = new Date(storedEnd);
      if (end.getTime() > Date.now()) {
        await this.locationService.start(this.gameId, end);
        this.trackingInactive.set(false);
        return;
      }
    }

    // 2. No (valid) stored context — derive the end time from the live game (the shared
    // state if already loaded, else a one-off REST fetch as a cold-start fallback).
    try {
      const game = this.gameState.state()?.game ?? (await this.gamesService.getGame(this.gameId));
      const end = this.deriveGameEnd(game);
      if (end && end.getTime() > Date.now()) {
        await this.locationService.start(this.gameId, end);
        this.trackingInactive.set(false);
        return;
      }
      // Game record reached, but we declined to start. Log why so a live "Location
      // reporting inactive" banner on an in-progress game can be diagnosed.
      console.warn('[ensureTracking] not starting tracking', {
        status: game.status,
        startedAt: game.startedAt,
        endsAt: game.endsAt,
        derivedEnd: end?.toISOString() ?? null,
        now: new Date().toISOString(),
      });
    } catch (err) {
      // getGame failed entirely (network / 5xx / deserialization) — fall through to the warning.
      console.warn('[ensureTracking] getGame failed', err);
    }

    // 3. Nothing to resume — surface a non-blocking warning.
    this.trackingInactive.set(true);
  }

  /**
   * Resolve the game's end instant, trusting the server-authoritative `endsAt` and only
   * falling back to recomputing from `startedAt + gameDuration` when the server did not
   * supply it (older payloads). Returns null when neither is available.
   */
  private deriveGameEnd(game: GameDto): Date | null {
    if (game.endsAt) {
      return new Date(game.endsAt);
    }
    if (game.startedAt) {
      return new Date(
        new Date(game.startedAt).getTime() +
          game.configuration.gameDuration * 60_000,
      );
    }
    return null;
  }

  toggleHud(): void {
    this.hudExpanded.update((v) => !v);
  }

  recenter(): void {
    this.autoFollow = true;
    if (this.selfLatLng) {
      this.map.setView(this.selfLatLng, this.map.getZoom());
    }
  }

  async openTagModal(): Promise<void> {
    this.showTagModal.set(true);
    this.tagCandidatesError.set(false);
    await this.fetchTagCandidates();
  }

  closeTagModal(): void {
    // NOTE: do NOT reset the pending confirmation here. Selecting a target
    // dismisses this sheet (firing didDismiss → closeTagModal) at the same moment
    // it opens the confirmation popup; clearing pendingTag here would close that
    // popup immediately. The sheet only ever has the list, so nothing to reset.
    this.showTagModal.set(false);
  }

  async retryTagCandidates(): Promise<void> {
    this.tagCandidatesError.set(false);
    await this.fetchTagCandidates();
  }

  private async fetchTagCandidates(): Promise<void> {
    this.tagCandidatesLoading.set(true);
    try {
      const result = await this.gamesService.getTagCandidates(this.gameId);
      this.tagCandidates.set(result.candidates);
    } catch {
      this.tagCandidatesError.set(true);
    } finally {
      this.tagCandidatesLoading.set(false);
    }
  }

  /** Close the selection sheet and open the confirmation popup for the chosen target. */
  selectTagTarget(prey: TagCandidateDto): void {
    this.showTagModal.set(false);
    this.pendingTag.set(prey);
    this.tagAcknowledged.set(false);
  }

  /** Back out of the confirmation screen to the prey list. */
  cancelTagConfirmation(): void {
    this.resetTagConfirmation();
  }

  private resetTagConfirmation(): void {
    this.pendingTag.set(null);
    this.tagAcknowledged.set(false);
  }

  /** Final step: send the tag request for the acknowledged target. */
  async confirmTag(): Promise<void> {
    const prey = this.pendingTag();
    if (!prey || !this.tagAcknowledged() || this.tagInFlight()) return;
    this.tagInFlight.set(true);
    try {
      await this.gamesService.tagPlayer(this.gameId, prey.userId);
      this.resetTagConfirmation();
    } catch (err) {
      const isOutOfRange =
        err instanceof HttpErrorResponse && err.status === 409;
      const messageKey = isOutOfRange
        ? 'TAG_MODAL.OUT_OF_RANGE'
        : 'TAG_MODAL.TAG_FAILED';
      const toast = await this.toastCtrl.create({
        message: this.translate.instant(messageKey),
        duration: 4000,
        color: 'danger',
        position: 'bottom',
      });
      await toast.present();
      if (isOutOfRange) {
        // Prey moved away — refresh the candidate list and return to the drawer.
        this.resetTagConfirmation();
        this.showTagModal.set(true);
        await this.fetchTagCandidates();
      }
    } finally {
      this.tagInFlight.set(false);
    }
  }

  /** Pull-to-refresh: force an immediate full resync of the shared game state. */
  async handleRefresh(event: RefresherCustomEvent): Promise<void> {
    await this.gameState.refreshNow();
    await event.target.complete();
  }

  private initMap(): void {
    this.map = L.map('map', { zoomControl: false, attributionControl: false });
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(
      this.map,
    );
    this.map.setView([52.0, 5.0], 15);

    // Disable auto-follow when user pans manually
    this.map.on('dragstart', () => {
      this.autoFollow = false;
    });

    // Ionic sizes ion-content after this component initialises; Leaflet starts at
    // 0×0 and won't load tiles until it is told to re-measure once on screen.
    setTimeout(() => this.map.invalidateSize(), 150);
  }

  /**
   * Watch position via the Capacitor Geolocation plugin (not navigator.geolocation,
   * which bypasses the native permission flow and silently fails on device) to keep
   * the on-screen hunter marker centred and the nearest-prey distance current.
   */
  private async startGps(): Promise<void> {
    try {
      await Geolocation.requestPermissions();
    } catch {
      // Permissions API may be unavailable (e.g. web) — try watching regardless.
    }

    try {
      this.watchId = await Geolocation.watchPosition(
        { enableHighAccuracy: true, maximumAge: 5000 },
        (position, err) => {
          if (err || !position) {
            this.gpsAlert.set(
              this.translate.instant('GAME_PROGRESS.GPS_SIGNAL_LOST'),
            );
            if (this.selfMarker) {
              this.selfMarker.remove();
              this.selfMarker = null;
              this.selfArrowEl = null;
            }
            return;
          }
          this.gpsAlert.set(null);
          const { latitude, longitude } = position.coords;
          const latlng = L.latLng(latitude, longitude);
          this.selfLatLng = latlng;

          if (this.selfMarker) {
            this.selfMarker.setLatLng(latlng);
          } else {
            this.selfMarker = L.marker(latlng, {
              icon: this.buildSelfArrowIcon(),
              interactive: false,
              keyboard: false,
            }).addTo(this.map);
            this.captureSelfArrow();
          }
          if (this.autoFollow) {
            this.map.setView(latlng);
          }
          this.updateNearestDistance();
        },
      );
    } catch {
      this.gpsAlert.set('Signal lost. Find open sky.');
    }
  }

  /** Build the rotatable "you" navigation arrow used as the self marker icon. */
  private buildSelfArrowIcon(): L.DivIcon {
    return L.divIcon({
      className: 'self-arrow-marker',
      html:
        '<div class="self-arrow">' +
        '<svg viewBox="0 0 24 24" width="30" height="30">' +
        `<path d="M12 2 L20 21 L12 16 L4 21 Z" fill="${MAP_COLORS.SIGNAL_DEEP}" stroke="${MAP_COLORS.SIGNAL}" stroke-width="1" stroke-linejoin="round" /></svg></div>`,
      iconSize: [30, 30],
      iconAnchor: [15, 15],
    });
  }

  /** Grab the arrow's inner element after the marker mounts and orient it immediately. */
  private captureSelfArrow(): void {
    this.selfArrowEl =
      this.selfMarker?.getElement()?.querySelector('.self-arrow') ?? null;
    const h = this.heading();
    if (h !== null) this.applyHeading(h);
  }

  /**
   * Rotate the arrow to the given compass heading. The map is north-up, so the heading
   * (clockwise from north) is the rotation directly. We accumulate the angle so a sweep
   * across the 0°/360° seam turns the short way instead of spinning all the way round.
   */
  private applyHeading(heading: number): void {
    if (!this.selfArrowEl) return;
    const delta =
      ((((heading - this.renderedHeading) % 360) + 540) % 360) - 180;
    this.renderedHeading += delta;
    this.selfArrowEl.style.transform = `rotate(${this.renderedHeading}deg)`;
  }

  // -------------------------------------------------------------------------
  // HUD cadence
  // -------------------------------------------------------------------------

  /** Sync the NEXT UPDATE bar's cadence to the latest known interval and penalty regime. */
  private syncPingCadence(status: GameStatusDto, penalised: boolean): void {
    const barMax = penalised ? this.PENALTY_BAR_SECONDS : status.currentPingInterval || 30;
    const sync = penalised
      ? (status.nextPingDurationWithPenalty ?? barMax)
      : (status.nextPingDuration ?? barMax);

    this.currentPingInterval = barMax;
    if (!this.waitingForStart()) {
      this.startPingCountdown(sync > 0 ? sync : barMax, barMax);
    }
  }

  private drawPlayfield(
    coords: { latitude: number; longitude: number }[],
  ): void {
    if (this.playfieldPolygon) return;
    if (!coords.length) return;

    const latlngs = coords.map(
      (c) => [c.latitude, c.longitude] as L.LatLngExpression,
    );
    this.playfieldPolygon = L.polygon(latlngs, {
      color: MAP_COLORS.HUNTER,
      fillColor: MAP_COLORS.HUNTER,
      fillOpacity: 0.1,
      weight: 2,
    }).addTo(this.map);
    this.map.fitBounds(this.playfieldPolygon.getBounds());
  }

  private upsertPreyBlip(
    userId: string,
    lat: number,
    lng: number,
    state: string,
  ): void {
    const latlng: L.LatLngExpression = [lat, lng];
    const isInactive = state === 'Tagged' || state === 'Out';
    const options: L.CircleMarkerOptions = isInactive
      ? {
          radius: 6,
          color: MAP_COLORS.TAGGED,
          fillColor: MAP_COLORS.TAGGED,
          fillOpacity: 0.7,
          weight: 2,
        }
      : {
          radius: 6,
          color: MAP_COLORS.HUNTER,
          fillColor: MAP_COLORS.HUNTER,
          fillOpacity: 0.9,
          weight: 2,
        };

    const existing = this.preyMarkers.get(userId);
    if (existing) {
      // Stamp the receipt time only when the position actually moved, so the
      // "Ns ago" descriptor reflects a genuinely fresh fix rather than every render.
      const prev = existing.getLatLng();
      if (prev.lat !== lat || prev.lng !== lng) {
        this.preyLastUpdate.set(userId, Date.now());
      }
      existing.setLatLng(latlng);
      existing.setStyle(options);
    } else {
      const marker = L.circleMarker(latlng, options).addTo(this.map);
      this.preyMarkers.set(userId, marker);
      this.preyLastUpdate.set(userId, Date.now());
    }
  }

  private updateNearestDistance(): void {
    if (!this.selfLatLng || this.preyMarkers.size === 0) {
      this.nearestDistance.set('--');
      this.nearestUpdatedAt.set(null);
      return;
    }
    const game = this.gameState.state()?.game;
    let minMetres = Infinity;
    let nearestUserId: string | null = null;
    for (const [userId, marker] of this.preyMarkers.entries()) {
      const state = game?.participants.find((p) => p.userId === userId)?.state ?? 'Active';
      if (state === 'Tagged' || state === 'Out') continue;
      const d = this.selfLatLng.distanceTo(marker.getLatLng());
      if (d < minMetres) {
        minMetres = d;
        nearestUserId = userId;
      }
    }
    if (nearestUserId === null) {
      this.nearestDistance.set('--');
      this.nearestUpdatedAt.set(null);
      return;
    }
    this.nearestDistance.set(`${Math.round(minMetres)}m`);
    this.nearestUpdatedAt.set(this.preyLastUpdate.get(nearestUserId) ?? null);
  }

  private formatDuration(seconds: number | null): string {
    if (seconds === null) return '--:--';
    const mins = Math.floor(seconds / 60)
      .toString()
      .padStart(2, '0');
    const secs = (seconds % 60).toString().padStart(2, '0');
    return `${mins}:${secs}`;
  }

  /** Tick the game-duration clock down once per second between server resyncs. */
  private startDurationTimer(): void {
    this.clearDurationTimer();
    this.durationTimer = setInterval(() => {
      this.nowTick.set(Date.now());
      const s = this.secondsRemaining();
      if (s === null) return;
      this.secondsRemaining.set(Math.max(0, s - 1));
    }, 1000);
  }

  private clearDurationTimer(): void {
    if (this.durationTimer !== null) {
      clearInterval(this.durationTimer);
      this.durationTimer = null;
    }
  }

  /**
   * Tick the next-update countdown down once per second until the next resync/penalty
   * change resyncs it. When the counter reaches 0 it rolls over to `max` for a clean full
   * sweep rather than sticking at 0.
   */
  private startPingCountdown(seconds: number, max: number): void {
    this.clearPingInterval();
    this.pingCountdown.set(seconds);
    this.pingIntervalTimer = setInterval(() => {
      const next = this.pingCountdown() - 1;
      this.pingCountdown.set(next >= 0 ? next : max);
    }, 1000);
  }

  private clearPingInterval(): void {
    if (this.pingIntervalTimer !== null) {
      clearInterval(this.pingIntervalTimer);
      this.pingIntervalTimer = null;
    }
  }

  /** Idempotent: safe to call from every effect that can observe the game ending. */
  private handleGameEnded(outcome?: string, survivorCount?: number): void {
    if (this.gameEndedHandled) return;
    this.gameEndedHandled = true;
    this.gameState.stop();
    void this.locationService.stop();
    // Hand the result to the debrief screen; it confirms against the server too, so
    // missing values here are non-fatal. null query params are dropped by the router.
    this.router.navigate(['/games', this.gameId, 'outcome'], {
      replaceUrl: true,
      queryParams: {
        role: 'hunter',
        outcome: outcome ?? null,
        survivors: survivorCount ?? null,
      },
    });
  }
}
