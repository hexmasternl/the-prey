import { Component, computed, inject, OnDestroy, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemOption,
  IonItemOptions,
  IonItemSliding,
  IonLabel,
  IonList,
  IonSpinner,
  IonTitle,
  IonToolbar,
  ToastController,
  ViewWillEnter,
  ViewWillLeave,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { checkmarkCircle, personRemove, settings } from 'ionicons/icons';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '@auth0/auth0-angular';
import { firstValueFrom } from 'rxjs';
import { GameDto, GamesService } from './games.service';
import { UserStateService } from '../users/user-state.service';

@Component({
  selector: 'app-game-lobby',
  templateUrl: 'game-lobby.page.html',
  styleUrls: ['game-lobby.page.scss'],
  imports: [
    TranslatePipe,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonList,
    IonItem,
    IonLabel,
    IonItemSliding,
    IonItemOptions,
    IonItemOption,
    IonIcon,
    IonSpinner,
  ],
})
export class GameLobbyPage implements ViewWillEnter, ViewWillLeave, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly userState = inject(UserStateService);
  private readonly authService = inject(AuthService);
  private readonly toastCtrl = inject(ToastController);
  private readonly translate = inject(TranslateService);

  readonly game = signal<GameDto | null>(null);
  readonly isLoading = signal(true);
  private eventSource: EventSource | null = null;

  readonly gameId = computed(() => this.route.snapshot.paramMap.get('id') ?? '');

  readonly isOwner = computed(() => {
    const g = this.game();
    const profile = this.userState.profile();
    return g != null && profile != null ? g.ownerUserId === profile.userId : false;
  });

  readonly currentUserId = computed(() => this.userState.profile()?.userId ?? '');

  constructor() {
    addIcons({ checkmarkCircle, personRemove, settings });
  }

  async ionViewWillEnter(): Promise<void> {
    const id = this.gameId();
    this.isLoading.set(true);
    try {
      const game = await this.gamesService.getGame(id);
      this.game.set(game);
    } catch {
      await this.showError('GAME_LOBBY.LOAD_ERROR');
    } finally {
      this.isLoading.set(false);
    }
    await this.connectStream();
  }

  ionViewWillLeave(): void {
    this.closeStream();
  }

  ngOnDestroy(): void {
    this.closeStream();
  }

  private async connectStream(): Promise<void> {
    try {
      const token = await firstValueFrom(this.authService.getAccessTokenSilently());
      const es = this.gamesService.connectLobbyStream(this.gameId(), token);
      this.eventSource = es;
      es.addEventListener('lobby-updated', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('settings-updated', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('ready-updated', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('hunter-designated', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('hunter-changed', (e: MessageEvent) => this.onLobbyEvent(e));
      es.onerror = () => this.refreshGame();
    } catch {
      // SSE is best-effort; polling fallback via onerror
    }
  }

  private onLobbyEvent(event: MessageEvent): void {
    try {
      const data = JSON.parse(event.data);
      if (data?.payload) this.game.set(data.payload);
    } catch {
      // ignore parse errors
    }
  }

  private async refreshGame(): Promise<void> {
    try {
      const game = await this.gamesService.getGame(this.gameId());
      this.game.set(game);
    } catch {
      // best effort
    }
  }

  private closeStream(): void {
    this.eventSource?.close();
    this.eventSource = null;
  }

  async designateHunter(userId: string): Promise<void> {
    if (!this.isOwner()) return;
    try {
      const game = await this.gamesService.setHunter(this.gameId(), userId);
      this.game.set(game);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
    }
  }

  async removePlayer(userId: string): Promise<void> {
    if (!this.isOwner()) return;
    try {
      const game = await this.gamesService.removePlayer(this.gameId(), userId);
      this.game.set(game);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
    }
  }

  async markReady(): Promise<void> {
    try {
      const game = await this.gamesService.setReady(this.gameId());
      this.game.set(game);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
    }
  }

  async editSettings(): Promise<void> {
    const toast = await this.toastCtrl.create({
      message: this.translate.instant('GAME_LOBBY.SETTINGS_COMING_SOON'),
      duration: 2000,
      position: 'bottom',
    });
    await toast.present();
  }

  isCurrentUser(userId: string): boolean {
    return userId === this.currentUserId();
  }

  isReady(userId: string): boolean {
    return this.game()?.lobby.find(p => p.userId === userId)?.isReady ?? false;
  }

  back(): void {
    this.router.navigate(['/home']);
  }

  private async showError(key: string): Promise<void> {
    const toast = await this.toastCtrl.create({
      message: this.translate.instant(key),
      duration: 4000,
      color: 'danger',
      position: 'bottom',
    });
    await toast.present();
  }
}
