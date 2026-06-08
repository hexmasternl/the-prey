import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonSpinner,
  IonToolbar,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { GameDto, GamesService } from './games.service';
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
export class GameJoinPage implements ViewWillEnter {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly userState = inject(UserStateService);

  readonly gameId = signal<string | null>(this.route.snapshot.queryParamMap.get('gameId'));
  readonly joinCode = signal('');
  readonly isLoading = signal(false);
  readonly isSubmitting = signal(false);
  readonly game = signal<GameDto | null>(null);
  readonly errorWrongCode = signal(false);
  readonly gameNotFound = signal(false);
  readonly gameStarted = signal(false);

  readonly callsign = computed(() => this.userState.profile()?.callsign ?? null);

  readonly canJoin = computed(
    () =>
      this.joinCode().length === 4 &&
      !this.isSubmitting() &&
      this.gameId() != null &&
      this.callsign() != null &&
      this.game() != null &&
      !this.gameNotFound() &&
      !this.gameStarted(),
  );

  async ionViewWillEnter(): Promise<void> {
    const id = this.gameId();
    if (!id) {
      this.gameNotFound.set(true);
      return;
    }
    this.isLoading.set(true);
    this.game.set(null);
    this.gameNotFound.set(false);
    this.gameStarted.set(false);
    this.errorWrongCode.set(false);
    try {
      const g = await this.gamesService.getGame(id);
      this.game.set(g);
      if (g.status !== 'Lobby') {
        this.gameStarted.set(true);
      }
    } catch {
      this.gameNotFound.set(true);
    } finally {
      this.isLoading.set(false);
    }
  }

  onCodeInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const digits = raw.replace(/\D/g, '').slice(0, 4);
    this.joinCode.set(digits);
    (event.target as HTMLInputElement).value = digits;
    this.errorWrongCode.set(false);
  }

  async join(): Promise<void> {
    const gameId = this.gameId();
    const callsign = this.callsign();
    const g = this.game();
    if (!gameId || !callsign || !g || !this.canJoin()) return;

    if (this.joinCode() !== g.gameCode) {
      this.errorWrongCode.set(true);
      return;
    }

    this.isSubmitting.set(true);
    this.errorWrongCode.set(false);
    try {
      const joined = await this.gamesService.joinGame(gameId, callsign);
      await this.router.navigate(['/games', joined.id, 'lobby']);
    } catch (err: unknown) {
      const body = this.errorBody(err);
      if (body?.toLowerCase().includes('already')) {
        await this.router.navigate(['/games', gameId, 'lobby']);
        return;
      }
      if (body?.toLowerCase().includes('progress') || body?.toLowerCase().includes('started')) {
        this.gameStarted.set(true);
        return;
      }
      this.errorWrongCode.set(true);
    } finally {
      this.isSubmitting.set(false);
    }
  }

  back(): void {
    this.router.navigate(['/home']);
  }

  private errorBody(err: unknown): string | null {
    if (err && typeof err === 'object' && 'error' in err) {
      return JSON.stringify((err as { error: unknown }).error);
    }
    return null;
  }
}
