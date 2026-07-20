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

/** Consecutive failed posts before the session is flagged as degraded to the UI. */
const REPORTING_FAILURE_THRESHOLD = 3;

interface LastFix {
  latitude: number;
  longitude: number;
  accuracy: number;
  /** Milliseconds since the unix epoch, or null if the platform did not supply it. */
  time: number | null;
}

/**
 * Why location reporting is not working, surfaced to the UI so the player understands
 * a broken-looking game instead of seeing a silently empty map.
 *  - `denied`: the OS location permission was refused/revoked — the user must grant it.
 *  - `unavailable`: location is enabled but no fix can be obtained (no signal, GPS off).
 */
export type GpsErrorKind = 'denied' | 'unavailable';

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

  private readonly gpsErrorSignal = signal<GpsErrorKind | null>(null);
  /**
   * Readonly signal describing why the device cannot produce a location fix, or `null`
   * when GPS is healthy. Pages surface this so a denied permission or lost signal is
   * visible instead of an empty map. Cleared as soon as a valid fix arrives.
   */
  readonly gpsError = this.gpsErrorSignal.asReadonly();

  private readonly reportingDegradedSignal = signal(false);
  /**
   * Readonly signal: true once several consecutive location posts have failed (network
   * loss, expired session, server down). Surfaced so the player knows their position is
   * no longer reaching the server rather than silently going stale. Cleared on the next
   * successful post.
   */
  readonly reportingDegraded = this.reportingDegradedSignal.asReadonly();

  /** Consecutive failed post cycles; drives `reportingDegraded` past the threshold. */
  private consecutiveFailures = 0;

  private nativeWatcherId: string | null = null;
  private webWatchId: string | null = null;
  private currentGameId: string | null = null;
  private gameEndTime: Date | null = null;
  private lastFix: LastFix | null = null;
  private postTimer: ReturnType<typeof setTimeout> | null = null;
  private lastIntervalSeconds = DEFAULT_INTERVAL_SECONDS;
  /** Wall-clock ms of the last post attempt; gates the reporting cadence. */
  private lastPostAtMs = 0;
  /** Guards against overlapping posts when the timer and a GPS-fix trigger coincide. */
  private postInFlight = false;

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
    this.lastPostAtMs = 0;
    this.postInFlight = false;
    this.gpsErrorSignal.set(null);
    this.reportingDegradedSignal.set(false);
    this.consecutiveFailures = 0;

    await Preferences.set({ key: PREF_GAME_ID, value: gameId });
    await Preferences.set({ key: PREF_END_TIME, value: gameEndTime.toISOString() });

    if (this.isNative) {
      const backgroundTitle = this.translate.instant('GAME_TRACKING.NOTIFICATION_TITLE');
      const backgroundMessage = this.translate.instant('GAME_TRACKING.NOTIFICATION_BODY');
      this.nativeWatcherId = await BackgroundGeolocation.addWatcher(
        { backgroundTitle, backgroundMessage, requestPermissions: true, stale: false, distanceFilter: 0 },
        (position, error) => {
          if (error) {
            // The plugin reports a denied/disabled permission as code 'NOT_AUTHORIZED'.
            this.gpsErrorSignal.set(error.code === 'NOT_AUTHORIZED' ? 'denied' : 'unavailable');
            return;
          }
          if (!position) return;
          this.gpsErrorSignal.set(null);
          this.lastFix = {
            latitude: position.latitude,
            longitude: position.longitude,
            accuracy: position.accuracy,
            time: position.time,
          };
          // Drive reporting from the native fix stream. The foreground service wakes the
          // JS bridge for every fix even while the screen is off / the app is in Doze,
          // where the setTimeout cadence loop is frozen and would never post.
          void this.maybePost('fix');
        },
      );
    } else {
      this.webWatchId = await Geolocation.watchPosition(
        { enableHighAccuracy: true, maximumAge: 5_000 },
        (position, err) => {
          if (err) {
            this.gpsErrorSignal.set(this.classifyWebGpsError(err));
            return;
          }
          if (!position) return;
          this.gpsErrorSignal.set(null);
          this.lastFix = {
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracy: position.coords.accuracy,
            time: position.timestamp,
          };
          void this.maybePost('fix');
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
    this.lastPostAtMs = 0;
    this.postInFlight = false;
    this.trackingSignal.set(false);
    this.gpsErrorSignal.set(null);
    this.reportingDegradedSignal.set(false);
    this.consecutiveFailures = 0;

    await Preferences.remove({ key: PREF_GAME_ID });
    await Preferences.remove({ key: PREF_END_TIME });
  }

  /** Map a web Geolocation error to our coarse error kind (code 1 = PERMISSION_DENIED). */
  private classifyWebGpsError(err: unknown): GpsErrorKind {
    const code = (err as { code?: number } | null)?.code;
    return code === 1 ? 'denied' : 'unavailable';
  }

  private scheduleNextPost(delaySeconds: number): void {
    if (this.postTimer !== null) {
      clearTimeout(this.postTimer);
    }
    this.postTimer = setTimeout(() => void this.maybePost('timer'), Math.max(0, delaySeconds) * 1_000);
  }

  /**
   * Single funnel for reporting, driven by two independent triggers:
   *  - `'timer'`: the self-scheduling `setTimeout` cadence loop. Reliable in the
   *    foreground (and on the web), but frozen by Android Doze when the screen is off.
   *  - `'fix'`: every GPS fix delivered by the native foreground service. This keeps
   *    reporting alive in the background precisely when the timer is frozen.
   *
   * Both funnel through the same end-time guard and interval gate, so the two triggers
   * never double-post: a post only goes out once at least `lastIntervalSeconds` has
   * elapsed since the previous attempt. The `'timer'` trigger owns re-arming the timer;
   * a premature `'fix'` simply waits for the cadence window to open.
   */
  private async maybePost(trigger: 'timer' | 'fix'): Promise<void> {
    if (!this.trackingSignal() || !this.currentGameId) {
      return;
    }

    // Auto-stop guard: the game window has closed.
    if (this.gameEndTime && Date.now() >= this.gameEndTime.getTime()) {
      await this.stop();
      return;
    }

    // No GPS fix yet — nothing to send. The timer keeps retrying; a fix trigger just waits.
    if (!this.lastFix) {
      if (trigger === 'timer') {
        this.scheduleNextPost(this.lastIntervalSeconds);
      }
      return;
    }

    const dueInMs = this.lastPostAtMs + this.lastIntervalSeconds * 1_000 - Date.now();
    if (dueInMs > 0) {
      // Not due yet. Only the timer re-arms (for the remaining window); ignore early fixes.
      if (trigger === 'timer') {
        this.scheduleNextPost(dueInMs / 1_000);
      }
      return;
    }

    await this.post();

    // Re-arm the cadence timer for the foreground / web case. In the background this timer
    // may be frozen by Doze — the next native GPS fix calls maybePost('fix') and keeps
    // reporting alive regardless.
    if (this.trackingSignal()) {
      this.scheduleNextPost(this.lastIntervalSeconds);
    }
  }

  /**
   * Post the latest fix once. Stamps `lastPostAtMs` up front (so the interval gate holds
   * for both success and failure), and guards against overlapping calls when the timer
   * and a GPS fix fire at the same moment. Never throws — a failed post is recorded as a
   * degraded cycle and retried on the next trigger.
   */
  private async post(): Promise<void> {
    if (this.postInFlight) {
      return;
    }
    const fix = this.lastFix;
    if (!fix) {
      return;
    }

    this.postInFlight = true;
    this.lastPostAtMs = Date.now();
    try {
      const recordedAt = new Date(fix.time ?? Date.now()).toISOString();
      const response = await this.games.recordLocation(
        this.currentGameId!,
        fix.latitude,
        fix.longitude,
        fix.accuracy,
        recordedAt,
      );
      const nextInterval =
        response.penaltyIntervalSeconds ?? response.nextLocationIntervalSeconds ?? this.lastIntervalSeconds;
      this.lastIntervalSeconds = nextInterval > 0 ? nextInterval : DEFAULT_INTERVAL_SECONDS;
      // Post succeeded — position is reaching the server again.
      this.consecutiveFailures = 0;
      this.reportingDegradedSignal.set(false);
    } catch {
      // Network error / 401 / token failure: keep tracking, retry on the last known cadence,
      // but flag the session as degraded once failures persist so the UI can warn the player.
      this.lastIntervalSeconds = this.lastIntervalSeconds > 0 ? this.lastIntervalSeconds : DEFAULT_INTERVAL_SECONDS;
      this.consecutiveFailures += 1;
      if (this.consecutiveFailures >= REPORTING_FAILURE_THRESHOLD) {
        this.reportingDegradedSignal.set(true);
      }
    } finally {
      this.postInFlight = false;
    }
  }
}
