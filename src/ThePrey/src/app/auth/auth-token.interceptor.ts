import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '@auth0/auth0-angular';
import { throwError } from 'rxjs';
import { catchError, switchMap, take } from 'rxjs/operators';
import { environment } from '../../environments/environment';

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
    // longer valid: surface the error (so the caller / global error handler can react and the
    // user can re-authenticate) instead of silently retrying the request unauthenticated.
    catchError(err => throwError(() => err)),
    switchMap(token =>
      next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })),
    ),
  );
};
