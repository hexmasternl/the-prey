import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
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
import { firstValueFrom } from 'rxjs';
import { AuthService } from '@auth0/auth0-angular';
import { App } from '@capacitor/app';
import type { PluginListenerHandle } from '@capacitor/core';
import { Geolocation } from '@capacitor/geolocation';
import { Preferences } from '@capacitor/preferences';
import { TranslatePipe } from '@ngx-translate/core';
import { GameParticipantStatusDto, GameStatusDto, GamesService } from './games.service';
import { GameStreamService, PlayerLocationUpdatedPayload, PlayerStatusChangedPayload, ParticipantStatusChangedPayload, PlayerPenalizedPayload } from './game-stream.service';
import { GameLocationService } from './game-location.service';

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
  ],
})
export class GamePreyPage implements OnInit, OnDestroy, ViewWillEnter {
  private readonly route          = inject(ActivatedRoute);
  private readonly router         = inject(Router);
  private readonly gamesService   = inject(GamesService);
  private readonly streamService  = inject(GameStreamService);
  private readonly locationService = inject(GameLocationService);
  private readonly auth           = inject(AuthService);

  readonly showSurroundingsWarning = signal(false);
  readonly warningAcknowledged = signal(false);

  /** Seconds left in the game, resynced from the server each poll and ticked down locally every second. */
  readonly secondsRemaining = signal<number | null>(null);
  readonly timeRemaining   = computed(() => this.formatDuration(this.secondsRemaining()));
  readonly preysLeft       = signal(0);
  readonly hasActivePenalty = signal(false);
  readonly gpsAlert        = signal<string | null>(null);
  readonly gameOverMessage  = signal<string | null>(null);
  /** Seconds until the next status poll, ticked down every second for the HUD. */
  readonly pingCountdown   = signal(30);
  /** Collapsed by default: only the remaining game time shows until the HUD is tapped. */
  readonly hudExpanded     = signal(false);
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
  private playerMarker: L.CircleMarker | null = null;
  /** Markers for every other player (hunter = red, other preys = grey), keyed by userId. */
  private otherMarkers = new Map<string, L.CircleMarker>();
  /** The hunter's userId, captured from the status snapshot — used to colour blips. */
  private hunterUserId: string | null = null;
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

    const user = await firstValueFrom(this.auth.user$);
    this.currentUserId = user?.sub ?? null;

    this.initMap();

    // Separate watch purely for updating the on-screen map marker
    this.startMapWatch();

    await this.pollStatus();
    this.startDurationTimer();
    this.connectStream();
    void this.registerResumeListener();
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
    // Don't resurrect a finished game (tagged/out or ended).
    if (this.gameOverMessage() || this.gameEndedHandled) return;
    // Reconnect the realtime channel (handles the silently-dead-socket case)…
    this.connectStream();
    // …and force an immediate status refresh to catch events missed while suspended.
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
      if (game.startedAt) {
        const end = new Date(new Date(game.startedAt).getTime() + game.configuration.gameDuration * 60_000);
        if (end.getTime() > Date.now()) {
          await this.locationService.start(this.gameId, end);
          this.trackingInactive.set(false);
          return;
        }
      }
    } catch {
      // fall through to the inactive warning
    }

    // 3. Nothing to resume — surface a non-blocking warning.
    this.trackingInactive.set(true);
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
          this.gpsAlert.set('Signal lost. Find open sky.');
          return;
        }
        this.gpsAlert.set(null);
        const { latitude, longitude } = position.coords;
        const latlng: L.LatLngExpression = [latitude, longitude];
        if (this.playerMarker) {
          this.playerMarker.setLatLng(latlng);
        } else {
          this.playerMarker = L.circleMarker(latlng, {
            radius: 8,
            color: '#64ff00',
            fillColor: '#64ff00',
            fillOpacity: 1,
            weight: 2,
          }).addTo(this.map);
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

  // -------------------------------------------------------------------------
  // Status polling
  // -------------------------------------------------------------------------

  private async pollStatus(): Promise<void> {
    try {
      const status = await this.gamesService.getGameStatus(this.gameId);
      this.applyStatus(status);

      // The native Android service manages its own interval autonomously —
      // we only use nextPingDuration here to pace the Angular-side UI poll.
      this.pollIntervalSeconds = status.nextPingDuration || 30;
      this.startPingCountdown(this.pollIntervalSeconds);
      this.pollTimer = setTimeout(() => this.pollStatus(), this.pollIntervalSeconds * 1_000);
    } catch {
      // Retry with a safe default on network error
      this.pollTimer = setTimeout(() => this.pollStatus(), 30_000);
    }
  }

  private applyStatus(status: GameStatusDto): void {
    this.secondsRemaining.set(status.gameDurationLeft);
    this.preysLeft.set(status.preysLeft);
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
  }

  /** Hunter → red; other preys → grey (dimmed once Tagged/Out). */
  private blipOptionsFor(userId: string, state: string): L.CircleMarkerOptions {
    if (userId === this.hunterUserId) {
      return { radius: 7, color: '#ff2f1f', fillColor: '#ff2f1f', fillOpacity: 0.9, weight: 2 };
    }
    const isInactive = state === 'Tagged' || state === 'Out';
    return { radius: 6, color: '#888888', fillColor: '#888888', fillOpacity: isInactive ? 0.4 : 0.8, weight: 2 };
  }

  private drawPlayfield(coords: { latitude: number; longitude: number }[]): void {
    if (this.playfieldPolygon) return;
    if (coords.length < 3) return; // a polygon needs at least three vertices

    const latlngs = coords.map(c => [c.latitude, c.longitude] as L.LatLngExpression);
    this.playfieldPolygon = L.polygon(latlngs, {
      color: '#64ff00',     // opaque border
      weight: 3,
      opacity: 1,
      fillColor: '#64ff00', // transparent fill: faint tint, map shows through
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

      // React when our own state changes
      if (userId === this.currentUserId) {
        if (newState === 'Tagged') {
          this.handleGameOver('You have been tagged. Game over for you.');
        } else if (newState === 'Out') {
          this.handleGameOver('You left the area for too long. You are out.');
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
      // Status poll will pick up the new state on the next tick
    });

    this.streamService.on('game-ended', () => {
      this.handleGameEnded();
    });
  }

  private handleGameOver(message: string): void {
    this.clearPoll();
    this.streamService.disconnect();
    void this.locationService.stop();
    this.stopMapWatch();
    this.gameOverMessage.set(message);
  }

  /** Idempotent: safe to call from both Web PubSub events and the poll path. */
  private handleGameEnded(): void {
    if (this.gameEndedHandled) return;
    this.gameEndedHandled = true;
    this.clearPoll();
    this.streamService.disconnect();
    void this.locationService.stop();
    this.router.navigate(['/home'], { replaceUrl: true });
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
