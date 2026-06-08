import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AuthService } from '@auth0/auth0-angular';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { IonContent, IonButton } from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, take } from 'rxjs/operators';
import { getCallbackUri } from '../auth.utils';
import { DebugLogService } from '../debug/debug-log.service';

@Component({
  selector: 'app-login',
  templateUrl: 'login.page.html',
  styleUrls: ['login.page.scss'],
  imports: [IonContent, IonButton, TranslatePipe],
})
export class LoginPage implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  protected readonly debug = inject(DebugLogService);

  constructor(
    private authService: AuthService,
    private router: Router,
  ) {}

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
      this.debug.log('LoginPage saw authenticated=true → navigating to /home');
      this.router.navigate(['/home'], { replaceUrl: true })
        .then((ok) => this.debug.log('navigate(/home) result', ok))
        .catch((err) => this.debug.log('navigate(/home) FAILED', err));
    });
  }

  login(): void {
    this.debug.log('login() tapped; redirect_uri', getCallbackUri());
    this.authService.loginWithRedirect({
      authorizationParams: { redirect_uri: getCallbackUri() },
      ...(Capacitor.isNativePlatform() && {
        async openUrl(url: string) {
          await Browser.open({ url, windowName: '_self' });
        },
      }),
    });
  }

  copyLog(): void {
    const ta = document.querySelector<HTMLTextAreaElement>('#debug-log');
    if (!ta) return;
    ta.select();
    document.execCommand('copy');
  }
}
