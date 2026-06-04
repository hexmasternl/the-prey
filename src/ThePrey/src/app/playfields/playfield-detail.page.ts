import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonItem,
  IonLabel,
  IonSpinner,
  IonTitle,
  IonToggle,
  IonToolbar,
  ToastController,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { Geolocation } from '@capacitor/geolocation';
import { PlayFieldDetailDto } from './playfield.model';
import { PlayfieldsService } from './playfields.service';
import { PlayfieldMapComponent } from './playfield-map/playfield-map.component';

@Component({
  selector: 'app-playfield-detail',
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-button fill="clear" (click)="back()">← {{ 'PLAYFIELD_DETAIL.BACK' | translate }}</ion-button>
        </ion-buttons>
        <ion-title>{{ 'PLAYFIELD_DETAIL.TITLE' | translate }}</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      @if (isLoading()) {
        <div style="display:flex;justify-content:center;padding-top:48px;">
          <ion-spinner name="crescent" />
        </div>
      } @else if (notFound()) {
        <p>{{ 'PLAYFIELD_DETAIL.NOT_FOUND' | translate }}</p>
        <ion-button fill="clear" (click)="back()">← {{ 'PLAYFIELD_DETAIL.BACK' | translate }}</ion-button>
      } @else if (playfield()) {
        <h2>{{ playfield()!.name }}</h2>

        <ion-item lines="none">
          <ion-label>{{ 'PLAYFIELD_DETAIL.VISIBILITY_LABEL' | translate }}</ion-label>
          <ion-toggle
            slot="end"
            [checked]="playfield()!.isPublic"
            (ionChange)="onVisibilityToggle($event)"
          />
        </ion-item>

        <app-playfield-map
          [coordinates]="playfield()!.points"
          [fallbackCenter]="fallbackCenter()"
        />

        <ion-button expand="block" (click)="goToArea()" style="margin-top:16px;">
          {{ 'PLAYFIELD_DETAIL.SET_AREA' | translate }}
        </ion-button>
      }
    </ion-content>
  `,
  imports: [
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonItem,
    IonLabel,
    IonToggle,
    IonSpinner,
    TranslatePipe,
    PlayfieldMapComponent,
  ],
})
export class PlayfieldDetailPage implements ViewWillEnter {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly toastController = inject(ToastController);

  readonly isLoading = signal(false);
  readonly notFound = signal(false);
  readonly playfield = signal<PlayFieldDetailDto | null>(null);
  readonly fallbackCenter = signal<{ lat: number; lon: number } | null>(null);

  async ionViewWillEnter(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      return;
    }

    this.isLoading.set(true);
    this.notFound.set(false);
    this.playfield.set(null);

    try {
      const data = await this.playfieldsService.getById(id);
      this.playfield.set(data);

      if (!data.points.length) {
        await this.resolveDeviceLocation();
      }
    } catch (err: any) {
      if (err?.status === 404) {
        this.notFound.set(true);
      }
    } finally {
      this.isLoading.set(false);
    }
  }

  async onVisibilityToggle(event: CustomEvent): Promise<void> {
    const current = this.playfield();
    if (!current) return;

    const newValue = event.detail.checked as boolean;
    const previous = current.isPublic;

    this.playfield.set({ ...current, isPublic: newValue });

    try {
      const updated = await this.playfieldsService.patchVisibility(current, newValue);
      this.playfield.set(updated);
    } catch {
      this.playfield.set({ ...current, isPublic: previous });
      const toast = await this.toastController.create({
        message: 'PLAYFIELD_DETAIL.ERROR_VISIBILITY',
        duration: 3000,
        color: 'danger',
        position: 'bottom',
      });
      await toast.present();
    }
  }

  goToArea(): void {
    const id = this.playfield()?.id;
    if (id) {
      this.router.navigate(['/playfields', id, 'area']);
    }
  }

  back(): void {
    this.router.navigate(['/playfields']);
  }

  private async resolveDeviceLocation(): Promise<void> {
    try {
      const permission = await Geolocation.requestPermissions();
      if (permission.location !== 'granted') return;

      const pos = await Geolocation.getCurrentPosition({ timeout: 8000 });
      this.fallbackCenter.set({
        lat: pos.coords.latitude,
        lon: pos.coords.longitude,
      });
    } catch {
      // Permission denied or timeout — map renders at world zoom
    }
  }
}
