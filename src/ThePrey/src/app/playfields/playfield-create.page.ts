import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Geolocation } from '@capacitor/geolocation';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonLabel,
  IonList,
  IonSpinner,
  IonTitle,
  IonToggle,
  IonToolbar,
} from '@ionic/angular/standalone';
import { DecimalPipe } from '@angular/common';
import { GpsCoordinateDto } from './playfield.model';
import { PlayfieldsService } from './playfields.service';
import { UserStateService } from '../users/user-state.service';

@Component({
  selector: 'app-playfield-create',
  templateUrl: 'playfield-create.page.html',
  styleUrls: ['playfield-create.page.scss'],
  imports: [
    FormsModule,
    DecimalPipe,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonItem,
    IonInput,
    IonLabel,
    IonToggle,
    IonList,
    IonSpinner,
  ],
})
export class PlayfieldCreatePage {
  private readonly router = inject(Router);
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly userState = inject(UserStateService);

  readonly name = signal('');
  readonly isPublic = signal(false);
  readonly points = signal<GpsCoordinateDto[]>([]);
  readonly isCapturing = signal(false);
  readonly isSaving = signal(false);

  readonly canSave = computed(
    () => this.name().trim().length > 0 && this.points().length >= 3,
  );

  async addCurrentLocation(): Promise<void> {
    this.isCapturing.set(true);
    try {
      const position = await Geolocation.getCurrentPosition({ enableHighAccuracy: true });
      this.points.update((list) => [
        ...list,
        {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
        },
      ]);
    } finally {
      this.isCapturing.set(false);
    }
  }

  removePoint(index: number): void {
    this.points.update((list) => list.filter((_, i) => i !== index));
  }

  async save(): Promise<void> {
    if (!this.canSave()) return;
    const ownerId = this.userState.profile()?.userId;
    if (!ownerId) return;

    this.isSaving.set(true);
    try {
      await this.playfieldsService.createLocal(
        this.name().trim(),
        this.isPublic(),
        this.points(),
        ownerId,
      );
      await this.router.navigate(['/playfields']);
    } finally {
      this.isSaving.set(false);
    }
  }

  back(): void {
    this.router.navigate(['/playfields']);
  }
}
