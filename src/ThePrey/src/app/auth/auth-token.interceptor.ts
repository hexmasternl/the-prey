import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '@auth0/auth0-angular';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { throwError } from 'rxjs';
import { catchError, switchMap, take } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { getCallbackUri } from '../auth.utils';

/**
 * Auth0 error codes (plus the "Missing Refresh Token" GenericError) that can only be cleared by
 * an interactive login: there is no usable access token and no refresh token to silently mint
 * one. A "zombie" session — isAuthenticated$ === true but with no refresh token, e.g. a session
 * created before the API allowed offline access — fails this way on every API call.
 */
const RECOVERABLE_AUTH_ERRORS = new Set([
  'login_required',
  'missing_refresh_token',
  'consent_required',
  'interaction_required',
]);

function needsInteractiveLogin(err: unknown): boolean {
  if (!err || typeof err !== 'object') return false;
  const e = err as { error?: unknown; message?: unknown };
  if (typeof e.error === 'string' && RECOVERABLE_AUTH_ERRORS.has(e.error)) return true;
  return typeof e.message === 'string' && e.message.toLowerCase().includes('missing refresh token');
}

// Module-level latch so a burst of parallel requests that all fail token acquisition triggers
// exactly one interactive login, not one per in-flight request. Cleared once a token succeeds.
let recovering = false;

/**
 * Attaches an Auth0 Bearer token to every request that targets the API.
 * Requests to other origins (i18n assets, CDNs, etc.) are passed through unchanged.
 */
export const authTokenInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  const auth = inject(AuthService);

  return auth.getAccessTokenSilently().pipe(
    take(1),
    // catchError sits BEFORE switchMap so it only handles a failed token acquisition, not
    // errors from the downstream HTTP call. When the silent refresh fails the session is no
    // longer valid. If the failure is one only an interactive login can fix (a zombie session
    // with no refresh token), start a single fresh login to replace the dead session so the app
    // can't get stuck "authenticated but tokenless". The error is still surfaced either way so
    // the caller / global error handler can react and the request doesn't go out unauthenticated.
    catchError((err) => {
      if (needsInteractiveLogin(err) && !recovering) {
        recovering = true;
        auth
          .loginWithRedirect({
            authorizationParams: { redirect_uri: getCallbackUri() },
            ...(Capacitor.isNativePlatform() && {
              async openUrl(url: string) {
                await Browser.open({ url, windowName: '_self' });
              },
            }),
          })
          .subscribe({ error: () => { recovering = false; } });
      }
      return throwError(() => err);
    }),
    switchMap((token) => {
      recovering = false;
      return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
    }),
  );
};
