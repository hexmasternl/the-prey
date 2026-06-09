import { ErrorHandler, inject, provideAppInitializer } from '@angular/core';
import { RouteReuseStrategy, provideRouter, withPreloading, PreloadAllModules } from '@angular/router';
import { bootstrapApplication } from '@angular/platform-browser';
import { IonicRouteStrategy, provideIonicAngular } from '@ionic/angular/standalone';
import { provideAuth0 } from '@auth0/auth0-angular';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authTokenInterceptor } from './app/auth/auth-token.interceptor';
import { httpErrorInterceptor } from './app/core/http-error.interceptor';
import { Capacitor } from '@capacitor/core';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { routes } from './app/app.routes';
import { AppComponent } from './app/app.component';
import { LanguageService } from './app/i18n/language.service';
import { DebugErrorHandler } from './app/debug/debug-error-handler';
import { environment } from './environments/environment';

const APP_ID = 'nl.hexmaster.theprey';
const redirectUri = Capacitor.isNativePlatform()
  ? `${APP_ID}://${environment.auth0.domain}/capacitor/${APP_ID}/callback`
  : window.location.origin;

bootstrapApplication(AppComponent, {
  providers: [
    { provide: RouteReuseStrategy, useClass: IonicRouteStrategy },
    { provide: ErrorHandler, useClass: DebugErrorHandler },
    provideIonicAngular(),
    provideRouter(routes, withPreloading(PreloadAllModules)),
    // httpErrorInterceptor is listed first so it wraps authTokenInterceptor and surfaces
    // both downstream HTTP failures and a failed token refresh.
    provideHttpClient(withInterceptors([httpErrorInterceptor, authTokenInterceptor])),
    provideTranslateService({
      loader: provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' }),
      fallbackLang: 'en',
    }),
    provideAppInitializer(() => inject(LanguageService).init()),
    provideAuth0({
      domain: environment.auth0.domain,
      clientId: environment.auth0.clientId,
      useRefreshTokens: true,
      useRefreshTokensFallback: false,
      cacheLocation: 'localstorage',
      authorizationParams: {
        redirect_uri: redirectUri,
        audience: 'https://api.theprey.nl',
      },
    }),
  ],
});
