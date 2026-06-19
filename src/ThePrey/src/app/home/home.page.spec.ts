import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { AuthService } from '@auth0/auth0-angular';
import { provideTranslateService } from '@ngx-translate/core';

import { HomePage } from './home.page';
import { UserStateService } from '../users/user-state.service';
import { GamesService, VersionCheckResult } from '../games/games.service';

describe('HomePage', () => {
  let component: HomePage;
  let fixture: ComponentFixture<HomePage>;
  let gamesService: jasmine.SpyObj<GamesService>;

  /** Deferred control over the version check so a test can assert the pending (disabled) state. */
  let resolveVersion: (result: VersionCheckResult) => void;

  beforeEach(async () => {
    const userState = jasmine.createSpyObj<UserStateService>(
      'UserStateService',
      ['init', 'clear'],
      {
        // profile() and syncFailed() are signals — stub them as callable accessors.
        profile: (() => null) as unknown as UserStateService['profile'],
        syncFailed: (() => false) as unknown as UserStateService['syncFailed'],
      },
    );

    gamesService = jasmine.createSpyObj<GamesService>('GamesService', [
      'getActiveGame',
      'checkAppVersion',
    ]);
    gamesService.getActiveGame.and.resolveTo(null);
    gamesService.checkAppVersion.and.returnValue(
      new Promise<VersionCheckResult>((resolve) => {
        resolveVersion = resolve;
      }),
    );

    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        provideTranslateService(),
        { provide: AuthService, useValue: jasmine.createSpyObj<AuthService>('AuthService', ['logout']) },
        { provide: UserStateService, useValue: userState },
        { provide: GamesService, useValue: gamesService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HomePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('blocks the menu while the version check is in flight', () => {
    // checkAppVersion has not resolved yet.
    expect(component.versionChecked()).toBeFalse();
    expect(component.versionBlocked()).toBeTrue();
    expect(component.updateRequired()).toBeFalse();
  });

  it('enables the menu when the version check returns ok', async () => {
    resolveVersion('ok');
    await fixture.whenStable();

    expect(component.versionChecked()).toBeTrue();
    expect(component.updateRequired()).toBeFalse();
    expect(component.versionBlocked()).toBeFalse();
  });

  it('keeps the menu blocked and flags update on a 409 result', async () => {
    resolveVersion('update-required');
    await fixture.whenStable();

    expect(component.versionChecked()).toBeTrue();
    expect(component.updateRequired()).toBeTrue();
    expect(component.versionBlocked()).toBeTrue();
  });
});
