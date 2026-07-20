import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';

import { locationConsentGuard } from './location-consent.guard';
import { LocationConsentPlatform, LocationConsentService } from './location-consent.service';

/**
 * `LocationConsentService` wraps every Capacitor plugin call the guard needs
 * (`@capacitor/preferences`, `@capacitor/app`) plus `AlertController`, so the guard itself
 * can be tested by mocking just that one injected class — no need to fight the
 * `registerPlugin(...)` proxy issue (its `get` trap re-creates a method wrapper on every
 * access, which defeats `spyOn` on a plugin object directly).
 */
function createConsentServiceSpy(platform: LocationConsentPlatform) {
  const spy = jasmine.createSpyObj<LocationConsentService>('LocationConsentService', [
    'hasConsent',
    'grantConsent',
    'presentDisclosure',
    'exitApp',
  ]);
  (spy as { platform: LocationConsentPlatform }).platform = platform;
  spy.grantConsent.and.resolveTo();
  spy.exitApp.and.resolveTo();
  return spy;
}

function runGuard(): boolean | UrlTree | Promise<boolean | UrlTree> {
  return TestBed.runInInjectionContext(() =>
    locationConsentGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
  ) as boolean | UrlTree | Promise<boolean | UrlTree>;
}

describe('locationConsentGuard', () => {
  let router: jasmine.SpyObj<Router>;

  function configure(platform: LocationConsentPlatform): jasmine.SpyObj<LocationConsentService> {
    const consent = createConsentServiceSpy(platform);
    router = jasmine.createSpyObj<Router>('Router', ['parseUrl']);

    TestBed.configureTestingModule({
      providers: [
        { provide: LocationConsentService, useValue: consent },
        { provide: Router, useValue: router },
      ],
    });

    return consent;
  }

  it('allows navigation without showing the disclosure when consent was already recorded', async () => {
    const consent = configure('android');
    consent.hasConsent.and.resolveTo(true);

    const result = await runGuard();

    expect(result).toBeTrue();
    expect(consent.presentDisclosure).not.toHaveBeenCalled();
  });

  it('persists consent and allows navigation once the player allows the disclosure', async () => {
    const consent = configure('android');
    consent.hasConsent.and.resolveTo(false);
    consent.presentDisclosure.and.resolveTo('allow');

    const result = await runGuard();

    expect(consent.grantConsent).toHaveBeenCalled();
    expect(consent.exitApp).not.toHaveBeenCalled();
    expect(result).toBeTrue();
  });

  it('exits the app and blocks navigation when the player declines on native Android', async () => {
    const consent = configure('android');
    consent.hasConsent.and.resolveTo(false);
    consent.presentDisclosure.and.resolveTo('decline');

    const result = await runGuard();

    expect(consent.exitApp).toHaveBeenCalled();
    expect(consent.grantConsent).not.toHaveBeenCalled();
    expect(result).toBeFalse();
  });

  it('redirects to the consent wall instead of exiting when the player declines on iOS', async () => {
    const consent = configure('ios');
    consent.hasConsent.and.resolveTo(false);
    consent.presentDisclosure.and.resolveTo('decline');
    const wallUrlTree = {} as UrlTree;
    router.parseUrl.and.returnValue(wallUrlTree);

    const result = await runGuard();

    expect(consent.exitApp).not.toHaveBeenCalled();
    expect(router.parseUrl).toHaveBeenCalledWith('/consent-required');
    expect(result).toBe(wallUrlTree);
  });

  it('redirects to the consent wall instead of exiting when the player declines on web/PWA', async () => {
    const consent = configure('web');
    consent.hasConsent.and.resolveTo(false);
    consent.presentDisclosure.and.resolveTo('decline');
    const wallUrlTree = {} as UrlTree;
    router.parseUrl.and.returnValue(wallUrlTree);

    const result = await runGuard();

    expect(consent.exitApp).not.toHaveBeenCalled();
    expect(router.parseUrl).toHaveBeenCalledWith('/consent-required');
    expect(result).toBe(wallUrlTree);
  });
});
