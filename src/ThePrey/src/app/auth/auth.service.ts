import { Injectable } from '@angular/core';
import { AuthService as Auth0Service } from '@auth0/auth0-angular';
import { Browser } from '@capacitor/browser';
import { BehaviorSubject, Observable } from 'rxjs';
import { environment } from '../../environments/environment';

const appId = 'nl.hexmaster.theprey';
const callbackUri = `${appId}://${environment.auth0.domain}/capacitor/${appId}/callback`;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _isAuthenticated = new BehaviorSubject<boolean>(false);
  readonly isAuthenticated$: Observable<boolean> = this._isAuthenticated.asObservable();

  constructor(private auth0: Auth0Service) {
    this.auth0.isAuthenticated$.subscribe(value => this._isAuthenticated.next(value));
  }

  login(): void {
    this.auth0.loginWithRedirect({
      async openUrl(url: string) {
        await Browser.open({ url, windowName: '_self' });
      },
    }).subscribe();
  }

  logout(): void {
    this.auth0.logout({
      logoutParams: { returnTo: callbackUri },
      async openUrl(url: string) {
        await Browser.open({ url, windowName: '_self' });
      },
    }).subscribe();
  }

  async restoreSession(): Promise<void> {
    try {
      await this.auth0.getAccessTokenSilently().toPromise();
    } catch {
      // No valid session — user stays unauthenticated, no error surfaced
    }
  }
}
