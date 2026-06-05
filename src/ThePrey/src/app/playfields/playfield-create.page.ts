import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonLabel,
  IonNote,
  IonSpinner,
  IonTitle,
  IonToggle,
  IonToolbar,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { GpsCoordinateDto } from './playfield.model';
import { PlayfieldsService } from './playfields.service';
import { PlayfieldDraftService } from './playfield-draft.service';
import { UserStateService } from '../users/user-state.service';

@Component({
  selector: 'app-playfield-create',
  templateUrl: 'playfield-create.page.html',
  styleUrls: ['playfield-create.page.scss'],
  imports: [
    FormsModule,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonItem,
    IonInput,
    IonLabel,
    IonNote,
    IonToggle,
    IonSpinner,
  ],
})
export class PlayfieldCreatePage implements ViewWillEnter {
  private readonly router = inject(Router);
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly userState = inject(UserStateService);
  private readonly draftService = inject(PlayfieldDraftService);

  readonly name = signal('');
  readonly isPublic = signal(false);
  readonly isSaving = signal(false);

  readonly areaPoints = this.draftService.points;
  readonly canSave = computed(() => this.name().trim().length > 0);

  private navigatedToArea = false;

  ionViewWillEnter(): void {
    if (!this.navigatedToArea) {
      this.draftService.clear();
    }
    this.navigatedToArea = false;
  }

  goToArea(): void {
    this.navigatedToArea = true;
    this.router.navigate(['/playfields', 'new', 'area']);
  }

  async save(): Promise<void> {
    if (!this.canSave()) return;
    const ownerId = this.userState.profile()?.userId;
    if (!ownerId) return;

    this.isSaving.set(true);
    try {
      const record = await this.playfieldsService.createLocal(
        this.name().trim(),
        this.isPublic(),
        this.draftService.points() as GpsCoordinateDto[],
        ownerId,
      );
      this.draftService.clear();
      await this.router.navigate(['/playfields', record.id]);
    } finally {
      this.isSaving.set(false);
    }
  }

  back(): void {
    this.draftService.clear();
    this.router.navigate(['/playfields']);
  }
}
