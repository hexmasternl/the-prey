import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '@auth0/auth0-angular';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { IonContent, IonButton } from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, switchMap, take } from 'rxjs/operators';
import { getCallbackUri } from '../auth.utils';

@Component({
  selector: 'app-login',
  templateUrl: 'login.page.html',
  styleUrls: ['login.page.scss'],
  imports: [IonContent, IonButton, TranslatePipe],
})
export class LoginPage implements OnInit {
  constructor(
    private authService: AuthService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    // Wait for the SDK to finish its silent token check (using the stored refresh
    // token) before deciding whether to redirect. Taking the first emission of
    // isAuthenticated$ directly would always see `false` because the refresh-token
    // exchange hasn't completed yet.
    this.authService.isLoading$.pipe(
      filter((isLoading) => !isLoading),
      take(1),
      switchMap(() => this.authService.isAuthenticated$),
      take(1),
    ).subscribe((authenticated) => {
      if (authenticated) {
        this.router.navigate(['/home'], { replaceUrl: true });
      }
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
