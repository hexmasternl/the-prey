import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastController } from '@ionic/angular/standalone';
import { TranslateService } from '@ngx-translate/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

/** Suppress duplicate toasts that would otherwise stack when several calls fail together. */
let lastToastAt = 0;
const TOAST_THROTTLE_MS = 4_000;

/**
 * Surfaces otherwise-silent API failures to the user as a toast, then rethrows so callers
 * keep their own handling. Only network loss (status 0) and server errors (5xx) are toasted;
 * routine 4xx responses (validation, conflicts, forbidden) are left to the page that made the
 * call, which can show contextual messaging. Errors from non-API origins are ignored.
 *
 * Only genuine HTTP failures (HttpErrorResponse) are classified. Non-HTTP errors — most
 * notably a failed Auth0 silent token refresh thrown by authTokenInterceptor, which this
 * interceptor wraps — never reach the wire and must NOT be reported as a lost connection;
 * they are rethrown untouched so the auth flow can prompt re-authentication.
 */
export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  const toasts = inject(ToastController);
  const translate = inject(TranslateService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        if (error.status === 0 || error.status >= 500) {
          void presentToast(toasts, translate, error.status === 0 ? 'ERRORS.NETWORK' : 'ERRORS.SERVER');
        }
      }
      return throwError(() => error);
    }),
  );
};

async function presentToast(
  toasts: ToastController,
  translate: TranslateService,
  key: string,
): Promise<void> {
  const now = Date.now();
  if (now - lastToastAt < TOAST_THROTTLE_MS) {
    return;
  }
  lastToastAt = now;

  // Translations are loaded at app startup (LanguageService.init), so instant() is safe here.
  const toast = await toasts.create({
    message: translate.instant(key),
    duration: 3_000,
    position: 'top',
    color: 'danger',
  });
  await toast.present();
}
