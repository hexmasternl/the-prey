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
  ViewWillEnter,
} from '@ionic/angular/standalone';
import * as L from 'leaflet';
import { AuthService } from '@auth0/auth0-angular';
import { firstValueFrom } from 'rxjs';
import { Geolocation } from '@capacitor/geolocation';
import { Preferences } from '@capacitor/preferences';
import { TranslatePipe } from '@ngx-translate/core';
import { GameStatusDto, GamesService } from './games.service';
import { GameStreamService } from './game-stream.service';
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
  private playfieldPolygon: L.Polygon | null = null;
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private durationTimer: ReturnType<typeof setInterval> | null = null;
  /** Local participant state map keyed by userId */
  private participantStates = new Map<string, string>();

  /** Capacitor Geolocation watch — used only for the on-screen map marker. */
  private mapWatchId: string | null = null;
  private currentUserId: string | null = null;

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

  ngOnDestroy(): void {
    this.clearPoll();
    this.clearDurationTimer();
    this.streamService.disconnect();
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
        this.map.setView(latlng);
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
      const intervalMs = (status.nextPingDuration || 30) * 1_000;
      this.pollTimer = setTimeout(() => this.pollStatus(), intervalMs);
    } catch {
      // Retry with a safe default on network error
      this.pollTimer = setTimeout(() => this.pollStatus(), 30_000);
    }
  }

  private applyStatus(status: GameStatusDto): void {
    this.secondsRemaining.set(status.gameDurationLeft);
    this.preysLeft.set(status.preysLeft);

    const preys = status.participants.filter(p => p.userId !== status.hunterUserId);

    const me = status.participants.find(p => p.userId === this.currentUserId) ?? null;
    this.hasActivePenalty.set(me?.hasActivePenalty ?? false);

    // Seed local state map from snapshot
    for (const prey of preys) {
      this.participantStates.set(prey.userId, prey.state);
    }

    this.drawPlayfield(status.playfieldCoordinates);
  }

  private drawPlayfield(coords: { latitude: number; longitude: number }[]): void {
    if (this.playfieldPolygon) return;
    if (!coords.length) return;

    const latlngs = coords.map(c => [c.latitude, c.longitude] as L.LatLngExpression);
    this.playfieldPolygon = L.polygon(latlngs, {
      color: '#64ff00',
      fillColor: 'rgba(100,255,0,0.12)',
      fillOpacity: 0.12,
      weight: 2,
    }).addTo(this.map);
    this.map.fitBounds(this.playfieldPolygon.getBounds());
  }

  // -------------------------------------------------------------------------
  // SSE stream
  // -------------------------------------------------------------------------

  private connectStream(): void {
    // Token provider (not a one-shot token) so every reconnect uses a fresh JWT.
    this.streamService.connect(this.gameId, () => firstValueFrom(this.auth.getAccessTokenSilently()));

    // Status changes (tagged/out, preys left) may have been missed while the stream was
    // down — refresh the snapshot immediately instead of waiting for the next poll tick.
    this.streamService.onReconnected(() => {
      this.clearPoll();
      void this.pollStatus();
    });

    this.streamService.on('participant-located', (payload) => {
      if (payload.participantRole === 'Prey' && payload.participantState) {
        this.participantStates.set(payload.userId, payload.participantState);
      }
    });

    this.streamService.on('participant-status-changed', (payload) => {
      this.participantStates.set(payload.participantId, payload.newState);

      // Recalculate preys-remaining count
      const activeCount = [...this.participantStates.values()].filter(s => s === 'Active' || s === 'Passive').length;
      this.preysLeft.set(activeCount);

      // React when our own state changes
      if (payload.participantId === this.currentUserId) {
        if (payload.newState === 'Tagged') {
          this.handleGameOver('You have been tagged. Game over for you.');
        } else if (payload.newState === 'Out') {
          this.handleGameOver('You left the area for too long. You are out.');
        }
      }
    });

    this.streamService.on('state-changed', () => {
      // Status poll will pick up the new state on the next tick
    });

    this.streamService.on('game-ended', () => {
      this.clearPoll();
      this.streamService.disconnect();
      void this.locationService.stop();
      this.router.navigate(['/home'], { replaceUrl: true });
    });
  }

  private handleGameOver(message: string): void {
    this.clearPoll();
    this.streamService.disconnect();
    void this.locationService.stop();
    this.stopMapWatch();
    this.gameOverMessage.set(message);
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
}
