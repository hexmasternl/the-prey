import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
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
  ToastController,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { PlayFieldDetailDto } from './playfield.model';
import { PlayfieldsService } from './playfields.service';
import { PlayfieldDraftService } from './playfield-draft.service';
import { PlayfieldMapComponent } from './playfield-map/playfield-map.component';

@Component({
  selector: 'app-playfield-detail',
  templateUrl: 'playfield-detail.page.html',
  styleUrls: ['playfield-detail.page.scss'],
  imports: [
    FormsModule,
    TranslatePipe,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonContent,
    IonItem,
    IonInput,
    IonLabel,
    IonNote,
    IonToggle,
    IonSpinner,
    PlayfieldMapComponent,
  ],
})
export class PlayfieldDetailPage implements ViewWillEnter {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly playfieldsService = inject(PlayfieldsService);
  private readonly draftService = inject(PlayfieldDraftService);
  private readonly toastController = inject(ToastController);

  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly notFound = signal(false);
  readonly playfield = signal<PlayFieldDetailDto | null>(null);
  readonly name = signal('');
  readonly isPublic = signal(false);

  readonly areaPoints = this.draftService.points;
  readonly canSave = computed(() => this.name().trim().length > 2 && this.areaPoints().length >= 3);

  private navigatedToArea = false;

  async ionViewWillEnter(): Promise<void> {
    if (this.navigatedToArea) {
      this.navigatedToArea = false;
      return;
    }

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      return;
    }

    this.isLoading.set(true);
    this.notFound.set(false);
    this.draftService.clear();

    try {
      const data = await this.playfieldsService.getById(id);
      this.playfield.set(data);
      this.name.set(data.name);
      this.isPublic.set(data.isPublic);
      this.draftService.set(data.points);
    } catch (err: any) {
      if (err?.status === 404) {
        this.notFound.set(true);
      }
    } finally {
      this.isLoading.set(false);
    }
  }

  goToArea(): void {
    const id = this.playfield()?.id;
    if (id) {
      this.navigatedToArea = true;
      this.router.navigate(['/playfields', id, 'area']);
    }
  }

  async save(): Promise<void> {
    const pf = this.playfield();
    if (!pf || !this.canSave()) return;

    this.isSaving.set(true);
    try {
      await this.playfieldsService.update(
        pf.id,
        this.name().trim(),
        this.isPublic(),
        this.areaPoints(),
        pf.lastUpdatedOn,
      );
      this.draftService.clear();
      await this.router.navigate(['/playfields']);
    } catch (err: any) {
      const message = err?.error?.message ?? err?.message ?? 'PLAYFIELD_DETAIL.ERROR_SAVE';
      const toast = await this.toastController.create({
        message,
        duration: 3000,
        color: 'danger',
        position: 'bottom',
      });
      await toast.present();
    } finally {
      this.isSaving.set(false);
    }
  }

  back(): void {
    this.draftService.clear();
    this.router.navigate(['/playfields']);
  }
}
