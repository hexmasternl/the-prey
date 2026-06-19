import {
  Component,
  computed,
  effect,
  inject,
  OnDestroy,
  OnInit,
  signal,
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
import { App } from '@capacitor/app';
import type { PluginListenerHandle } from '@capacitor/core';
import { Geolocation } from '@capacitor/geolocation';
import { Preferences } from '@capacitor/preferences';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  GameDto,
  GameParticipantStatusDto,
  GameStatusDto,
  GamesService,
  TagCandidateDto,
} from './games.service';
import { computeThreatState, ThreatState } from './threat-state';
import { MAP_COLORS } from '../shared/map-colors';
import {
  GameStreamService,
  PlayerLocationUpdatedPayload,
  PlayerStatusChangedPayload,
  ParticipantStatusChangedPayload,
  PlayerPenalizedPayload,
  GameEndedPayload,
} from './game-stream.service';
import { GameLocationService } from './game-location.service';
import { CompassService } from './compass.service';
import { HunterDelayOverlayComponent } from './hunter-delay-overlay.component';

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
  ],
})
export class GameHunterPage implements OnInit, OnDestroy, ViewWillEnter {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly streamService = inject(GameStreamService);
  private readonly locationService = inject(GameLocationService);
  private readonly compass = inject(CompassService);
  private readonly toastCtrl = inject(ToastController);
  private readonly translate = inject(TranslateService);

  /** Device compass heading (degrees clockwise from north); rotates the self arrow. */
  readonly heading = this.compass.heading;

  /** The authenticated user's Auth0 sub — populated from the first status poll. */
  private currentUserId: string | null = null;

  readonly showSurroundingsWarning = signal(false);
  readonly warningAcknowledged = signal(false);

  /** Seconds left in the game, resynced from the server each poll and ticked down locally every second. */
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
      case 'critical': return 'GAME_PROGRESS.STATUS_CRITICAL';
      case 'final':    return 'GAME_PROGRESS.STATUS_ENDGAME';
      default:         return 'GAME_PROGRESS.STATUS_LIVE';
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

  readonly preysLeft = signal(0);
  readonly hasActivePenalty = signal(false);
  readonly nearestDistance = signal<string>('--');
  /** Collapsed by default: only game time + next-ping show until the HUD is tapped. */
  readonly hudExpanded = signal(false);
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
  /** Server-supplied full ping interval (seconds) used as the NEXT UPDATE bar denominator. */
  currentPingInterval = 30;
  /** True while the game is in the Ready state (armed but not yet started by the sweep). */
  readonly waitingForStart = signal(false);
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
  /** ISO timestamp at which the hunter may move, from the status poll; drives the countdown overlay. */
  readonly hunterMayMoveAt = signal<string | null>(null);
  /** Ticked every second by the duration timer so delay gating flips without a poll. */
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
  /** Local participant state map keyed by userId */
  private participantStates = new Map<string, string>();
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private pingIntervalTimer: ReturnType<typeof setInterval> | null = null;
  private durationTimer: ReturnType<typeof setInterval> | null = null;
  private watchId: string | null = null;
  pollIntervalSeconds = 30;
  private autoFollow = true;
  private selfLatLng: L.LatLng | null = null;
  /** Guard: prevent duplicate game-ended handling */
  private gameEndedHandled = false;
  /** Capacitor App foreground/background listener — drives resume resync. */
  private resumeListener: PluginListenerHandle | null = null;

  constructor() {
    // Spin the self arrow to match the compass whenever a new heading arrives.
    effect(() => {
      const h = this.heading();
      if (h !== null) this.applyHeading(h);
    });
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

    this.initMap();
    void this.startGps();
    void this.compass.start();

    // Check if we're entering a game that is still in the Ready state (armed by the host
    // but not yet committed by the sweep). If so, show the waiting overlay immediately
    // and skip the ping countdown until InProgress arrives via stream.
    await this.checkReadyState();

    await this.pollStatus();
    this.startDurationTimer();
    this.connectStream();
    void this.registerResumeListener();
  }

  /**
   * Check whether the game is currently in the Ready state. If so, set waitingForStart
   * so the overlay is shown and the ping countdown is suppressed until InProgress arrives.
   */
  private async checkReadyState(): Promise<void> {
    try {
      const game = await this.gamesService.getGame(this.gameId);
      if (game.status === 'Ready') {
        this.waitingForStart.set(true);
      }
    } catch {
      // Non-fatal — pollStatus will handle any further issues.
    }
  }

  /**
   * Web PubSub delivers nothing while the app is backgrounded or the tab is hidden,
   * and a socket suspended in the background can be left silently half-open (its
   * `onclose` never fires, so the stream's own reconnect never kicks in). When the
   * app returns to the foreground we therefore both re-establish the realtime channel
   * and re-fetch the status to reconcile anything missed while away. The Capacitor App
   * plugin fires `appStateChange` on native resume and on web via document visibility.
   */
  private async registerResumeListener(): Promise<void> {
    this.resumeListener = await App.addListener(
      'appStateChange',
      ({ isActive }) => {
        if (isActive) {
          this.resyncOnResume();
        }
      },
    );
  }

  private resyncOnResume(): void {
    if (this.gameEndedHandled) return;
    // Reconnect the realtime channel (handles the silently-dead-socket case)…
    this.connectStream();
    // …and force an immediate status refresh to catch events missed while suspended.
    this.clearPoll();
    void this.pollStatus();
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
    this.clearPoll();
    this.clearPingInterval();
    this.clearDurationTimer();
    this.streamService.disconnect();
    this.compass.stop();
    void this.resumeListener?.remove();
    this.resumeListener = null;
    // NOTE: intentionally do NOT stop location tracking here — the service must keep
    // broadcasting while the game is in progress even if the player leaves this page.
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

    // 2. No (valid) stored context — derive the end time from the live game.
    try {
      const game = await this.gamesService.getGame(this.gameId);
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
      const isOutOfRange = err instanceof HttpErrorResponse && err.status === 409;
      const messageKey = isOutOfRange ? 'TAG_MODAL.OUT_OF_RANGE' : 'TAG_MODAL.TAG_FAILED';
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

  /** Pull-to-refresh: fetch the latest game status and apply it immediately. */
  async handleRefresh(event: RefresherCustomEvent): Promise<void> {
    try {
      const status = await this.gamesService.getGameStatus(this.gameId);
      this.applyStatus(status);
    } catch {
      // Silently ignore — the poll timer will retry shortly
    } finally {
      await event.target.complete();
    }
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
            this.gpsAlert.set(this.translate.instant('GAME_PROGRESS.GPS_SIGNAL_LOST'));
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
        '<path d="M12 2 L20 21 L12 16 L4 21 Z" /></svg></div>',
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
    const delta = ((heading - this.renderedHeading) % 360 + 540) % 360 - 180;
    this.renderedHeading += delta;
    this.selfArrowEl.style.transform = `rotate(${this.renderedHeading}deg)`;
  }

  private async pollStatus(): Promise<void> {
    try {
      const status = await this.gamesService.getGameStatus(this.gameId);
      // Capture the hunter's own userId from the first status poll
      if (!this.currentUserId && status.hunterUserId) {
        this.currentUserId = status.hunterUserId;
      }
      this.applyStatus(status);

      // A successful status poll means the game is InProgress (the status endpoint only
      // serves running games). If we entered during the Ready state, startedAt was null
      // and tracking never started — (re)start it now that the game is live. Idempotent:
      // ensureTracking short-circuits once the location service is broadcasting.
      void this.ensureTracking();

      // Choose bar regime: penalised players run on a fixed 30-second cadence;
      // everyone else uses the server-supplied interval.
      // applyStatus() (called above) has already refreshed hasActivePenalty.
      const penalised = this.hasActivePenalty();
      const barMax = penalised ? this.PENALTY_BAR_SECONDS : (status.currentPingInterval || 30);
      const sync   = penalised
        ? (status.nextPingDurationWithPenalty ?? barMax)
        : (status.nextPingDuration           ?? barMax);

      this.currentPingInterval  = barMax;  // NEXT UPDATE bar denominator
      this.pollIntervalSeconds  = barMax;  // steady poll cadence, decoupled from boundary time

      // Only start the ping countdown when the game is actually running.
      if (!this.waitingForStart()) {
        // A sync of 0 means a broadcast is imminent — start a fresh full sweep.
        this.startPingCountdown(sync > 0 ? sync : barMax, barMax);
      }
      this.pollTimer = setTimeout(
        () => this.pollStatus(),
        this.pollIntervalSeconds * 1000,
      );
    } catch {
      // The status endpoint only serves in-progress games, so an error here may mean the
      // game ended while we were backgrounded/disconnected and we missed `game-ended`.
      // Confirm against the full game record before falling back to a plain retry.
      if (await this.checkGameEndedOnServer()) return;
      this.pollTimer = setTimeout(() => this.pollStatus(), 30_000);
    }
  }

  /**
   * Authoritative fallback for a missed `game-ended` event: fetch the full game record and,
   * if it has completed, hand off to the outcome screen. Returns true when the game has ended
   * (navigation triggered), false otherwise. Idempotent via `handleGameEnded`'s guard.
   * Also sets `waitingForStart` if the game is still in the Ready state (status poll failed
   * because the game is not yet InProgress).
   */
  private async checkGameEndedOnServer(): Promise<boolean> {
    try {
      const game = await this.gamesService.getGame(this.gameId);
      if (game.status === 'Completed') {
        const survivorCount = game.participants.filter(
          (p) =>
            p.userId !== game.hunterUserId &&
            (p.state === 'Active' || p.state === 'Passive'),
        ).length;
        this.handleGameEnded({
          gameId: this.gameId,
          outcome: game.outcome,
          survivorCount,
        });
        return true;
      }
      if (game.status === 'Ready') {
        // The game was armed but the sweep hasn't promoted it yet. Show the waiting overlay.
        this.waitingForStart.set(true);
      }
    } catch {
      // Game record unreachable — let the caller retry.
    }
    return false;
  }

  private applyStatus(status: GameStatusDto): void {
    this.secondsRemaining.set(status.gameDurationLeft);
    this.isEndgame.set(status.isEndgame ?? false);
    this.preysLeft.set(status.preysLeft);
    this.hunterMayMoveAt.set(status.hunterMayMoveAt ?? null);

    const hunter =
      status.participants.find((p) => p.userId === status.hunterUserId) ?? null;
    const preys = status.participants.filter(
      (p) => p.userId !== status.hunterUserId,
    );

    const me = hunter;
    this.hasActivePenalty.set(me?.hasActivePenalty ?? false);

    // Seed the local state cache from the status snapshot
    for (const prey of preys) {
      this.participantStates.set(prey.userId, prey.state);
    }

    this.drawPlayfield(status.playfieldCoordinates);
    this.updatePreyBlips(preys);
    this.updateNearestDistance();
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

  private updatePreyBlips(preys: GameParticipantStatusDto[]): void {
    for (const prey of preys) {
      if (!prey.lastKnownLocation) continue;
      this.upsertPreyBlip(
        prey.userId,
        prey.lastKnownLocation.latitude,
        prey.lastKnownLocation.longitude,
        prey.state,
      );
    }
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
      // "Ns ago" descriptor reflects a genuinely fresh fix rather than every poll.
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
    let minMetres = Infinity;
    let nearestUserId: string | null = null;
    for (const [userId, marker] of this.preyMarkers.entries()) {
      const state = this.participantStates.get(userId) ?? 'Active';
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
   * Tick the next-update countdown down once per second until the next poll resyncs it.
   * When the counter reaches 0 it rolls over to `max` for a clean full sweep rather than
   * sticking at 0 until the poll fires.
   */
  private startPingCountdown(seconds: number, max: number): void {
    this.clearPingInterval();
    this.pingCountdown.set(seconds);
    this.pingIntervalTimer = setInterval(() => {
      const next = this.pingCountdown() - 1;
      this.pingCountdown.set(next >= 0 ? next : max);
    }, 1000);
  }

  private connectStream(): void {
    this.streamService.connect(this.gameId);

    // WebSocket reconnected after a drop — refresh the status snapshot immediately
    // instead of waiting for the next poll tick to catch any missed events.
    this.streamService.onReconnected(() => {
      this.clearPoll();
      void this.pollStatus();
    });

    // `player-location-updated` replaces the old `participant-located` event.
    // Arrives ~every 30 s from the sweep; updates the prey blip on the map. Our own
    // location is drawn in green by the GPS watch, so never plot the hunter's last-known
    // location as a (red) prey blip.
    this.streamService.on<PlayerLocationUpdatedPayload>(
      'player-location-updated',
      (payload) => {
        if (payload.userId === this.currentUserId) return;
        const state =
          payload.participantState ??
          this.participantStates.get(payload.userId) ??
          'Active';
        this.participantStates.set(payload.userId, state);
        this.upsertPreyBlip(
          payload.userId,
          payload.latitude,
          payload.longitude,
          state,
        );
        this.updateNearestDistance();
      },
    );

    // Status changes arrive from both event names — treat them identically.
    const onStatusChanged = (userId: string, newState: string): void => {
      this.participantStates.set(userId, newState);
      const marker = this.preyMarkers.get(userId);
      if (marker) {
        const isInactive = newState === 'Tagged' || newState === 'Out';
        marker.setStyle(
          isInactive
            ? { color: MAP_COLORS.TAGGED, fillColor: MAP_COLORS.TAGGED, fillOpacity: 0.7 }
            : { color: MAP_COLORS.HUNTER, fillColor: MAP_COLORS.HUNTER, fillOpacity: 0.9 },
        );
      }
      const activeCount = [...this.participantStates.values()].filter(
        (s) => s === 'Active' || s === 'Passive',
      ).length;
      this.preysLeft.set(activeCount);
      this.updateNearestDistance();
    };

    this.streamService.on<PlayerStatusChangedPayload>(
      'player-status-changed',
      (payload) => {
        onStatusChanged(payload.userId, payload.newState);
      },
    );

    this.streamService.on<ParticipantStatusChangedPayload>(
      'participant-status-changed',
      (payload) => {
        onStatusChanged(payload.participantId, payload.newState);
      },
    );

    // Hunter penalty (e.g. for leaving the playfield) — switch to the 30-second penalty
    // regime immediately so the bar reflects the new cadence without waiting for the next poll.
    this.streamService.on<PlayerPenalizedPayload>(
      'player-penalized',
      (payload) => {
        if (payload.userId === this.currentUserId) {
          this.hasActivePenalty.set(true);
          this.currentPingInterval = this.PENALTY_BAR_SECONDS;
          this.pollIntervalSeconds  = this.PENALTY_BAR_SECONDS;
          this.startPingCountdown(this.PENALTY_BAR_SECONDS, this.PENALTY_BAR_SECONDS);
        }
      },
    );

    this.streamService.on('state-changed', () => {
      this.handleStateChanged();
    });

    this.streamService.on<GameEndedPayload>('game-ended', (payload) => {
      this.handleGameEnded(payload);
    });
  }

  /**
   * Handle a game-state transition broadcast from the stream. When the game moves from
   * Ready to InProgress, remove the waiting overlay, seed the ping countdown, and let
   * normal gameplay begin. The next status poll will supply the full HUD values.
   */
  private handleStateChanged(): void {
    if (this.waitingForStart()) {
      // Trigger an immediate status poll so the InProgress values (hunterMayMoveAt etc.)
      // are applied without waiting for the next scheduled tick.
      this.clearPoll();
      void this.pollStatusForInProgress();
    }
  }

  private async pollStatusForInProgress(): Promise<void> {
    try {
      const status = await this.gamesService.getGameStatus(this.gameId);
      if (!this.currentUserId && status.hunterUserId) {
        this.currentUserId = status.hunterUserId;
      }
      this.applyStatus(status);
      // Game is now InProgress — lift the waiting overlay and start the countdown.
      this.waitingForStart.set(false);
      // The game just went live; start broadcasting location now (we entered during Ready,
      // when startedAt was null and ensureTracking could not start). Idempotent.
      void this.ensureTracking();

      // Apply the same regime logic as the main pollStatus path.
      // applyStatus() above has already refreshed hasActivePenalty.
      const penalised = this.hasActivePenalty();
      const barMax = penalised ? this.PENALTY_BAR_SECONDS : (status.currentPingInterval || 30);
      const sync   = penalised
        ? (status.nextPingDurationWithPenalty ?? barMax)
        : (status.nextPingDuration           ?? barMax);

      this.currentPingInterval = barMax;
      this.pollIntervalSeconds = barMax;
      this.startPingCountdown(sync > 0 ? sync : barMax, barMax);
      this.pollTimer = setTimeout(() => this.pollStatus(), this.pollIntervalSeconds * 1_000);
    } catch {
      // Status endpoint not yet serving (game still transitioning) — retry shortly.
      this.pollTimer = setTimeout(() => this.pollStatus(), 5_000);
    }
  }

  /** Idempotent: safe to call from both Web PubSub events and the poll path. */
  private handleGameEnded(payload?: GameEndedPayload): void {
    if (this.gameEndedHandled) return;
    this.gameEndedHandled = true;
    this.clearPoll();
    this.streamService.disconnect();
    void this.locationService.stop();
    // Hand the result to the debrief screen; it confirms against the server too, so
    // missing values here are non-fatal. null query params are dropped by the router.
    this.router.navigate(['/games', this.gameId, 'outcome'], {
      replaceUrl: true,
      queryParams: {
        role: 'hunter',
        outcome: payload?.outcome ?? null,
        survivors: payload?.survivorCount ?? null,
      },
    });
  }

  private clearPoll(): void {
    if (this.pollTimer !== null) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private clearPingInterval(): void {
    if (this.pingIntervalTimer !== null) {
      clearInterval(this.pingIntervalTimer);
      this.pingIntervalTimer = null;
    }
  }
}
