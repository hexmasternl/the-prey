import { WebPlugin } from '@capacitor/core';
import type { GameLocationPlugin, StartTrackingOptions, StartTrackingResult, UpdateIntervalOptions } from './game-location-plugin';

/**
 * No-op web implementation of GameLocationPlugin.
 *
 * The real tracking on web/browser is handled by GameLocationService,
 * which falls back to @capacitor/geolocation + HttpClient. This stub
 * satisfies Capacitor's registerPlugin() contract for the web platform.
 */
export class GameLocationWebStub extends WebPlugin implements GameLocationPlugin {
  startTracking(_options: StartTrackingOptions): Promise<StartTrackingResult> {
    return Promise.resolve({ started: false });
  }

  stopTracking(): Promise<void> {
    return Promise.resolve();
  }

  updateInterval(_options: UpdateIntervalOptions): Promise<void> {
    return Promise.resolve();
  }
}
