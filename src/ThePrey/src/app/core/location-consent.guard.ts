import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { LocationConsentService } from './location-consent.service';

/**
 * One-time, app-entry gate for the background-location disclosure (Google Play Prominent
 * Disclosure & Consent policy). Applied to `home` and, for deep-link safety, the gameplay
 * entry routes (`games/join`, `games/:id/lobby|play|hunt`) — once consent is persisted this
 * resolves to `true` on every subsequent check, so guarding several routes costs nothing
 * after the first launch.
 *
 * - Consent already recorded (`LocationConsentService.hasConsent`) → allow.
 * - Not recorded → show the disclosure (`presentDisclosure`).
 *   - ALLOW → persist consent, allow.
 *   - DECLINE on native Android → hard-exit the app (`App.exitApp()`) and block navigation.
 *   - DECLINE on iOS native or web/PWA → redirect to the full-screen, non-dismissable
 *     consent wall (`/consent-required`) instead of the originally requested route.
 */
export const locationConsentGuard: CanActivateFn = async () => {
  const consent = inject(LocationConsentService);
  const router = inject(Router);

  if (await consent.hasConsent()) {
    return true;
  }

  const choice = await consent.presentDisclosure();
  if (choice === 'allow') {
    await consent.grantConsent();
    return true;
  }

  if (consent.platform === 'android') {
    await consent.exitApp();
    return false;
  }

  return router.parseUrl('/consent-required');
};
