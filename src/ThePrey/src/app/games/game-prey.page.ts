import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
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
  IonRefresher,
  IonRefresherContent,
  RefresherCustomEvent,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import * as L from 'leaflet';
import { App } from '@capacitor/app';
import type { PluginListenerHandle } from '@capacitor/core';
import { Geolocation } from '@capacitor/geolocation';
import { Preferences } from '@capacitor/preferences';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { GameDto, GameParticipantStatusDto, GameStatusDto, GamesService } from './games.service';
import { computeThreatState, ThreatState } from './threat-state';
import { MAP_COLORS } from '../shared/map-colors';
import { GameStreamService, PlayerLocationUpdatedPayload, PlayerStatusChangedPayload, ParticipantStatusChangedPayload, PlayerPenalizedPayload, GameEndedPayload } from './game-stream.service';
import { GameLocationService } from './game-location.service';
import { CompassService } from './compass.service';
import { HunterDelayOverlayComponent } from './hunter-delay-overlay.component';
import { UserStateService } from '../users/user-state.service';

@Component({
  selector: 'app-game-prey',
  templateUrl: 'game-prey.page.html',
  styleUrls: ['game-prey.page.scss'],
  imports: [
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
    IonRefresher,
    IonRefresherContent,
    TranslatePipe,
    HunterDelayOverlayComponent,
  ],
})
export class GamePreyPage implements OnInit, OnDestroy, ViewWillEnter {
  private readonly route          = inject(ActivatedRoute);
  private readonly router         = inject(Router);
  private readonly gamesService   = inject(GamesService);
  private readonly streamService  = inject(GameStreamService);
  private readonly locationService = inject(GameLocationService);
  private readonly userState      = inject(UserStateService);
  private readonly compass        = inject(CompassService);
  private readonly translate      = inject(TranslateService);

  /** Device compass heading (degrees clockwise from north); rotates the self arrow. */
  readonly heading = this.compass.heading;

  readonly showSurroundingsWarning = signal(false);
  readonly warningAcknowledged = signal(false);

  /** Seconds left in the game, resynced from the server each poll and ticked down locally every second. */
  readonly secondsRemaining = signal<number | null>(null);
  readonly timeRemaining   = computed(() => this.formatDuration(this.secondsRemaining()));

  /** True when the server has flagged the game as being in its final stage. */
  readonly isEndgame = signal(false);

  /**
   * Three-state threat level derived from remaining time and the endgame flag.
   * Drives [attr.data-threat] on ion-content so the shared HUD chrome escalates.
   *   normal   → signal green   (standard play)
   *   final    → caution amber  (endgame stage active)
   *   critical → hunter red     (≤60 seconds left)
   *
   * NOTE: Proximity-based escalation (hunter-is-near) is intentionally absent.
   * The prey client does NOT receive hunter distance in real-time — the status
   * poll only supplies secondsRemaining and isEndgame. A proximity-driven
   * escalation path requires the backend to push hunter distance to prey players,
   * which is a separate, deferred feature.
   */
  readonly threatState = computed<ThreatState>(() =>
    computeThreatState(this.secondsRemaining(), this.isEndgame()),
  );

  /**
   * Status pill label for the prey status bar. Reflects the player's in-game
   * state (hidden/active, spectating) and the threat phase.
   *   active + normal   → 'GAME_PROGRESS.STATUS_LIVE'
   *   active + final    → 'GAME_PROGRESS.STATUS_ENDGAME'
   *   active + critical → 'GAME_PROGRESS.STATUS_CRITICAL'
   *   spectating (tagged/out) → 'GAME_PROGRESS.STATUS_SPECTATING'
   */
  readonly statusPillLabel = computed(() => {
    if (this.outReason() !== null) return 'GAME_PROGRESS.STATUS_SPECTATING';
    switch (this.threatState()) {
      case 'critical': return 'GAME_PROGRESS.STATUS_CRITICAL';
      case 'final':    return 'GAME_PROGRESS.STATUS_ENDGAME';
      default:         return 'GAME_PROGRESS.STATUS_LIVE';
    }
  });

  readonly preysLeft       = signal(0);
  readonly hasActivePenalty = signal(false);
  readonly gpsAlert        = signal<string | null>(null);
  /**
   * Distance to the hunter, computed locally from our own GPS fix and the hunter's
   * last-known blip position ('--' until both are known). The prey is not sent hunter
   * distance by the server, so this is derived client-side and is only as fresh as the
   * hunter's last broadcast — hence the companion "Ns ago" descriptor below.
   */
  readonly hunterDistance  = signal<string>('--');
  /** Epoch ms at which the hunter's location was last received, or null when unknown. */
  readonly hunterUpdatedAt = signal<number | null>(null);
  /** Ticked every second by the duration timer so measuredAgo recomputes live. */
  private readonly nowTick = signal(Date.now());
  /** Whole seconds since the hunter's location was last received — the "Ns ago" descriptor. */
  readonly measuredAgo = computed<number | null>(() => {
    const t = this.hunterUpdatedAt();
    if (t === null) return null;
    return Math.max(0, Math.floor((this.nowTick() - t) / 1000));
  });
  /**
   * Set once this player is tagged or ruled out while the game is still running. Drives
   * the spectator overlay; the player stays connected and keeps receiving updates until
   * the game itself ends (`game-ended`), at which point everyone lands on the outcome screen.
   */
  readonly outReason       = signal<'tagged' | 'out' | null>(null);
  /** Seconds until the next status poll, ticked down every second for the HUD. */
  readonly pingCountdown   = signal(30);
  /** Server-supplied full ping interval (seconds) used as the NEXT UPDATE bar denominator. */
  currentPingInterval = 30;
  /** True while the game is in the Ready state (armed but not yet started by the sweep). */
  readonly waitingForStart = signal(false);
  /** NEXT UPDATE bar fill percentage: countdown / currentPingInterval, clamped 0–100. */
  readonly pingBarWidth = computed(() => {
    const pct = (this.pingCountdown() / (this.currentPingInterval || 30)) * 100;
    return Math.min(100, Math.max(0, isNaN(pct) ? 0 : pct));
  });
  /** Collapsed by default: only the remaining game time shows until the HUD is tapped. */
  readonly hudExpanded     = signal(false);
  /** ISO timestamp at which the hunter may move, from the status poll; drives the countdown overlay. */
  readonly hunterMayMoveAt = signal<string | null>(null);
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
  private playerMarker: L.Marker | null = null;
  /** Inner element of the self arrow icon whose CSS rotation we drive from the compass. */
  private playerArrowEl: HTMLElement | null = null;
  /** Continuously accumulated rotation so the arrow always turns the short way round. */
  private renderedHeading = 0;
  /** Markers for every other player (hunter = red, other preys = orange/grey), keyed by userId. */
  private otherMarkers = new Map<string, L.CircleMarker>();
  /** The hunter's userId, captured from the status snapshot — used to colour blips. */
  private hunterUserId: string | null = null;
  /** Our own last GPS fix, used to compute distance to the hunter. */
  private selfLatLng: L.LatLng | null = null;
  /** The hunter's last-known position and the epoch ms at which it was received. */
  private hunterLatLng: L.LatLng | null = null;
  private hunterLastUpdate: number | null = null;
  private playfieldPolygon: L.Polygon | null = null;
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private durationTimer: ReturnType<typeof setInterval> | null = null;
  private pingIntervalTimer: ReturnType<typeof setInterval> | null = null;
  pollIntervalSeconds = 30;
  /** Local participant state map keyed by userId */
  private participantStates = new Map<string, string>();

  /** Capacitor Geolocation watch — used only for the on-screen map marker. */
  private mapWatchId: string | null = null;
  private currentUserId: string | null = null;
  /** Guard: prevent duplicate game-ended handling. */
  private gameEndedHandled = false;
  /** Capacitor App foreground/background listener — drives resume resync. */
  private resumeListener: PluginListenerHandle | null = null;

  // -------------------------------------------------------------------------
  // Lifecycle
  // -------------------------------------------------------------------------

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

    // Participant blips are keyed by the backend's internal user id (User.Id), not the
    // Auth0 `sub`. Use the same identity source as the lobby so our own blip is recognised
    // as "self" and skipped — otherwise we'd be plotted as an orange "other prey".
    this.currentUserId = this.userState.profile()?.userId ?? null;

    this.initMap();

    // Separate watch purely for updating the on-screen map marker
    this.startMapWatch();
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
    this.resumeListener = await App.addListener('appStateChange', ({ isActive }) => {
      if (isActive) {
        this.resyncOnResume();
      }
    });
  }

  private resyncOnResume(): void {
    // The game is fully over for everyone — nothing left to reconcile. A tagged/out
    // player is NOT finished here: they remain a spectator and must still reconnect so
    // they catch the eventual `game-ended` event missed while suspended.
    if (this.gameEndedHandled) return;
    // Reconnect the realtime channel (handles the silently-dead-socket case)…
    this.connectStream();
    // …and force an immediate status refresh to catch events missed while suspended.
    // pollStatus() also detects a game that ended while we were away (see its catch).
    this.clearPoll();
    void this.pollStatus();
  }

  /**
   * Health-check the background tracking session every time the page is shown. The
   * GameLocationService is a singleton that outlives this page, so if it is already
   * tracking this game there is nothing to do. Otherwise attempt to recover a session
   * (after an OS kill) from Preferences, or derive the end time from the live game.
   */
  async ionViewWillEnter(): Promise<void> {
    await this.ensureTracking();
  }

  toggleHud(): void {
    this.hudExpanded.update(v => !v);
  }

  ngOnDestroy(): void {
    this.clearPoll();
    this.clearDurationTimer();
    this.clearPingInterval();
    this.streamService.disconnect();
    this.compass.stop();
    void this.resumeListener?.remove();
    this.resumeListener = null;
    // NOTE: intentionally do NOT stop location tracking here — the service must keep
    // broadcasting while the game is in progress even if the player leaves this page.
    this.stopMapWatch();
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
    const storedId = (await Preferences.get({ key: 'game.tracking.gameId' })).value;
    const storedEnd = (await Preferences.get({ key: 'game.tracking.gameEndTime' })).value;
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
      return new Date(new Date(game.startedAt).getTime() + game.configuration.gameDuration * 60_000);
    }
    return null;
  }

  // -------------------------------------------------------------------------
  // Map initialisation
  // -------------------------------------------------------------------------

  private initMap(): void {
    this.map = L.map('map', { zoomControl: false, attributionControl: false });
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(this.map);
    this.map.setView([52.0, 5.0], 15);
    // ion-content[fullscreen] only sizes the #map container after this runs, so
    // Leaflet caches a wrong (often zero) viewport. Recompute on the next tick so
    // tiles and the playfield overlay lay out against the real container size.
    setTimeout(() => this.map.invalidateSize(), 0);
  }

  /**
   * Start a Capacitor Geolocation watch to update the on-screen player marker.
   * This is intentionally separate from the background broadcasting so the map
   * updates at a higher frequency (continuous) than the server POST cadence.
   */
  private startMapWatch(): void {
    Geolocation.watchPosition(
      { enableHighAccuracy: true, maximumAge: 5_000 },
      (position, err) => {
        if (err || !position) {
          this.gpsAlert.set(this.translate.instant('GAME_PROGRESS.GPS_SIGNAL_LOST'));
          return;
        }
        this.gpsAlert.set(null);
        const { latitude, longitude } = position.coords;
        const latlng: L.LatLngExpression = [latitude, longitude];
        this.selfLatLng = L.latLng(latitude, longitude);
        this.updateHunterDistance();
        if (this.playerMarker) {
          this.playerMarker.setLatLng(latlng);
        } else {
          this.playerMarker = L.marker(latlng, {
            icon: this.buildSelfArrowIcon(),
            interactive: false,
            keyboard: false,
          }).addTo(this.map);
          this.captureSelfArrow();
        }
        // Only follow the player until the playfield is framed; once the field is
        // drawn we fitBounds to it and must not recenter on every GPS tick, or the
        // overlay scrolls off-screen.
        if (!this.playfieldPolygon) {
          this.map.setView(latlng);
        }
      }
    ).then((watchId) => {
      this.mapWatchId = watchId;
    }).catch(() => {
      this.gpsAlert.set('Signal lost. Find open sky.');
    });
  }

  private stopMapWatch(): void {
    if (this.mapWatchId !== null) {
      Geolocation.clearWatch({ id: this.mapWatchId });
      this.mapWatchId = null;
    }
  }

  /** Build the rotatable "you" navigation arrow used as the self marker icon. */
  private buildSelfArrowIcon(): L.DivIcon {
    return L.divIcon({
      className: 'self-arrow-marker',
      html:
        '<div class="self-arrow">' +
        '<svg viewBox="0 0 24 24" width="32" height="32">' +
        '<path d="M12 2 L20 21 L12 16 L4 21 Z" /></svg></div>',
      iconSize: [32, 32],
      iconAnchor: [16, 16],
    });
  }

  /** Grab the arrow's inner element after the marker mounts and orient it immediately. */
  private captureSelfArrow(): void {
    this.playerArrowEl = this.playerMarker?.getElement()?.querySelector('.self-arrow') ?? null;
    const h = this.heading();
    if (h !== null) this.applyHeading(h);
  }

  /**
   * Rotate the arrow to the given compass heading. The map is north-up, so the heading
   * (clockwise from north) is the rotation directly. We accumulate the angle so a sweep
   * across the 0°/360° seam turns the short way instead of spinning all the way round.
   */
  private applyHeading(heading: number): void {
    if (!this.playerArrowEl) return;
    const delta = ((heading - this.renderedHeading) % 360 + 540) % 360 - 180;
    this.renderedHeading += delta;
    this.playerArrowEl.style.transform = `rotate(${this.renderedHeading}deg)`;
  }

  // -------------------------------------------------------------------------
  // Status polling
  // -------------------------------------------------------------------------

  private async pollStatus(): Promise<void> {
    try {
      const status = await this.gamesService.getGameStatus(this.gameId);
      this.applyStatus(status);

      // A successful status poll means the game is InProgress (the status endpoint only
      // serves running games). If we entered during the Ready state, startedAt was null
      // and tracking never started — (re)start it now that the game is live. Idempotent:
      // ensureTracking short-circuits once the location service is broadcasting.
      void this.ensureTracking();

      // Capture the server-supplied ping interval for the NEXT UPDATE bar denominator.
      this.currentPingInterval = status.currentPingInterval || 30;

      // The native Android service manages its own interval autonomously —
      // we only use nextPingDuration here to pace the Angular-side UI poll.
      this.pollIntervalSeconds = status.nextPingDuration || 30;

      // Only start the ping countdown when the game is actually running.
      if (!this.waitingForStart()) {
        this.startPingCountdown(status.nextPingDuration || 30);
      }
      this.pollTimer = setTimeout(() => this.pollStatus(), this.pollIntervalSeconds * 1_000);
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
          p => p.userId !== game.hunterUserId && (p.state === 'Active' || p.state === 'Passive'),
        ).length;
        this.handleGameEnded({ gameId: this.gameId, outcome: game.outcome, survivorCount });
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
    this.hunterUserId = status.hunterUserId;

    const preys = status.participants.filter(p => p.userId !== status.hunterUserId);

    const me = status.participants.find(p => p.userId === this.currentUserId) ?? null;
    this.hasActivePenalty.set(me?.hasActivePenalty ?? false);

    // Seed local state map from snapshot
    for (const prey of preys) {
      this.participantStates.set(prey.userId, prey.state);
    }

    this.drawPlayfield(status.playfieldCoordinates);
    this.updateOtherBlips(status.participants);
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
      this.currentPingInterval = status.currentPingInterval || 30;
      this.pollIntervalSeconds = status.nextPingDuration || 30;
      this.applyStatus(status);
      // Game is now InProgress — lift the waiting overlay and start the countdown.
      this.waitingForStart.set(false);
      // The game just went live; start broadcasting location now (we entered during Ready,
      // when startedAt was null and ensureTracking could not start). Idempotent.
      void this.ensureTracking();
      this.startPingCountdown(status.nextPingDuration || 30);
      this.pollTimer = setTimeout(() => this.pollStatus(), this.pollIntervalSeconds * 1_000);
    } catch {
      // Status endpoint not yet serving (game still transitioning) — retry shortly.
      this.pollTimer = setTimeout(() => this.pollStatus(), 5_000);
    }
  }

  /**
   * Plot every player except ourselves from the status snapshot. Our own position is
   * drawn in green by the GPS watch (`startMapWatch`); the hunter is red and every
   * other prey is grey. Players without a last-known location are skipped.
   */
  private updateOtherBlips(participants: GameParticipantStatusDto[]): void {
    for (const p of participants) {
      if (p.userId === this.currentUserId) continue;
      if (!p.lastKnownLocation) continue;
      this.participantStates.set(p.userId, p.state);
      this.upsertOtherBlip(p.userId, p.lastKnownLocation.latitude, p.lastKnownLocation.longitude, p.state);
    }
  }

  /** Create or move a player blip, colouring it by role (hunter vs. other prey). */
  private upsertOtherBlip(userId: string, lat: number, lng: number, state: string): void {
    const latlng: L.LatLngExpression = [lat, lng];
    const options = this.blipOptionsFor(userId, state);
    const existing = this.otherMarkers.get(userId);
    if (existing) {
      existing.setLatLng(latlng);
      existing.setStyle(options);
    } else {
      this.otherMarkers.set(userId, L.circleMarker(latlng, options).addTo(this.map));
    }

    // Track the hunter's position + receipt time so the HUD can show distance-to-hunter
    // and how long ago that fix arrived. Stamp the time only on a genuine move.
    if (userId === this.hunterUserId) {
      if (!this.hunterLatLng || this.hunterLatLng.lat !== lat || this.hunterLatLng.lng !== lng) {
        this.hunterLastUpdate = Date.now();
      }
      this.hunterLatLng = L.latLng(lat, lng);
      this.updateHunterDistance();
    }
  }

  /** Recompute distance to the hunter from our own fix and the hunter's last-known blip. */
  private updateHunterDistance(): void {
    if (!this.selfLatLng || !this.hunterLatLng) {
      this.hunterDistance.set('--');
      this.hunterUpdatedAt.set(null);
      return;
    }
    this.hunterDistance.set(`${Math.round(this.selfLatLng.distanceTo(this.hunterLatLng))}m`);
    this.hunterUpdatedAt.set(this.hunterLastUpdate);
  }

  /** Hunter → red; other preys → orange (grey once Tagged/Out). */
  private blipOptionsFor(userId: string, state: string): L.CircleMarkerOptions {
    if (userId === this.hunterUserId) {
      return { radius: 7, color: MAP_COLORS.HUNTER, fillColor: MAP_COLORS.HUNTER, fillOpacity: 0.9, weight: 2 };
    }
    const isInactive = state === 'Tagged' || state === 'Out';
    const colour = isInactive ? MAP_COLORS.TAGGED : MAP_COLORS.CAUTION;
    return { radius: 6, color: colour, fillColor: colour, fillOpacity: isInactive ? 0.4 : 0.9, weight: 2 };
  }

  private drawPlayfield(coords: { latitude: number; longitude: number }[]): void {
    if (this.playfieldPolygon) return;
    if (coords.length < 3) return; // a polygon needs at least three vertices

    const latlngs = coords.map(c => [c.latitude, c.longitude] as L.LatLngExpression);
    this.playfieldPolygon = L.polygon(latlngs, {
      color: MAP_COLORS.SIGNAL,   // opaque border
      weight: 3,
      opacity: 1,
      fillColor: MAP_COLORS.SIGNAL, // transparent fill: faint tint, map shows through
      fillOpacity: 0.1,
    }).addTo(this.map);

    // Ensure the container size is current before framing the field, then keep
    // the whole polygon in view (the GPS watch no longer recenters once it exists).
    this.map.invalidateSize();
    this.map.fitBounds(this.playfieldPolygon.getBounds(), { padding: [24, 24] });
  }

  // -------------------------------------------------------------------------
  // Pull-to-refresh
  // -------------------------------------------------------------------------

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

  // -------------------------------------------------------------------------
  // Web PubSub real-time stream
  // -------------------------------------------------------------------------

  private connectStream(): void {
    this.streamService.connect(this.gameId);

    // WebSocket reconnected after a drop — refresh the snapshot immediately instead
    // of waiting for the next poll tick to catch missed status-change events.
    this.streamService.onReconnected(() => {
      this.clearPoll();
      void this.pollStatus();
    });

    // Live location pushes from the sweep (~every 30 s). Our own marker is driven by
    // the GPS watch, so skip self; everyone else is (re)plotted with their role colour.
    this.streamService.on<PlayerLocationUpdatedPayload>('player-location-updated', (payload) => {
      if (payload.userId === this.currentUserId) return;
      const state = payload.participantState ?? this.participantStates.get(payload.userId) ?? 'Active';
      this.participantStates.set(payload.userId, state);
      this.upsertOtherBlip(payload.userId, payload.latitude, payload.longitude, state);
    });

    // Status changes arrive from either event name — treat them identically.
    const onStatusChanged = (userId: string, newState: string): void => {
      this.participantStates.set(userId, newState);

      // Recolour the player's blip to reflect the new state (e.g. dim once Tagged/Out).
      const marker = this.otherMarkers.get(userId);
      if (marker) {
        marker.setStyle(this.blipOptionsFor(userId, newState));
      }

      // Recalculate preys-remaining count
      const activeCount = [...this.participantStates.values()].filter(s => s === 'Active' || s === 'Passive').length;
      this.preysLeft.set(activeCount);

      // React when our own state changes — switch to spectator mode but stay connected.
      if (userId === this.currentUserId) {
        if (newState === 'Tagged') {
          this.markSelfOut('tagged');
        } else if (newState === 'Out') {
          this.markSelfOut('out');
        }
      }
    };

    this.streamService.on<PlayerStatusChangedPayload>('player-status-changed', (payload) => {
      onStatusChanged(payload.userId, payload.newState);
    });

    this.streamService.on<ParticipantStatusChangedPayload>('participant-status-changed', (payload) => {
      onStatusChanged(payload.participantId, payload.newState);
    });

    // Own penalty notification
    this.streamService.on<PlayerPenalizedPayload>('player-penalized', (payload) => {
      if (payload.userId === this.currentUserId) {
        this.hasActivePenalty.set(true);
      }
    });

    this.streamService.on('state-changed', () => {
      this.handleStateChanged();
    });

    this.streamService.on<GameEndedPayload>('game-ended', (payload) => {
      this.handleGameEnded(payload);
    });
  }

  /**
   * This player has been tagged or ruled out, but the GAME is still running. Show the
   * spectator overlay while deliberately KEEPING the realtime stream, status polling and
   * the location/foreground service alive — the player keeps seeing the action and, crucially,
   * still receives the `game-ended` event so everyone reaches the outcome screen together.
   * All connections and the Android foreground service are torn down only in `handleGameEnded`.
   */
  private markSelfOut(reason: 'tagged' | 'out'): void {
    this.outReason.set(reason);
  }

  /** Idempotent: safe to call from both Web PubSub events and the poll path. */
  private handleGameEnded(payload?: GameEndedPayload): void {
    if (this.gameEndedHandled) return;
    this.gameEndedHandled = true;
    this.clearPoll();
    this.streamService.disconnect();
    this.stopMapWatch();
    void this.locationService.stop();
    // Hand the result to the debrief screen; it confirms against the server too, so
    // missing values here are non-fatal. null query params are dropped by the router.
    this.router.navigate(['/games', this.gameId, 'outcome'], {
      replaceUrl: true,
      queryParams: {
        role: 'prey',
        outcome: payload?.outcome ?? null,
        survivors: payload?.survivorCount ?? null,
      },
    });
  }

  // -------------------------------------------------------------------------
  // Helpers
  // -------------------------------------------------------------------------

  private clearPoll(): void {
    if (this.pollTimer !== null) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private formatDuration(seconds: number | null): string {
    if (seconds === null) return '--:--';
    const mins = Math.floor(seconds / 60).toString().padStart(2, '0');
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

  /** Tick the next-update countdown down once per second until the next poll resyncs it. */
  private startPingCountdown(seconds: number): void {
    this.clearPingInterval();
    this.pingCountdown.set(seconds);
    this.pingIntervalTimer = setInterval(() => {
      this.pingCountdown.set(Math.max(0, this.pingCountdown() - 1));
    }, 1000);
  }

  private clearPingInterval(): void {
    if (this.pingIntervalTimer !== null) {
      clearInterval(this.pingIntervalTimer);
      this.pingIntervalTimer = null;
    }
  }
}
