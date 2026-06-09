import { TestBed } from '@angular/core/testing';
import { HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { AuthService } from '@auth0/auth0-angular';
import { Observable, firstValueFrom, of, throwError } from 'rxjs';
import { authTokenInterceptor } from './auth-token.interceptor';
import { environment } from '../../environments/environment';

describe('authTokenInterceptor', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let injector: EnvironmentInjector;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', [
      'getAccessTokenSilently',
      'loginWithRedirect',
    ]);
    auth.loginWithRedirect.and.returnValue(of(undefined) as unknown as Observable<void>);
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: auth }],
    });
    injector = TestBed.inject(EnvironmentInjector);
  });

  function run(req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
    return runInInjectionContext(injector, () => authTokenInterceptor(req, next));
  }

  it('passes non-API requests through without requesting a token', async () => {
    let seen: HttpRequest<unknown> | undefined;
    const next: HttpHandlerFn = (r) => { seen = r; return of({} as HttpEvent<unknown>); };

    await firstValueFrom(run(new HttpRequest('GET', 'https://cdn.example.com/i18n/en.json'), next));

    expect(auth.getAccessTokenSilently).not.toHaveBeenCalled();
    expect(seen!.headers.has('Authorization')).toBeFalse();
  });

  it('attaches a bearer token to API requests', async () => {
    auth.getAccessTokenSilently.and.returnValue(of('tok123') as unknown as Observable<string>);
    let seen: HttpRequest<unknown> | undefined;
    const next: HttpHandlerFn = (r) => { seen = r; return of({} as HttpEvent<unknown>); };

    await firstValueFrom(run(new HttpRequest('GET', `${environment.apiUrl}/games`), next));

    expect(seen!.headers.get('Authorization')).toBe('Bearer tok123');
  });

  it('propagates a failed token refresh instead of sending the request unauthenticated', async () => {
    auth.getAccessTokenSilently.and.returnValue(
      throwError(() => new Error('login_required')) as unknown as Observable<string>,
    );
    const next = jasmine.createSpy('next').and.returnValue(of({} as HttpEvent<unknown>));

    await expectAsync(
      firstValueFrom(run(new HttpRequest('GET', `${environment.apiUrl}/games`), next as unknown as HttpHandlerFn)),
    ).toBeRejected();

    expect(next).not.toHaveBeenCalled();
    expect(auth.loginWithRedirect).not.toHaveBeenCalled();
  });

  it('starts a fresh login when the session has no refresh token, and still rejects', async () => {
    auth.getAccessTokenSilently.and.returnValue(
      throwError(() => new Error('Missing Refresh Token')) as unknown as Observable<string>,
    );
    const next = jasmine.createSpy('next').and.returnValue(of({} as HttpEvent<unknown>));

    await expectAsync(
      firstValueFrom(run(new HttpRequest('GET', `${environment.apiUrl}/games`), next as unknown as HttpHandlerFn)),
    ).toBeRejected();

    expect(auth.loginWithRedirect).toHaveBeenCalledTimes(1);
    expect(next).not.toHaveBeenCalled();
  });
});
