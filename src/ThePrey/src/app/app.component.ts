import { Component, NgZone, OnInit, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { IonApp, IonRouterOutlet, IonSpinner } from '@ionic/angular/standalone';
import { AuthService, IdToken } from '@auth0/auth0-angular';
import { App, URLOpenListenerEvent } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { filter, mergeMap, pairwise, switchMap, take } from 'rxjs/operators';
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
    // Trigger the global user-profile sync (POST /users + IndexedDB cache)
    // whenever the user becomes authenticated. AppComponent is the root
    // component and lives for the whole app process, so we must NOT take(1) on
    // the outer stream: after a logout→login round-trip (the session ends,
    // isAuthenticated$ flips to false, then true again on the next login — on
    // native this happens in the SAME process with no page reload) a one-shot
    // subscription would never re-run, leaving the home page stuck on its
    // spinner with a null profile (cleared on logout). Re-subscribing per
    // authentication, and taking the first non-null claims INSIDE switchMap,
    // re-syncs on every login while still calling init() exactly once per login.
    // The inner filter(non-null) is essential: at the moment Auth0 finishes
    // loading idTokenClaims$ may emit null first; taking that null would
    // complete the inner stream before the real claims arrive.
    this.authService.isAuthenticated$.pipe(
      filter(authenticated => authenticated),
      switchMap(() => this.authService.idTokenClaims$.pipe(
        filter((claims): claims is IdToken => claims != null),
        take(1),
      )),
    ).subscribe(claims => {
      this.userState.init(claims);
    });

    // Send the user back to the login page whenever the session ends. On logout
    // the Auth0 SDK flips isAuthenticated$ to false; without an explicit redirect
    // the (now unauthenticated) user is left on the home page, where the profile
    // is null and the page hangs on its loading spinner forever. pairwise() limits
    // this to a real authenticated → unauthenticated transition, so the initial
    // `false` at startup (before login) and the join-deep-link flow are untouched.
    this.authService.isAuthenticated$.pipe(
      pairwise(),
      filter(([wasAuthenticated, isAuthenticated]) => wasAuthenticated && !isAuthenticated),
    ).subscribe(() => {
      this.router.navigateByUrl('/login', { replaceUrl: true });
    });

    if (!Capacitor.isNativePlatform()) return;

    // Handle cold-start deep link (Android: app not in memory when tapped)
    App.getLaunchUrl().then((result) => {
      if (result?.url) this.handleDeepLink(result.url);
    });

    // Handle foreground deep link
    App.addListener('appUrlOpen', ({ url }: URLOpenListenerEvent) => {
      this.handleDeepLink(url);
    });
  }

  private handleDeepLink(url: string): void {
    this.ngZone.run(() => {
      if (url.startsWith(nativeCallbackUri)) {
        this.authService.handleRedirectCallback(url).pipe(
          mergeMap(() => Browser.close()),
        ).subscribe({
          error: (err) => console.error('Auth0 handleRedirectCallback failed', err),
        });
      } else {
        // Game join App Link, e.g. https://theprey.nl/games/join/<id> (the OS routes
        // these in via autoVerify). Older links used a ?gameId=<id> query param —
        // still supported as a fallback.
        const pathMatch = url.match(/\/games\/join\/([^/?#]+)/);
        const queryMatch = url.match(/[?&]gameId=([^&]+)/);
        const gameId = pathMatch?.[1] ?? queryMatch?.[1];
        if (gameId) {
          this.router.navigate(['/games/join'], {
            queryParams: { gameId: decodeURIComponent(gameId) },
          });
        }
      }
    });
  }
}
