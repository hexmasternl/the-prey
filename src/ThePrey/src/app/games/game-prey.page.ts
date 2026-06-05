import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { IonContent } from '@ionic/angular/standalone';
import * as L from 'leaflet';
import { AuthService } from '@auth0/auth0-angular';
import { firstValueFrom } from 'rxjs';
import { Geolocation } from '@capacitor/geolocation';
import { GameStatusDto, GamesService } from './games.service';
import { GameStreamService } from './game-stream.service';
import { GameLocationService } from './game-location.service';

@Component({
  selector: 'app-game-prey',
  templateUrl: 'game-prey.page.html',
  styleUrls: ['game-prey.page.scss'],
  imports: [IonContent],
})
export class GamePreyPage implements OnInit, OnDestroy {
  private readonly route          = inject(ActivatedRoute);
  private readonly router         = inject(Router);
  private readonly gamesService   = inject(GamesService);
  private readonly streamService  = inject(GameStreamService);
  private readonly locationService = inject(GameLocationService);
  private readonly auth           = inject(AuthService);

  readonly timeRemaining   = signal('--:--');
  readonly preysLeft       = signal(0);
  readonly hasActivePenalty = signal(false);
  readonly gpsAlert        = signal<string | null>(null);

  private gameId!: string;
  private token!: string;
  private map!: L.Map;
  private playerMarker: L.CircleMarker | null = null;
  private playfieldPolygon: L.Polygon | null = null;
  private pollTimer: ReturnType<typeof setTimeout> | null = null;

  /** Capacitor Geolocation watch — used only for the on-screen map marker. */
  private mapWatchId: string | null = null;
  private currentUserId: string | null = null;

  // -------------------------------------------------------------------------
  // Lifecycle
  // -------------------------------------------------------------------------

  async ngOnInit(): Promise<void> {
    this.gameId = this.route.snapshot.paramMap.get('id') ?? '';
    this.token  = await firstValueFrom(this.auth.getAccessTokenSilently());

    const user = await firstValueFrom(this.auth.user$);
    this.currentUserId = user?.sub ?? null;

    this.initMap();

    // Start background location broadcasting (native foreground service on Android,
    // interval + HttpClient fallback on web). On Android the service autonomously
    // polls game status, updates its own interval, and self-terminates when the game ends.
    await this.locationService.startTracking(this.gameId, this.currentUserId ?? '', 30_000);

    // Separate watch purely for updating the on-screen map marker
    this.startMapWatch();

    await this.pollStatus();
    this.connectStream();
  }

  ngOnDestroy(): void {
    this.clearPoll();
    this.streamService.disconnect();
    this.locationService.stopTracking();
    this.stopMapWatch();
    if (this.map) {
      this.map.remove();
    }
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
    const mins = Math.floor(status.gameDurationLeft / 60).toString().padStart(2, '0');
    const secs = (status.gameDurationLeft % 60).toString().padStart(2, '0');
    this.timeRemaining.set(`${mins}:${secs}`);
    this.preysLeft.set(status.preys.length);

    const me = status.preys.find(p => p.userId === this.currentUserId)
      ?? (status.hunter?.userId === this.currentUserId ? status.hunter : null);
    this.hasActivePenalty.set(me?.hasActivePenalty ?? false);

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
    this.streamService.connect(this.gameId, this.token);

    this.streamService.on('participant-located', (payload) => {
      if (payload.participantRole === 'Hunter') {
        // Hunter location is for contextual awareness; no map marker shown to prey
      }
    });

    this.streamService.on('state-changed', () => {
      // Status poll will pick up the new state on the next tick
    });

    this.streamService.on('game-ended', () => {
      this.clearPoll();
      this.streamService.disconnect();
      this.locationService.stopTracking();
      this.router.navigate(['/home'], { replaceUrl: true });
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
}
