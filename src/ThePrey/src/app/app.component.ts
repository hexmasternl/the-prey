import { Component, NgZone, OnInit, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { IonApp, IonRouterOutlet, IonSpinner } from '@ionic/angular/standalone';
import { AuthService, IdToken } from '@auth0/auth0-angular';
import { App, URLOpenListenerEvent } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { filter, mergeMap, switchMap, take } from 'rxjs/operators';
import { nativeCallbackUri } from './auth.utils';
import { UserStateService } from './users/user-state.service';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  styleUrls: ['app.component.scss'],
  imports: [IonApp, IonRouterOutlet, IonSpinner],
})
export class AppComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly ngZone = inject(NgZone);
  private readonly router = inject(Router);
  private readonly userState = inject(UserStateService);

  private readonly authLoading = toSignal(this.authService.isLoading$, { initialValue: true });
  private readonly authenticated = toSignal(this.authService.isAuthenticated$, { initialValue: false });

  /**
   * Hide the router outlet (and show the boot spinner) when:
   *   - Auth0 SDK is still initialising, OR
   *   - The user is authenticated but the server sync hasn't finished yet.
   * Unauthenticated users see the outlet immediately so the login page can render.
   */
  readonly showContent = computed(() =>
    !this.authLoading() && (!this.authenticated() || !this.userState.isSyncing())
  );

  ngOnInit(): void {
    // Once Auth0 is ready and the user is authenticated, trigger the global
    // user-profile sync (POST /users + IndexedDB cache).
    this.authService.isLoading$.pipe(
      filter(loading => !loading),
      take(1),
      switchMap(() => this.authService.idTokenClaims$),
      take(1),
      filter((claims): claims is IdToken => claims != null),
    ).subscribe(claims => {
      this.userState.init(claims);
    });

    if (!Capacitor.isNativePlatform()) return;

    // Handle cold-start deep link (Android: app not in memory when tapped)
    App.getLaunchUrl().then((result) => {
      if (!result?.url) return;
      const url = result.url;
      if (url.startsWith(nativeCallbackUri)) {
        this.ngZone.run(() => {
          this.authService.handleRedirectCallback(url).pipe(
            mergeMap(() => Browser.close()),
          ).subscribe();
        });
      } else {
        // Game join deep link: nl.hexmaster.theprey://join?gameId=<id>
        const gameIdMatch = url.match(/[?&]gameId=([^&]+)/);
        if (gameIdMatch?.[1]) {
          this.ngZone.run(() => {
            this.router.navigate(['/games/join'], { queryParams: { gameId: gameIdMatch[1] } });
          });
        }
      }
    });

    // Handle foreground deep link
    App.addListener('appUrlOpen', ({ url }: URLOpenListenerEvent) => {
      this.ngZone.run(() => {
        if (url.startsWith(nativeCallbackUri)) {
          this.authService.handleRedirectCallback(url).pipe(
            mergeMap(() => Browser.close()),
          ).subscribe();
        } else {
          // Game join deep link: nl.hexmaster.theprey://join?gameId=<id>
          const gameIdMatch = url.match(/[?&]gameId=([^&]+)/);
          if (gameIdMatch?.[1]) {
            this.router.navigate(['/games/join'], { queryParams: { gameId: gameIdMatch[1] } });
          }
        }
      });
    });
  }
}
