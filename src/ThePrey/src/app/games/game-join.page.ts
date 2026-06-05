import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonSpinner,
  IonToolbar,
  ToastController,
} from '@ionic/angular/standalone';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { GamesService } from './games.service';
import { UserStateService } from '../users/user-state.service';

@Component({
  selector: 'app-game-join',
  templateUrl: 'game-join.page.html',
  styleUrls: ['game-join.page.scss'],
  imports: [
    TranslatePipe,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonContent,
    IonSpinner,
  ],
})
export class GameJoinPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly userState = inject(UserStateService);
  private readonly toastCtrl = inject(ToastController);
  private readonly translate = inject(TranslateService);

  readonly gameId = signal<string | null>(this.route.snapshot.queryParamMap.get('gameId'));
  readonly joinCode = signal('');
  readonly isSubmitting = signal(false);

  readonly callsign = computed(() => this.userState.profile()?.callsign ?? null);

  readonly canJoin = computed(
    () =>
      this.joinCode().length === 8 &&
      !this.isSubmitting() &&
      this.gameId() != null &&
      this.callsign() != null,
  );

  onCodeInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const digits = raw.replace(/\D/g, '').slice(0, 8);
    this.joinCode.set(digits);
    (event.target as HTMLInputElement).value = digits;
  }

  async join(): Promise<void> {
    const gameId = this.gameId();
    const callsign = this.callsign();
    if (!gameId || !callsign || !this.canJoin()) return;

    this.isSubmitting.set(true);
    try {
      const game = await this.gamesService.joinGame(gameId, this.joinCode(), callsign);
      await this.router.navigate(['/games', game.id, 'lobby']);
    } catch {
      const toast = await this.toastCtrl.create({
        message: this.translate.instant('GAME_JOIN.ERROR_WRONG_CODE'),
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
