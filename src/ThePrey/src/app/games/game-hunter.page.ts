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
import { GameParticipantStatusDto, GameStatusDto, GamesService } from './games.service';
import { GameStreamService } from './game-stream.service';
import { GameLocationService } from './game-location.service';

@Component({
  selector: 'app-game-hunter',
  templateUrl: 'game-hunter.page.html',
  styleUrls: ['game-hunter.page.scss'],
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
export class GameHunterPage implements OnInit, OnDestroy, ViewWillEnter {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly streamService = inject(GameStreamService);
  private readonly locationService = inject(GameLocationService);
  private readonly auth = inject(AuthService);

  readonly showSurroundingsWarning = signal(false);
  readonly warningAcknowledged = signal(false);

  /** Seconds left in the game, resynced from the server each poll and ticked down locally every second. */
  readonly secondsRemaining = signal<number | null>(null);
  readonly timeRemaining = computed(() => this.formatDuration(this.secondsRemaining()));
  readonly preysLeft = signal(0);
  readonly hasActivePenalty = signal(false);
  readonly nearestDistance = signal<string>('--');
  readonly gpsAlert = signal<string | null>(null);
  readonly pingCountdown = signal(30);
  readonly showTagModal = signal(false);
  readonly taggablePrey = signal<GameParticipantStatusDto[]>([]);
  readonly tagInFlight = signal(false);
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
  private selfMarker: L.CircleMarker | null = null;
  private playfieldPolygon: L.Polygon | null = null;
  private preyMarkers = new Map<string, L.CircleMarker>();
  /** Local participant state map keyed by userId */
  private participantStates = new Map<string, string>();
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private pingIntervalTimer: ReturnType<typeof setInterval> | null = null;
  private durationTimer: ReturnType<typeof setInterval> | null = null;
  private watchId: string | null = null;
  pollIntervalSeconds = 30;
  private autoFollow = true;
  private selfLatLng: L.LatLng | null = null;

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
    await this.pollStatus();
    this.startDurationTimer();
    this.connectStream();
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

  recenter(): void {
    this.autoFollow = true;
    if (this.selfLatLng) {
      this.map.setView(this.selfLatLng, this.map.getZoom());
    }
  }

  openTagModal(): void {
    // Show only Active/Passive preys
    this.showTagModal.set(true);
  }

  closeTagModal(): void {
    this.showTagModal.set(false);
  }

  async confirmTag(prey: GameParticipantStatusDto): Promise<void> {
    if (this.tagInFlight()) return;
    this.tagInFlight.set(true);
    try {
      await this.gamesService.tagPlayer(this.gameId, prey.userId);
      this.showTagModal.set(false);
    } catch {
      // Tag failed — let user retry
    } finally {
      this.tagInFlight.set(false);
    }
  }

  private initMap(): void {
    this.map = L.map('map', { zoomControl: false, attributionControl: false });
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(this.map);
    this.map.setView([52.0, 5.0], 15);

    // Disable auto-follow when user pans manually
    this.map.on('dragstart', () => { this.autoFollow = false; });

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
            this.gpsAlert.set('Signal lost. Find open sky.');
            if (this.selfMarker) {
              this.selfMarker.remove();
              this.selfMarker = null;
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
            this.selfMarker = L.circleMarker(latlng, {
              radius: 7,
              color: '#64ff00',
              fillColor: '#64ff00',
              fillOpacity: 1,
              weight: 2,
            }).addTo(this.map);
          }
          if (this.autoFollow) {
            this.map.setView(latlng);
          }
          this.updateNearestDistance();
        }
      );
    } catch {
      this.gpsAlert.set('Signal lost. Find open sky.');
    }
  }

  private async pollStatus(): Promise<void> {
    try {
      const status = await this.gamesService.getGameStatus(this.gameId);
      this.applyStatus(status);
      this.pollIntervalSeconds = status.nextPingDuration || 30;
      this.startPingCountdown(this.pollIntervalSeconds);
      this.pollTimer = setTimeout(() => this.pollStatus(), this.pollIntervalSeconds * 1000);
    } catch {
      this.pollTimer = setTimeout(() => this.pollStatus(), 30_000);
    }
  }

  private applyStatus(status: GameStatusDto): void {
    this.secondsRemaining.set(status.gameDurationLeft);
    this.preysLeft.set(status.preysLeft);

    const hunter = status.participants.find(p => p.userId === status.hunterUserId) ?? null;
    const preys = status.participants.filter(p => p.userId !== status.hunterUserId);

    const me = hunter;
    this.hasActivePenalty.set(me?.hasActivePenalty ?? false);

    // Seed the local state map from the status snapshot
    for (const prey of preys) {
      this.participantStates.set(prey.userId, prey.state);
    }
    this.updateTaggablePrey(preys);

    this.drawPlayfield(status.playfieldCoordinates);
    this.updatePreyBlips(preys);
    this.updateNearestDistance();
  }

  private updateTaggablePrey(preys: GameParticipantStatusDto[]): void {
    this.taggablePrey.set(preys.filter(p => p.state === 'Active' || p.state === 'Passive'));
  }

  private drawPlayfield(coords: { latitude: number; longitude: number }[]): void {
    if (this.playfieldPolygon) return;
    if (!coords.length) return;

    const latlngs = coords.map(c => [c.latitude, c.longitude] as L.LatLngExpression);
    this.playfieldPolygon = L.polygon(latlngs, {
      color: '#ff2f1f',
      fillColor: 'rgba(255,47,31,0.10)',
      fillOpacity: 0.1,
      weight: 2,
    }).addTo(this.map);
    this.map.fitBounds(this.playfieldPolygon.getBounds());
  }

  private updatePreyBlips(preys: GameParticipantStatusDto[]): void {
    for (const prey of preys) {
      if (!prey.lastKnownLocation) continue;
      this.upsertPreyBlip(prey.userId, prey.lastKnownLocation.latitude, prey.lastKnownLocation.longitude, prey.state);
    }
  }

  private upsertPreyBlip(userId: string, lat: number, lng: number, state: string): void {
    const latlng: L.LatLngExpression = [lat, lng];
    const isInactive = state === 'Tagged' || state === 'Out';
    const options: L.CircleMarkerOptions = isInactive
      ? { radius: 6, color: '#888888', fillColor: '#888888', fillOpacity: 0.7, weight: 2 }
      : { radius: 6, color: '#ff2f1f', fillColor: '#ff2f1f', fillOpacity: 0.9, weight: 2 };

    const existing = this.preyMarkers.get(userId);
    if (existing) {
      existing.setLatLng(latlng);
      existing.setStyle(options);
    } else {
      const marker = L.circleMarker(latlng, options).addTo(this.map);
      this.preyMarkers.set(userId, marker);
    }
  }

  private updateNearestDistance(): void {
    if (!this.selfLatLng || this.preyMarkers.size === 0) {
      this.nearestDistance.set('--');
      return;
    }
    let minMetres = Infinity;
    for (const [userId, marker] of this.preyMarkers.entries()) {
      const state = this.participantStates.get(userId) ?? 'Active';
      if (state === 'Tagged' || state === 'Out') continue;
      const d = this.selfLatLng.distanceTo(marker.getLatLng());
      if (d < minMetres) minMetres = d;
    }
    this.nearestDistance.set(minMetres === Infinity ? '--' : `${Math.round(minMetres)}m`);
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

  private startPingCountdown(seconds: number): void {
    this.clearPingInterval();
    this.pingCountdown.set(seconds);
    this.pingIntervalTimer = setInterval(() => {
      const next = Math.max(0, this.pingCountdown() - 1);
      this.pingCountdown.set(next);
    }, 1000);
  }

  private connectStream(): void {
    // Token provider (not a one-shot token) so every reconnect uses a fresh JWT.
    this.streamService.connect(this.gameId, () => firstValueFrom(this.auth.getAccessTokenSilently()));

    // Prey locations broadcast over SSE may have been missed while the stream was down —
    // refresh the status snapshot immediately instead of waiting for the next poll tick.
    this.streamService.onReconnected(() => {
      this.clearPoll();
      void this.pollStatus();
    });

    this.streamService.on('participant-located', (payload) => {
      if (payload.participantRole === 'Prey') {
        const state = payload.participantState ?? this.participantStates.get(payload.userId) ?? 'Active';
        this.participantStates.set(payload.userId, state);
        this.upsertPreyBlip(payload.userId, payload.latitude, payload.longitude, state);
        this.updateNearestDistance();
      }
    });

    this.streamService.on('participant-status-changed', (payload) => {
      this.participantStates.set(payload.participantId, payload.newState);
      const marker = this.preyMarkers.get(payload.participantId);
      if (marker) {
        const isInactive = payload.newState === 'Tagged' || payload.newState === 'Out';
        marker.setStyle(isInactive
          ? { color: '#888888', fillColor: '#888888', fillOpacity: 0.7 }
          : { color: '#ff2f1f', fillColor: '#ff2f1f', fillOpacity: 0.9 }
        );
      }
      // Recalculate preys left
      const activeCount = [...this.participantStates.values()].filter(s => s === 'Active' || s === 'Passive').length;
      this.preysLeft.set(activeCount);
      // Update taggable list
      const taggable = [...this.participantStates.entries()]
        .filter(([, s]) => s === 'Active' || s === 'Passive')
        .map(([id]) => ({ userId: id } as GameParticipantStatusDto));
      this.taggablePrey.set(taggable);
      this.updateNearestDistance();
    });

    this.streamService.on('state-changed', () => {
      // status poll will pick up the new state
    });

    this.streamService.on('game-ended', () => {
      this.clearPoll();
      this.streamService.disconnect();
      void this.locationService.stop();
      this.router.navigate(['/home'], { replaceUrl: true });
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
