import { Injectable, signal } from '@angular/core';

/**
 * Exposes the device compass heading (degrees clockwise from north, 0 = facing north)
 * as a signal, sourced from the browser DeviceOrientation API. Works inside the
 * Capacitor WebView without an extra native plugin.
 *
 * - iOS reports a ready-made compass bearing via the non-standard `webkitCompassHeading`,
 *   but first requires an explicit permission grant (`requestPermission`).
 * - Android exposes `deviceorientationabsolute`, whose `alpha` increases counter-clockwise
 *   from north, so the heading is `360 - alpha`.
 *
 * When neither is available the heading stays `null` and callers should leave the
 * direction arrow pointing north rather than spinning randomly.
 */
@Injectable({ providedIn: 'root' })
export class CompassService {
  /** Device heading in degrees clockwise from north (0 = north), or null if unavailable. */
  readonly heading = signal<number | null>(null);

  private listening = false;
  private readonly handler = (event: Event): void =>
    this.onOrientation(event as DeviceOrientationEvent);

  /**
   * Begin listening for orientation changes. On iOS this prompts for permission and must
   * therefore be reachable from a user gesture; if the grant is refused (or the API is
   * unavailable) it resolves quietly and `heading` simply stays null.
   */
  async start(): Promise<void> {
    if (this.listening) return;

    // iOS 13+ gates the orientation events behind an explicit permission grant.
    const orientationEvent = DeviceOrientationEvent as unknown as {
      requestPermission?: () => Promise<PermissionState>;
    };
    if (typeof orientationEvent.requestPermission === 'function') {
      try {
        const result = await orientationEvent.requestPermission();
        if (result !== 'granted') return;
      } catch {
        return;
      }
    }

    // Prefer the absolute (true-north referenced) event where the platform offers it.
    const hasAbsolute = 'ondeviceorientationabsolute' in window;
    if (hasAbsolute) {
      window.addEventListener('deviceorientationabsolute', this.handler, true);
    } else {
      window.addEventListener('deviceorientation', this.handler, true);
    }
    this.listening = true;
  }

  stop(): void {
    if (!this.listening) return;
    window.removeEventListener('deviceorientationabsolute', this.handler, true);
    window.removeEventListener('deviceorientation', this.handler, true);
    this.listening = false;
  }

  private onOrientation(event: DeviceOrientationEvent): void {
    let heading: number | null = null;

    const webkitHeading = (event as unknown as { webkitCompassHeading?: number })
      .webkitCompassHeading;
    if (typeof webkitHeading === 'number' && !Number.isNaN(webkitHeading)) {
      // iOS: already a compass bearing (0 = north, growing clockwise).
      heading = webkitHeading;
    } else if (event.absolute && event.alpha !== null) {
      // Android absolute: alpha grows counter-clockwise from north.
      heading = 360 - event.alpha;
    }

    if (heading === null) return;
    this.heading.set(((heading % 360) + 360) % 360);
  }
}
