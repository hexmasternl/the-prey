import { registerPlugin } from '@capacitor/core';

/**
 * Options for starting background location tracking.
 */
export interface StartTrackingOptions {
  /** UUID of the active game session. */
  gameId: string;
  /** API base URL, no trailing slash (e.g. "https://api.theprey.eu"). */
  apiUrl: string;
  /** Auth0 machine-to-machine client ID for the client credentials grant. */
  clientId: string;
  /** Auth0 machine-to-machine client secret for the client credentials grant. */
  clientSecret: string;
  /** The player's own user ID — used by the native service to detect elimination. */
  userId: string;
  /** Initial POST interval in milliseconds. Defaults to 30 000 in the native service. */
  intervalMs: number;
}

export interface UpdateIntervalOptions {
  /** New POST interval in milliseconds. */
  intervalMs: number;
}

export interface StartTrackingResult {
  started: boolean;
}

/**
 * GameLocationPlugin bridges Angular code to the native Android
 * LocationForegroundService via Capacitor.
 *
 * On web/browser targets every method is a no-op (the Angular service
 * falls back to @capacitor/geolocation + HttpClient instead).
 */
export interface GameLocationPlugin {
  /**
   * Start the Android foreground service that posts GPS coordinates at
   * `intervalMs` cadence. Requests location permissions if not yet granted.
   * Resolves once the service Intent has been dispatched.
   */
  startTracking(options: StartTrackingOptions): Promise<StartTrackingResult>;

  /**
   * Stop the foreground service. Safe to call even if not currently tracking.
   */
  stopTracking(): Promise<void>;

  /**
   * Adjust the posting interval without restarting the service.
   * Useful when the server returns a new `nextPingDuration`.
   */
  updateInterval(options: UpdateIntervalOptions): Promise<void>;
}

/**
 * Singleton plugin instance.
 *
 * On Android: delegates to GameLocationPlugin.kt via Capacitor bridge.
 * On web: all calls resolve immediately (no-op stubs injected by Capacitor).
 */
export const GameLocation = registerPlugin<GameLocationPlugin>('GameLocation', {
  web: () =>
    // Lazy web stub — every method resolves without doing anything.
    // The Angular service (game-location.service.ts) takes over in the browser.
    import('./game-location-web-stub').then((m) => new m.GameLocationWebStub()),
});
