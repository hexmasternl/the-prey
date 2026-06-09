import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '@auth0/auth0-angular';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { IonContent, IonButton } from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, take } from 'rxjs/operators';
import { getCallbackUri } from '../auth.utils';

@Component({
  selector: 'app-login',
  templateUrl: 'login.page.html',
  styleUrls: ['login.page.scss'],
  imports: [IonContent, IonButton, TranslatePipe],
})
export class LoginPage implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);

  constructor(
    private authService: AuthService,
    private router: Router,
  ) {}

  /**
   * Where to go once authenticated. Defaults to /home, but a caller (e.g. the game-join
   * page redirecting an unauthenticated visitor) can pass ?returnUrl= to be sent back to
   * the page they came from. Only same-app paths are honoured to avoid open redirects.
   */
  private returnUrl(): string {
    const target = this.route.snapshot.queryParamMap.get('returnUrl');
    return target && target.startsWith('/') ? target : '/home';
  }

  ngOnInit(): void {
    // Navigate to /home on the first `true` from isAuthenticated$. This fires both
    // when the SDK's silent refresh-token check restores an existing session at
    // startup AND when the redirect callback completes after a fresh login — the
    // latter happens after ngOnInit has already run, so we must keep listening
    // rather than reading auth state once. Filtering for `true` lets us ignore the
    // initial `false` without gating on isLoading$.
    this.authService.isAuthenticated$.pipe(
      filter((authenticated) => authenticated),
      take(1),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => {
      this.router.navigateByUrl(this.returnUrl(), { replaceUrl: true });
    });
  }

  login(): void {
    this.authService.loginWithRedirect({
      authorizationParams: { redirect_uri: getCallbackUri() },
      ...(Capacitor.isNativePlatform() && {
        async openUrl(url: string) {
          await Browser.open({ url, windowName: '_self' });
        },
      }),
    });
  }
}
