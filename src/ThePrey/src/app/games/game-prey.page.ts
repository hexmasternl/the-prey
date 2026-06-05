import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonContent,
} from '@ionic/angular/standalone';
import * as L from 'leaflet';
import { AuthService } from '@auth0/auth0-angular';
import { firstValueFrom } from 'rxjs';
import { GameStatusDto, GamesService } from './games.service';
import { GameStreamService } from './game-stream.service';

@Component({
  selector: 'app-game-prey',
  templateUrl: 'game-prey.page.html',
  styleUrls: ['game-prey.page.scss'],
  imports: [IonContent],
})
export class GamePreyPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly streamService = inject(GameStreamService);
  private readonly auth = inject(AuthService);

  readonly timeRemaining = signal('--:--');
  readonly preysLeft = signal(0);
  readonly hasActivePenalty = signal(false);
  readonly gpsAlert = signal<string | null>(null);

  private gameId!: string;
  private map!: L.Map;
  private playerMarker: L.CircleMarker | null = null;
  private playfieldPolygon: L.Polygon | null = null;
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private watchId: number | null = null;
  private currentUserId: string | null = null;

  async ngOnInit(): Promise<void> {
    this.gameId = this.route.snapshot.paramMap.get('id') ?? '';
    const token = await firstValueFrom(this.auth.getAccessTokenSilently());
    const user = await firstValueFrom(this.auth.user$);
    this.currentUserId = user?.sub ?? null;

    this.initMap();
    this.startGps();
    await this.pollStatus(token);
    this.connectStream(token);
  }

  ngOnDestroy(): void {
    this.clearPoll();
    this.streamService.disconnect();
    if (this.watchId !== null) {
      navigator.geolocation.clearWatch(this.watchId);
      this.watchId = null;
    }
    if (this.map) {
      this.map.remove();
    }
  }

  private initMap(): void {
    this.map = L.map('map', { zoomControl: false, attributionControl: false });
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(this.map);
    this.map.setView([52.0, 5.0], 15);
  }

  private startGps(): void {
    if (!navigator.geolocation) {
      this.gpsAlert.set('Signal lost. Find open sky.');
      return;
    }
    this.watchId = navigator.geolocation.watchPosition(
      (pos) => {
        this.gpsAlert.set(null);
        const { latitude, longitude } = pos.coords;
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
      },
      () => { this.gpsAlert.set('Signal lost. Find open sky.'); },
      { enableHighAccuracy: true, maximumAge: 5000 }
    );
  }

  private async pollStatus(token: string): Promise<void> {
    try {
      const status = await this.gamesService.getGameStatus(this.gameId);
      this.applyStatus(status);
      const interval = (status.nextPingDuration || 30) * 1000;
      this.pollTimer = setTimeout(() => this.pollStatus(token), interval);
    } catch {
      this.pollTimer = setTimeout(() => this.pollStatus(token), 30_000);
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

  private connectStream(token: string): void {
    this.streamService.connect(this.gameId, token);

    this.streamService.on('participant-located', (payload) => {
      if (payload.participantRole === 'Hunter') {
        // hunter location is for contextual awareness; no map marker shown to prey
      }
    });

    this.streamService.on('state-changed', () => {
      // status poll will pick up the new state
    });

    this.streamService.on('game-ended', () => {
      this.clearPoll();
      this.streamService.disconnect();
      this.router.navigate(['/home'], { replaceUrl: true });
    });
  }

  private clearPoll(): void {
    if (this.pollTimer !== null) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }
}
