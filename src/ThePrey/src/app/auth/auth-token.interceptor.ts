import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '@auth0/auth0-angular';
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
    switchMap(token =>
      next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })),
    ),
    catchError(() => next(req)),
  );
};
