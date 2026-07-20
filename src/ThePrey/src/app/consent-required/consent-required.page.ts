import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { IonContent, IonButton } from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { LocationConsentService } from '../core/location-consent.service';

/**
 * Full-screen, non-dismissable fallback shown when the player declines the background-
 * location disclosure on native iOS or web/PWA — platforms that can't hard-exit the app the
 * way native Android does (`App.exitApp()` is rejected by Apple review, and a browser tab
 * cannot close itself). There is no back button, close icon, or swipe-to-dismiss; the only
 * affordance is Accept, which persists consent and continues to the home menu.
 */
@Component({
  selector: 'app-consent-required',
  templateUrl: 'consent-required.page.html',
  styleUrls: ['consent-required.page.scss'],
  imports: [IonContent, IonButton, TranslatePipe],
})
export class ConsentRequiredPage {
  private readonly consent = inject(LocationConsentService);
  private readonly router = inject(Router);

  async accept(): Promise<void> {
    await this.consent.grantConsent();
    await this.router.navigateByUrl('/home');
  }
}
