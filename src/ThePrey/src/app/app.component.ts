import { Component, NgZone, OnInit } from '@angular/core';
import { IonApp, IonRouterOutlet } from '@ionic/angular/standalone';
import { AuthService } from '@auth0/auth0-angular';
import { App, URLOpenListenerEvent } from '@capacitor/app';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { mergeMap } from 'rxjs/operators';
import { nativeCallbackUri } from './auth.utils';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  imports: [IonApp, IonRouterOutlet],
})
export class AppComponent implements OnInit {
  constructor(
    private authService: AuthService,
    private ngZone: NgZone,
  ) {}

  ngOnInit(): void {
    if (!Capacitor.isNativePlatform()) return;

    // Handle cold-start deep link (Android: app not in memory when tapped)
    App.getLaunchUrl().then((result) => {
      if (result?.url?.startsWith(nativeCallbackUri)) {
        this.ngZone.run(() => {
          this.authService.handleRedirectCallback(result.url).pipe(
            mergeMap(() => Browser.close()),
          ).subscribe();
        });
      }
    });

    // Handle foreground deep link
    App.addListener('appUrlOpen', ({ url }: URLOpenListenerEvent) => {
      this.ngZone.run(() => {
        if (url.startsWith(nativeCallbackUri)) {
          this.authService.handleRedirectCallback(url).pipe(
            mergeMap(() => Browser.close()),
          ).subscribe();
        }
      });
    });
  }
}
