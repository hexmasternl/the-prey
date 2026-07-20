import { TestBed } from '@angular/core/testing';
import { Capacitor } from '@capacitor/core';
import { AlertController } from '@ionic/angular/standalone';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';

import { GameLocationService } from './game-location.service';
import { GamesService } from './games.service';

/**
 * The private consent-gate helpers (`hasStoredConsent`, `hasOsLocationPermission`) and the
 * plugin-call seams (`addNativeWatcher`) are spied on directly rather than mocking
 * `@capacitor/preferences` / `@capacitor/geolocation` / the registered `BackgroundGeolocation`
 * plugin: those are all `registerPlugin(...)` proxies whose `get` trap re-creates a method
 * wrapper on every access, which silently defeats `spyOn` on the plugin object itself (see
 * the JSDoc on `addNativeWatcher` in game-location.service.ts).
 */
interface TestableGameLocationService {
  start(gameId: string, gameEndTime: Date): Promise<void>;
  stop(): Promise<void>;
  isTracking(): boolean;
  hasStoredConsent(): Promise<boolean>;
  hasOsLocationPermission(): Promise<boolean>;
  addNativeWatcher(...args: unknown[]): Promise<string>;
}

/** A future game-end so `start()` never short-circuits on the "already ended" guard. */
const FUTURE_END_TIME = new Date(Date.now() + 60 * 60 * 1000);

/**
 * Fake `AlertController` whose `present()` synchronously invokes the disclosure's DECLINE
 * (`role: 'cancel'`) or ALLOW button handler, simulating the player's tap without a real
 * overlay. `create` is exposed on the returned object so tests can assert whether the
 * disclosure was shown at all.
 */
function createAlertControllerStub(choice: 'allow' | 'decline'): AlertController {
  const create = jasmine.createSpy('create').and.callFake(
    (opts: { buttons: { role?: string; handler?: () => void }[] }) =>
      Promise.resolve({
        present: jasmine.createSpy('present').and.callFake(() => {
          const button =
            choice === 'decline'
              ? opts.buttons.find((b) => b.role === 'cancel')
              : opts.buttons.find((b) => b.role !== 'cancel');
          button?.handler?.();
        }),
      }),
  );
  return { create } as unknown as AlertController;
}

/**
 * Builds a `GameLocationService` on the native branch (`Capacitor.isNativePlatform()`
 * stubbed `true`) with the given disclosure outcome wired into `AlertController`.
 */
function createNativeService(alertChoice: 'allow' | 'decline'): {
  service: TestableGameLocationService;
  alertCreate: jasmine.Spy;
} {
  const alertCtrl = createAlertControllerStub(alertChoice);

  TestBed.configureTestingModule({
    providers: [
      { provide: GamesService, useValue: {} },
      { provide: TranslateService, useValue: { instant: (key: string) => key, get: (key: string) => of(key) } },
      { provide: AlertController, useValue: alertCtrl },
    ],
  });

  spyOn(Capacitor, 'isNativePlatform').and.returnValue(true);

  const service = TestBed.inject(GameLocationService) as unknown as TestableGameLocationService;
  return { service, alertCreate: (alertCtrl as unknown as { create: jasmine.Spy }).create };
}

describe('GameLocationService — background-location consent gate', () => {
  it('shows the disclosure and only calls addWatcher once the player allows it', async () => {
    const { service, alertCreate } = createNativeService('allow');
    spyOn(service, 'hasStoredConsent').and.resolveTo(false);
    spyOn(service, 'hasOsLocationPermission').and.resolveTo(false);
    const addWatcher = spyOn(service, 'addNativeWatcher').and.resolveTo('watcher-1');

    await service.start('game-1', FUTURE_END_TIME);

    expect(alertCreate).toHaveBeenCalled();
    expect(addWatcher).toHaveBeenCalled();
    expect(service.isTracking()).toBeTrue();

    await service.stop();
  });

  it('does not request the OS permission or start tracking when the player declines', async () => {
    const { service, alertCreate } = createNativeService('decline');
    spyOn(service, 'hasStoredConsent').and.resolveTo(false);
    spyOn(service, 'hasOsLocationPermission').and.resolveTo(false);
    const addWatcher = spyOn(service, 'addNativeWatcher').and.resolveTo('watcher-1');

    await service.start('game-1', FUTURE_END_TIME);

    expect(alertCreate).toHaveBeenCalled();
    expect(addWatcher).not.toHaveBeenCalled();
    expect(service.isTracking()).toBeFalse();
  });

  it('skips the disclosure and proceeds straight to addWatcher when consent is stored and the OS permission is still granted', async () => {
    const { service, alertCreate } = createNativeService('decline'); // would fail the test below if (wrongly) shown
    spyOn(service, 'hasStoredConsent').and.resolveTo(true);
    spyOn(service, 'hasOsLocationPermission').and.resolveTo(true);
    const addWatcher = spyOn(service, 'addNativeWatcher').and.resolveTo('watcher-1');

    await service.start('game-1', FUTURE_END_TIME);

    expect(alertCreate).not.toHaveBeenCalled();
    expect(addWatcher).toHaveBeenCalled();
    expect(service.isTracking()).toBeTrue();

    await service.stop();
  });

  it('re-discloses when consent was stored previously but the OS permission is no longer granted', async () => {
    const { service, alertCreate } = createNativeService('allow');
    spyOn(service, 'hasStoredConsent').and.resolveTo(true);
    spyOn(service, 'hasOsLocationPermission').and.resolveTo(false);
    const addWatcher = spyOn(service, 'addNativeWatcher').and.resolveTo('watcher-1');

    await service.start('game-1', FUTURE_END_TIME);

    expect(alertCreate).toHaveBeenCalled();
    expect(addWatcher).toHaveBeenCalled();
    expect(service.isTracking()).toBeTrue();

    await service.stop();
  });
});
