import { inject, Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Capacitor } from '@capacitor/core';
import { Geolocation } from '@capacitor/geolocation';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { GameLocation } from './game-location-plugin';

interface LocationBody {
  latitude: number;
  longitude: number;
}

/**
 * GameLocationService is the single point of truth for background location
 * broadcasting during a prey game session.
 *
 * - On Android: delegates entirely to the native LocationForegroundService via
 *   the GameLocation Capacitor plugin. The service runs in a foreground service
 *   context so it survives screen lock.
 *
 * - On web (browser / PWA): falls back to @capacitor/geolocation + HttpClient
 *   with a setInterval loop. This only works while the browser tab is active
 *   (browsers suspend JS timers in background tabs), but it provides parity for
 *   local development and testing.
 *
 * Usage in GamePreyPage:
 *   await this.locationService.startTracking(gameId, userId, intervalMs);
 *   // ...later...
 *   this.locationService.stopTracking();
 *   // The native Android service manages its own interval autonomously.
 *   // updateInterval() is retained for the web fallback and future use.
 */
@Injectable({ providedIn: 'root' })
export class GameLocationService implements OnDestroy {
  private readonly http = inject(HttpClient);

  /** True when the native Android plugin is available. */
  private readonly isNative = Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android';

  // Web-fallback state
  private webIntervalHandle: ReturnType<typeof setInterval> | null = null;
  private webWatchId: string | null = null;
  private webGameId: string | null = null;
  private webIntervalMs = 30_000;
  private lastLat: number | null = null;
  private lastLon: number | null = null;

  // -------------------------------------------------------------------------
  // Public API
  // -------------------------------------------------------------------------

  /**
   * Start broadcasting the device's GPS location to the game server.
   *
   * @param gameId     Active game UUID.
   * @param userId     The player's own user ID — passed to the native service so it
   *                   can detect elimination and self-terminate.
   * @param intervalMs Initial milliseconds between each POST. Defaults to 30 000.
   *                   On Android the service updates this from the server's
   *                   nextPingDuration autonomously.
   */
  async startTracking(gameId: string, userId: string, intervalMs = 30_000): Promise<void> {
    if (this.isNative) {
      await GameLocation.startTracking({
        gameId,
        apiUrl: environment.apiUrl,
        clientId: environment.auth0ClientId,
        clientSecret: environment.auth0ClientSecret,
        userId,
        intervalMs,
      });
    } else {
      await this.startWebTracking(gameId, intervalMs);
    }
  }

  /**
   * Stop all location broadcasting and clean up resources.
   */
  async stopTracking(): Promise<void> {
    if (this.isNative) {
      await GameLocation.stopTracking();
    } else {
      this.stopWebTracking();
    }
  }

  /**
   * Adjust the POST interval without restarting. Call this whenever the
   * server returns a new `nextPingDuration` value.
   *
   * @param intervalMs New interval in milliseconds.
   */
  async updateInterval(intervalMs: number): Promise<void> {
    if (this.isNative) {
      await GameLocation.updateInterval({ intervalMs });
    } else {
      // Restart the web interval with the new cadence
      if (this.webGameId) {
        this.stopWebTracking();
        await this.startWebTracking(this.webGameId, intervalMs);
      }
    }
  }

  ngOnDestroy(): void {
    this.stopWebTracking();
  }

  // -------------------------------------------------------------------------
  // Web / browser fallback
  // -------------------------------------------------------------------------

  private async startWebTracking(gameId: string, intervalMs: number): Promise<void> {
    this.stopWebTracking(); // Clear any previous session

    this.webGameId     = gameId;
    this.webIntervalMs = intervalMs;

    // Start watching GPS position with Capacitor Geolocation
    this.webWatchId = await Geolocation.watchPosition(
      { enableHighAccuracy: true, maximumAge: 5_000 },
      (position, err) => {
        if (err || !position) return;
        this.lastLat = position.coords.latitude;
        this.lastLon = position.coords.longitude;
      }
    );

    // Fire immediately, then repeat at intervalMs
    this.postWebLocation();
    this.webIntervalHandle = setInterval(() => this.postWebLocation(), intervalMs);
  }

  private stopWebTracking(): void {
    if (this.webIntervalHandle !== null) {
      clearInterval(this.webIntervalHandle);
      this.webIntervalHandle = null;
    }
    if (this.webWatchId !== null) {
      Geolocation.clearWatch({ id: this.webWatchId });
      this.webWatchId = null;
    }
    this.webGameId   = null;
    this.lastLat     = null;
    this.lastLon     = null;
  }

  private postWebLocation(): void {
    if (this.lastLat === null || this.lastLon === null) return;
    if (!this.webGameId) return;

    const body: LocationBody = { latitude: this.lastLat, longitude: this.lastLon };
    const url = `${environment.apiUrl}/games/${this.webGameId}/location`;

    firstValueFrom(
      this.http.post(url, body)
    ).catch(() => {
      // Network error — skip this tick; we'll retry on the next interval
    });
  }
}
