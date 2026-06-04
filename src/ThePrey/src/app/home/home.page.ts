import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { IonButton, IonContent, IonSpinner } from '@ionic/angular/standalone';
import { App } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { Geolocation, WatchPositionCallback } from '@capacitor/geolocation';
import { AuthService, IdToken } from '@auth0/auth0-angular';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, switchMap, take } from 'rxjs/operators';
import { getCallbackUri } from '../auth.utils';
import { UserStateService } from '../users/user-state.service';

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
  imports: [IonButton, IonContent, IonSpinner, TranslatePipe],
})
export class HomePage implements OnInit, OnDestroy {
  /** Last known position, formatted to 4 decimals; null until the first GPS fix. */
  lat: string | null = null;
  lon: string | null = null;

  private watchId: string | undefined;

  constructor(
    readonly authService: AuthService,
    readonly userState: UserStateService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    // Wait for the Auth0 SDK to finish its silent-token check, then fetch the
    // ID token claims and kick off the user-profile init (cache restore + server sync).
    this.authService.isLoading$.pipe(
      filter(loading => !loading),
      take(1),
      switchMap(() => this.authService.idTokenClaims$),
      take(1),
      filter((claims): claims is IdToken => claims != null),
    ).subscribe(claims => {
      this.userState.init(claims);
    });

    this.startLocationWatch();
  }

  ngOnDestroy(): void {
    if (this.watchId) {
      Geolocation.clearWatch({ id: this.watchId });
    }
  }

  private async startLocationWatch(): Promise<void> {
    try {
      const permission = await Geolocation.requestPermissions();
      if (permission.location !== 'granted') return;

      this.watchId = await Geolocation.watchPosition(
        { enableHighAccuracy: false, timeout: 10000 },
        this.onPosition,
      );
    } catch {
      // Permission denied or unavailable — coords stay at fallback
    }
  }

  private readonly onPosition: WatchPositionCallback = (position, err) => {
    if (err || !position) return;
    this.lat = position.coords.latitude.toFixed(4);
    this.lon = position.coords.longitude.toFixed(4);
  };

  goToPlay(): void {
    this.router.navigate(['/play']);
  }

  goToPlayfields(): void {
    this.router.navigate(['/playfields']);
  }

  goToSettings(): void {
    this.router.navigate(['/settings']);
  }

  async logout(): Promise<void> {
    await this.userState.clear();
    this.authService.logout({
      logoutParams: { returnTo: getCallbackUri() },
      ...(Capacitor.isNativePlatform() && {
        async openUrl(url: string) {
          await Browser.open({ url, windowName: '_self' });
        },
      }),
    });
  }

  retrySync(): void {
    this.authService.idTokenClaims$.pipe(
      take(1),
      filter((claims): claims is IdToken => claims != null),
    ).subscribe(claims => {
      this.userState.init(claims);
    });
  }

  quit(): void {
    App.exitApp();
  }
}
