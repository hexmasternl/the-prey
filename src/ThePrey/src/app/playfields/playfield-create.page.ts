import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Browser } from '@capacitor/browser';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
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
import { addIcons } from 'ionicons';
import { chevronBack } from 'ionicons/icons';
import { TranslatePipe } from '@ngx-translate/core';
import { PlayfieldsService } from './playfields.service';
import { PlayfieldDraftService } from './playfield-draft.service';
import { isPublicEligibleName } from './playfield.model';
import { UserStateService } from '../users/user-state.service';
import { LanguageService } from '../i18n/language.service';

@Component({
  selector: 'app-playfield-create',
  templateUrl: 'playfield-create.page.html',
  styleUrls: ['playfield-create.page.scss'],
  imports: [
    FormsModule,
    TranslatePipe,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonIcon,
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
  private readonly toastController = inject(ToastController);
  private readonly languageService = inject(LanguageService);

  constructor() {
    addIcons({ chevronBack });

    // The public toggle is only enabled while the name follows the public-listing
    // convention. If the name stops matching after the toggle was switched on,
    // reset it to private before the control is disabled.
    effect(() => {
      if (!this.canBePublic() && this.isPublic()) {
        this.isPublic.set(false);
      }
    });
  }

  readonly name = signal('');
  readonly isPublic = signal(false);
  readonly isSaving = signal(false);

  readonly areaPoints = this.draftService.points;
  readonly canBePublic = computed(() => isPublicEligibleName(this.name()));
  readonly canSave = computed(
    () => this.name().trim().length > 3 && this.draftService.points().length >= 3,
  );

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

    this.isSaving.set(true);
    try {
      await this.playfieldsService.create(
        this.name().trim(),
        this.isPublic(),
        this.draftService.points(),
      );
      this.draftService.clear();
      await this.router.navigate(['/playfields']);
    } catch (err: any) {
      const message = err?.error?.message ?? err?.message ?? 'PLAYFIELD_CREATE.ERROR_CREATE';
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

  async openHelp(): Promise<void> {
    await Browser.open({ url: `https://theprey.nl/${this.languageService.current}/playfield/` });
  }

  back(): void {
    this.draftService.clear();
    this.router.navigate(['/playfields']);
  }
}
