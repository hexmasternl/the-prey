import { inject, Injectable, signal } from '@angular/core';
import { Capacitor, registerPlugin } from '@capacitor/core';
import { Geolocation } from '@capacitor/geolocation';
import { Preferences } from '@capacitor/preferences';
import { TranslateService } from '@ngx-translate/core';
import type { BackgroundGeolocationPlugin } from '@capacitor-community/background-geolocation';
import { GamesService } from './games.service';

/**
 * Native background geolocation plugin. Registered manually because the package ships
 * type definitions only — there is no runtime web implementation, so we guard every
 * call behind `Capacitor.isNativePlatform()` and fall back to `@capacitor/geolocation`
 * on the web (browser/PWA), where background tracking is not possible anyway.
 */
const BackgroundGeolocation = registerPlugin<BackgroundGeolocationPlugin>('BackgroundGeolocation');

/** Preferences keys used to recover an active tracking session after an OS kill. */
const PREF_GAME_ID = 'game.tracking.gameId';
const PREF_END_TIME = 'game.tracking.gameEndTime';

/** Fallback reporting cadence when the backend has not (yet) supplied one. */
const DEFAULT_INTERVAL_SECONDS = 30;

interface LastFix {
  latitude: number;
  longitude: number;
  accuracy: number;
  /** Milliseconds since the unix epoch, or null if the platform did not supply it. */
  time: number | null;
}

/**
 * Single source of truth for background location broadcasting during an active game.
 *
 * Lifecycle:
 *  - `start(gameId, gameEndTime)` activates the native foreground service (Android) or
 *    iOS background location, persists the game context to Preferences for OS-kill
 *    recovery, and begins a self-scheduling post loop.
 *  - `stop()` deactivates the plugin, clears Preferences, and flips `isTracking` to false.
 *
 * Authentication reuses the in-app Auth0 session: every `POST /games/{id}/locations` is
 * sent through Angular's `HttpClient`, and `authTokenInterceptor` attaches a fresh Bearer
 * token. No client id/secret is baked into the app.
 *
 * The reporting interval is driven entirely by the backend response
 * (`nextLocationIntervalSeconds` / `penaltyIntervalSeconds`); the service never hardcodes
 * the cadence beyond the failure fallback.
 */
@Injectable({ providedIn: 'root' })
export class GameLocationService {
  private readonly games = inject(GamesService);
  private readonly translate = inject(TranslateService);

  private readonly isNative = Capacitor.isNativePlatform();

  private readonly trackingSignal = signal(false);
  /** Readonly boolean signal: true while a tracking session is active. */
  readonly isTracking = this.trackingSignal.asReadonly();

  private nativeWatcherId: string | null = null;
  private webWatchId: string | null = null;
  private currentGameId: string | null = null;
  private gameEndTime: Date | null = null;
  private lastFix: LastFix | null = null;
  private postTimer: ReturnType<typeof setTimeout> | null = null;
  private lastIntervalSeconds = DEFAULT_INTERVAL_SECONDS;

  /**
   * Begin broadcasting the device location for `gameId` until `gameEndTime`.
   *
   * - No-op when already tracking the same game.
   * - Stops the previous session first when switching to a different game.
   * - Does nothing if `gameEndTime` is already in the past.
   */
  async start(gameId: string, gameEndTime: Date): Promise<void> {
    if (this.trackingSignal() && this.currentGameId === gameId) {
      return; // already tracking this game — no duplicate session
    }
    if (this.trackingSignal() && this.currentGameId !== gameId) {
      await this.stop(); // switch games: tear down the old session first
    }
    if (gameEndTime.getTime() <= Date.now()) {
      return; // game window already closed
    }

    this.currentGameId = gameId;
    this.gameEndTime = gameEndTime;
    this.lastIntervalSeconds = DEFAULT_INTERVAL_SECONDS;
    this.lastFix = null;

    await Preferences.set({ key: PREF_GAME_ID, value: gameId });
    await Preferences.set({ key: PREF_END_TIME, value: gameEndTime.toISOString() });

    if (this.isNative) {
      const backgroundTitle = this.translate.instant('GAME_TRACKING.NOTIFICATION_TITLE');
      const backgroundMessage = this.translate.instant('GAME_TRACKING.NOTIFICATION_BODY');
      this.nativeWatcherId = await BackgroundGeolocation.addWatcher(
        { backgroundTitle, backgroundMessage, requestPermissions: true, stale: false, distanceFilter: 0 },
        (position, error) => {
          if (error || !position) return;
          this.lastFix = {
            latitude: position.latitude,
            longitude: position.longitude,
            accuracy: position.accuracy,
            time: position.time,
          };
        },
      );
    } else {
      this.webWatchId = await Geolocation.watchPosition(
        { enableHighAccuracy: true, maximumAge: 5_000 },
        (position, err) => {
          if (err || !position) return;
          this.lastFix = {
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracy: position.coords.accuracy,
            time: position.timestamp,
          };
        },
      );
    }

    this.trackingSignal.set(true);
    this.scheduleNextPost(0); // fire the first cycle immediately
  }

  /** Stop tracking, deactivate the plugin, and clear the persisted recovery context. */
  async stop(): Promise<void> {
    if (this.postTimer !== null) {
      clearTimeout(this.postTimer);
      this.postTimer = null;
    }
    if (this.nativeWatcherId !== null) {
      try {
        await BackgroundGeolocation.removeWatcher({ id: this.nativeWatcherId });
      } catch {
        // watcher already gone — ignore
      }
      this.nativeWatcherId = null;
    }
    if (this.webWatchId !== null) {
      await Geolocation.clearWatch({ id: this.webWatchId });
      this.webWatchId = null;
    }

    this.currentGameId = null;
    this.gameEndTime = null;
    this.lastFix = null;
    this.trackingSignal.set(false);

    await Preferences.remove({ key: PREF_GAME_ID });
    await Preferences.remove({ key: PREF_END_TIME });
  }

  private scheduleNextPost(delaySeconds: number): void {
    this.postTimer = setTimeout(() => void this.postCycle(), delaySeconds * 1_000);
  }

  /**
   * One reporting cycle: enforce the end-time guard, post the latest fix, then schedule
   * the next cycle using the backend-supplied interval (falling back to the last known
   * interval, then 30 s). Never throws — a failed post simply reschedules.
   */
  private async postCycle(): Promise<void> {
    if (!this.trackingSignal() || !this.currentGameId) {
      return;
    }

    // Auto-stop guard: the game window has closed.
    if (this.gameEndTime && Date.now() >= this.gameEndTime.getTime()) {
      await this.stop();
      return;
    }

    const fix = this.lastFix;
    if (!fix) {
      // No GPS fix yet — try again on the next interval.
      this.scheduleNextPost(this.lastIntervalSeconds);
      return;
    }

    let nextInterval = this.lastIntervalSeconds;
    try {
      const recordedAt = new Date(fix.time ?? Date.now()).toISOString();
      const response = await this.games.recordLocation(
        this.currentGameId,
        fix.latitude,
        fix.longitude,
        fix.accuracy,
        recordedAt,
      );
      nextInterval =
        response.penaltyIntervalSeconds ?? response.nextLocationIntervalSeconds ?? this.lastIntervalSeconds;
      this.lastIntervalSeconds = nextInterval > 0 ? nextInterval : DEFAULT_INTERVAL_SECONDS;
    } catch {
      // Network error / 401 / token failure: keep tracking, retry on the last known cadence.
      this.lastIntervalSeconds = this.lastIntervalSeconds > 0 ? this.lastIntervalSeconds : DEFAULT_INTERVAL_SECONDS;
    }

    if (this.trackingSignal()) {
      this.scheduleNextPost(this.lastIntervalSeconds);
    }
  }
}
