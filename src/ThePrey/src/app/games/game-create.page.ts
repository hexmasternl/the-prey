import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AlertController,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonToolbar,
  ModalController,
  ToastController,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { chevronBack } from 'ionicons/icons';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PlayfieldSelectionPage } from '../playfields/playfield-selection/playfield-selection.page';
import { PlayFieldRecord } from '../playfields/playfield.model';
import { GamesService } from './games.service';
import { UserStateService } from '../users/user-state.service';

const GAME_CREATE_INTRO_SEEN_KEY = 'game-create-intro-seen';

@Component({
  selector: 'app-game-create',
  templateUrl: 'game-create.page.html',
  styleUrls: ['game-create.page.scss'],
  imports: [
    TranslatePipe,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonContent,
    IonIcon,
    IonSelect,
    IonSelectOption,
    IonSpinner,
  ],
})
export class GameCreatePage implements ViewWillEnter {
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly userState = inject(UserStateService);
  private readonly modalCtrl = inject(ModalController);
  private readonly toastCtrl = inject(ToastController);
  private readonly alertCtrl = inject(AlertController);
  private readonly translate = inject(TranslateService);

  constructor() {
    addIcons({ chevronBack });
  }

  ionViewWillEnter(): void {
    this.maybeShowIntro();
  }

  private async maybeShowIntro(): Promise<void> {
    if (localStorage.getItem(GAME_CREATE_INTRO_SEEN_KEY)) {
      return;
    }

    const [header, message, ok] = await Promise.all([
      this.translate.get('GAME_CREATE.INTRO_HEADER').toPromise(),
      this.translate.get('GAME_CREATE.INTRO_MESSAGE').toPromise(),
      this.translate.get('GAME_CREATE.INTRO_OK').toPromise(),
    ]);

    const alert = await this.alertCtrl.create({
      header,
      message,
      buttons: [{ text: ok, role: 'cancel' }],
    });
    await alert.present();

    localStorage.setItem(GAME_CREATE_INTRO_SEEN_KEY, 'true');
  }

  readonly gameDuration = signal(60);
  readonly hunterDelay = signal(10);
  readonly endgameDuration = signal(10);
  readonly locationInterval = signal(5);
  readonly endgameInterval = signal(3);
  readonly selectedPlayfield = signal<PlayFieldRecord | null>(null);
  readonly isSubmitting = signal(false);

  readonly canCreate = computed(() => this.selectedPlayfield() !== null && !this.isSubmitting());

  async selectPlayfield(): Promise<void> {
    const modal = await this.modalCtrl.create({ component: PlayfieldSelectionPage });
    await modal.present();
    const { data } = await modal.onDidDismiss<{ playfield: PlayFieldRecord } | null>();
    if (data?.playfield) {
      this.selectedPlayfield.set(data.playfield);
    }
  }

  async create(): Promise<void> {
    const playfield = this.selectedPlayfield();
    const profile = this.userState.profile();
    if (!playfield || !profile || this.isSubmitting()) return;

    this.isSubmitting.set(true);
    try {
      const game = await this.gamesService.createGame({
        playfieldId: playfield.id,
        displayName: profile.callsign,
        gameDuration: this.gameDuration(),
        hunterDelayTime: this.hunterDelay(),
        finalStageDuration: this.endgameDuration(),
        defaultLocationInterval: this.locationInterval() * 60,
        finalLocationInterval: this.endgameInterval() * 60,
      });
      await this.router.navigate(['/games', game.id, 'lobby']);
    } catch {
      const toast = await this.toastCtrl.create({
        message: this.translate.instant('GAME_CREATE.ERROR_CREATE'),
        duration: 4000,
        color: 'danger',
        position: 'bottom',
      });
      await toast.present();
    } finally {
      this.isSubmitting.set(false);
    }
  }

  back(): void {
    this.router.navigate(['/home']);
  }
}
