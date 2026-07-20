import { inject, Injectable } from '@angular/core';
import { App } from '@capacitor/app';
import { Capacitor } from '@capacitor/core';
import { Preferences } from '@capacitor/preferences';
import { AlertController } from '@ionic/angular/standalone';
import { TranslateService } from '@ngx-translate/core';

/** Preferences key recording the one-time, permanent acceptance of the disclosure below. */
const PREF_CONSENT_ACCEPTED = 'location.consent.accepted';

export type LocationConsentPlatform = 'android' | 'ios' | 'web';

/**
 * Backs the one-time, app-entry background-location consent gate (Google Play Prominent
 * Disclosure & Consent policy). Before the home menu is ever shown, the player must accept
 * a prominent in-app disclosure explaining that active games run a background service which
 * collects and reports their device location. Unlike a per-game prompt, acceptance here is
 * permanent — persisted to `PREF_CONSENT_ACCEPTED` and never re-shown once granted.
 *
 * This gate is purely about the *disclosure*; it does not touch the OS location-permission
 * request itself, which still fires — unexplained in-app, as before — the moment a game
 * actually starts background tracking (see `GameLocationService.start`).
 *
 * Consumed by `locationConsentGuard` (the route gate) and `ConsentRequiredPage` (the
 * non-dismissable fallback for platforms that can't hard-exit on decline).
 */
@Injectable({ providedIn: 'root' })
export class LocationConsentService {
  private readonly alertCtrl = inject(AlertController);
  private readonly translate = inject(TranslateService);

  /**
   * 'android' | 'ios' | 'web' — drives the decline behaviour: only native Android can
   * hard-exit the app (`App.exitApp()`); iOS rejects that pattern at App Review and a
   * browser tab can't close itself, so both fall back to the full-screen consent wall.
   */
  readonly platform: LocationConsentPlatform = Capacitor.getPlatform() as LocationConsentPlatform;

  /** Whether the player has already accepted the disclosure, on any previous launch. */
  async hasConsent(): Promise<boolean> {
    const { value } = await Preferences.get({ key: PREF_CONSENT_ACCEPTED });
    return value === 'true';
  }

  /** Persist acceptance for good — per the one-time-gate design, this is never re-checked. */
  async grantConsent(): Promise<void> {
    await Preferences.set({ key: PREF_CONSENT_ACCEPTED, value: 'true' });
  }

  /**
   * Present the prominent disclosure and resolve once the player has responded. Styled
   * through the shared `tp-overlay` overlay class (`global.scss`); all copy comes from the
   * `LOCATION_CONSENT` i18n namespace — no hard-coded/unlocalized strings. Non-dismissable:
   * `backdropDismiss: false` and no close affordance — DECLINE (`role: 'cancel'`) and ALLOW
   * are the only ways to resolve it.
   */
  async presentDisclosure(): Promise<'allow' | 'decline'> {
    const [header, message, allowText, declineText] = await Promise.all([
      this.translate.get('LOCATION_CONSENT.TITLE').toPromise(),
      this.translate.get('LOCATION_CONSENT.BODY').toPromise(),
      this.translate.get('LOCATION_CONSENT.ALLOW').toPromise(),
      this.translate.get('LOCATION_CONSENT.DECLINE').toPromise(),
    ]);

    return new Promise<'allow' | 'decline'>((resolve) => {
      void this.alertCtrl
        .create({
          header,
          message,
          cssClass: 'tp-overlay',
          backdropDismiss: false,
          buttons: [
            { text: declineText, role: 'cancel', handler: () => resolve('decline') },
            { text: allowText, handler: () => resolve('allow') },
          ],
        })
        .then((alert) => alert.present());
    });
  }

  /**
   * Android-only hard exit on decline. Never called on iOS/web — Apple rejects `exitApp()`
   * calls at review, and a browser tab cannot close itself; both route to the consent wall
   * instead (see `locationConsentGuard`).
   */
  async exitApp(): Promise<void> {
    await App.exitApp();
  }
}
