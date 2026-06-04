import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';

/** Placeholder until the full playfield detail/edit page is implemented. */
@Component({
  selector: 'app-playfield-detail',
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button fill="clear" (click)="back()">&#8592;</ion-button>
        </ion-buttons>
        <ion-title>Playfield</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content class="ion-padding">
      <p>Playfield detail — coming soon.</p>
    </ion-content>
  `,
  imports: [IonHeader, IonToolbar, IonTitle, IonButtons, IonButton, IonContent],
})
export class PlayfieldDetailPage {
  private readonly router = inject(Router);

  back(): void {
    this.router.navigate(['/playfields']);
  }
}
