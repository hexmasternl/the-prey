import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService, IdToken } from '@auth0/auth0-angular';
import { Observable, of } from 'rxjs';
import { AppComponent } from './app.component';
import { UserStateService } from './users/user-state.service';

function setup(
  isAuthenticated$: Observable<boolean>,
  idTokenClaims$: Observable<IdToken | null> = of(null),
) {
  const auth = jasmine.createSpyObj<AuthService>(
    'AuthService',
    ['handleRedirectCallback'],
    {
      isLoading$: of(false),
      isAuthenticated$,
      idTokenClaims$,
      user$: of(null),
    },
  );
  const userState = jasmine.createSpyObj<UserStateService>('UserStateService', ['init', 'isSyncing']);

  return TestBed.configureTestingModule({
    imports: [AppComponent],
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: auth },
      { provide: UserStateService, useValue: userState },
    ],
  }).compileComponents();
}

describe('AppComponent', () => {
  it('should create the app', async () => {
    await setup(of(false));

    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should redirect to login when the user logs out', async () => {
    // Emits true (logged in) then false (logged out) — the logout transition.
    await setup(of(true, false));

    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigateByUrl');

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(navigateSpy).toHaveBeenCalledWith('/login', { replaceUrl: true });
  });

  it('should re-sync the profile on every login, not just the first', async () => {
    // login → logout → login again, all within one app process (the native case,
    // where AppComponent is never destroyed). init() must run on BOTH logins.
    const claims = { sub: 'auth0|operative', __raw: 'token' } as unknown as IdToken;
    await setup(of(true, false, true), of(claims));

    const userState = TestBed.inject(UserStateService);

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(userState.init).toHaveBeenCalledTimes(2);
  });

  it('should not redirect on the initial unauthenticated state', async () => {
    // A user who was never logged in: a single `false` must not bounce anywhere.
    await setup(of(false));

    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigateByUrl');

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
