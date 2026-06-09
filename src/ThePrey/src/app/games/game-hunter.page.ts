import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { IonContent } from '@ionic/angular/standalone';
import * as L from 'leaflet';
import { AuthService } from '@auth0/auth0-angular';
import { firstValueFrom } from 'rxjs';
import { GameParticipantStatusDto, GameStatusDto, GamesService } from './games.service';
import { GameStreamService } from './game-stream.service';

@Component({
  selector: 'app-game-hunter',
  templateUrl: 'game-hunter.page.html',
  styleUrls: ['game-hunter.page.scss'],
  imports: [IonContent],
})
export class GameHunterPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly streamService = inject(GameStreamService);
  private readonly auth = inject(AuthService);

  readonly timeRemaining = signal('--:--');
  readonly preysLeft = signal(0);
  readonly hasActivePenalty = signal(false);
  readonly nearestDistance = signal<string>('--');
  readonly gpsAlert = signal<string | null>(null);
  readonly pingCountdown = signal(30);
  readonly showTagModal = signal(false);
  readonly taggablePrey = signal<GameParticipantStatusDto[]>([]);
  readonly tagInFlight = signal(false);

  private gameId!: string;
  private map!: L.Map;
  private selfMarker: L.CircleMarker | null = null;
  private playfieldPolygon: L.Polygon | null = null;
  private preyMarkers = new Map<string, L.CircleMarker>();
  /** Local participant state map keyed by userId */
  private participantStates = new Map<string, string>();
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private pingIntervalTimer: ReturnType<typeof setInterval> | null = null;
  private watchId: number | null = null;
  pollIntervalSeconds = 30;
  private autoFollow = true;
  private selfLatLng: L.LatLng | null = null;

  async ngOnInit(): Promise<void> {
    this.gameId = this.route.snapshot.paramMap.get('id') ?? '';
    const token = await firstValueFrom(this.auth.getAccessTokenSilently());

    this.initMap();
    this.startGps();
    await this.pollStatus();
    this.connectStream(token);
  }

  ngOnDestroy(): void {
    this.clearPoll();
    this.clearPingInterval();
    this.streamService.disconnect();
    if (this.watchId !== null) {
      navigator.geolocation.clearWatch(this.watchId);
      this.watchId = null;
    }
    if (this.map) {
      this.map.remove();
    }
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
      },
      () => {
        this.gpsAlert.set('Signal lost. Find open sky.');
        if (this.selfMarker) {
          this.selfMarker.remove();
          this.selfMarker = null;
        }
      },
      { enableHighAccuracy: true, maximumAge: 5000 }
    );
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
    const mins = Math.floor(status.gameDurationLeft / 60).toString().padStart(2, '0');
    const secs = (status.gameDurationLeft % 60).toString().padStart(2, '0');
    this.timeRemaining.set(`${mins}:${secs}`);
    this.preysLeft.set(status.preysLeft);

    const me = status.hunter;
    this.hasActivePenalty.set(me?.hasActivePenalty ?? false);

    // Seed the local state map from the status snapshot
    for (const prey of status.preys) {
      this.participantStates.set(prey.userId, prey.state);
    }
    this.updateTaggablePrey(status.preys);

    this.drawPlayfield(status.playfieldCoordinates);
    this.updatePreyBlips(status);
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

  private updatePreyBlips(status: GameStatusDto): void {
    for (const prey of status.preys) {
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

  private startPingCountdown(seconds: number): void {
    this.clearPingInterval();
    this.pingCountdown.set(seconds);
    this.pingIntervalTimer = setInterval(() => {
      const next = Math.max(0, this.pingCountdown() - 1);
      this.pingCountdown.set(next);
    }, 1000);
  }

  private connectStream(token: string): void {
    this.streamService.connect(this.gameId, token);

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
