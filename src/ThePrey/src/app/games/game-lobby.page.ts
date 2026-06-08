import { Component, computed, inject, OnDestroy, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonToolbar,
  ToastController,
  ViewWillEnter,
  ViewWillLeave,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { checkmarkCircle, personRemove, shareSocial } from 'ionicons/icons';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '@auth0/auth0-angular';
import { Capacitor } from '@capacitor/core';
import { Share } from '@capacitor/share';
import { firstValueFrom } from 'rxjs';
import { nativeGameJoinUri } from '../auth.utils';
import { GameConfigurationDto, GameDto, GamesService } from './games.service';
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
    IonContent,
    IonIcon,
    IonSelect,
    IonSelectOption,
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

  // Config signals — initialised from game on load, kept in sync via SSE
  readonly gameDuration = signal(60);
  readonly hunterDelay = signal(10);
  readonly endgameDuration = signal(10);
  readonly locationInterval = signal(5);
  readonly endgameInterval = signal(3);

  readonly gameId = computed(() => this.route.snapshot.paramMap.get('id') ?? '');

  readonly isOwner = computed(() => {
    const g = this.game();
    const profile = this.userState.profile();
    return g != null && profile != null ? g.ownerUserId === profile.userId : false;
  });

  readonly currentUserId = computed(() => this.userState.profile()?.userId ?? '');

  readonly canShare = computed(
    () =>
      Capacitor.isNativePlatform() ||
      (typeof navigator !== 'undefined' && !!navigator.share),
  );

  constructor() {
    addIcons({ checkmarkCircle, personRemove, shareSocial });
  }

  async ionViewWillEnter(): Promise<void> {
    const id = this.gameId();
    this.isLoading.set(true);
    try {
      const game = await this.gamesService.getGame(id);
      this.game.set(game);
      this.syncConfigFromGame(game);
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
      es.addEventListener('game-started', (e: MessageEvent) => this.onGameStarted(e));
      es.onerror = () => this.refreshGame();
    } catch {
      // SSE is best-effort; polling fallback via onerror
    }
  }

  private onLobbyEvent(event: MessageEvent): void {
    try {
      const data = JSON.parse(event.data);
      if (data?.payload) {
        this.game.set(data.payload);
        this.syncConfigFromGame(data.payload);
      }
    } catch {
      // ignore parse errors
    }
  }

  private onGameStarted(event: MessageEvent): void {
    try {
      const data = JSON.parse(event.data);
      const game: GameDto | null = data?.payload ?? null;
      if (!game) return;
      const uid = this.currentUserId();
      this.closeStream();
      if (game.hunter?.userId === uid) {
        this.router.navigate(['/games', game.id, 'hunt'], { replaceUrl: true });
      } else if (game.preys.some(p => p.userId === uid)) {
        this.router.navigate(['/games', game.id, 'play'], { replaceUrl: true });
      }
    } catch {
      // ignore parse errors
    }
  }

  private async refreshGame(): Promise<void> {
    try {
      const game = await this.gamesService.getGame(this.gameId());
      this.game.set(game);
      this.syncConfigFromGame(game);
    } catch {
      // best effort
    }
  }

  private closeStream(): void {
    this.eventSource?.close();
    this.eventSource = null;
  }

  private syncConfigFromGame(g: GameDto): void {
    this.gameDuration.set(g.configuration.gameDuration);
    this.hunterDelay.set(g.configuration.hunterDelayTime);
    this.endgameDuration.set(g.configuration.finalStageDuration);
    this.locationInterval.set(Math.round(g.configuration.defaultLocationInterval / 60));
    this.endgameInterval.set(Math.round(g.configuration.finalLocationInterval / 60));
  }

  private buildConfig(g: GameDto): GameConfigurationDto {
    return {
      gameDuration: this.gameDuration(),
      hunterDelayTime: this.hunterDelay(),
      finalStageDuration: this.endgameDuration(),
      defaultLocationInterval: this.locationInterval() * 60,
      finalLocationInterval: this.endgameInterval() * 60,
      enablePreyBoundaryPenalties: g.configuration.enablePreyBoundaryPenalties,
      enableHunterBoundaryPenalty: g.configuration.enableHunterBoundaryPenalty,
    };
  }

  private async saveConfig(): Promise<void> {
    const g = this.game();
    if (!g || !this.isOwner()) return;
    try {
      const updated = await this.gamesService.updateConfig(this.gameId(), this.buildConfig(g));
      this.game.set(updated);
      this.syncConfigFromGame(updated);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
      // Revert display to last confirmed server state
      const current = this.game();
      if (current) this.syncConfigFromGame(current);
    }
  }

  async onDurationChange(e: Event): Promise<void> {
    this.gameDuration.set(+(e as CustomEvent).detail.value);
    await this.saveConfig();
  }

  async onHunterDelayChange(e: Event): Promise<void> {
    this.hunterDelay.set(+(e as CustomEvent).detail.value);
    await this.saveConfig();
  }

  async onEndgameDurationChange(e: Event): Promise<void> {
    this.endgameDuration.set(+(e as CustomEvent).detail.value);
    await this.saveConfig();
  }

  async onLocationIntervalChange(e: Event): Promise<void> {
    this.locationInterval.set(+(e as CustomEvent).detail.value);
    await this.saveConfig();
  }

  async onEndgameIntervalChange(e: Event): Promise<void> {
    this.endgameInterval.set(+(e as CustomEvent).detail.value);
    await this.saveConfig();
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

  isCurrentUser(userId: string): boolean {
    return userId === this.currentUserId();
  }

  isReady(userId: string): boolean {
    return this.game()?.lobby.find(p => p.userId === userId)?.isReady ?? false;
  }

  async shareGame(): Promise<void> {
    const g = this.game();
    if (!g) return;
    // On device, share the custom-scheme deep link so tapping it reopens the app
    // on the join screen. In the browser there is no app to open, so link to the web join page.
    const url = Capacitor.isNativePlatform()
      ? nativeGameJoinUri(g.id)
      : `${window.location.origin}/games/join?gameId=${g.id}`;
    const message = `${this.translate.instant('GAME_SHARE.MESSAGE')} ${g.gameCode}`;
    const title = this.translate.instant('GAME_SHARE.TITLE');
    try {
      if (Capacitor.isNativePlatform()) {
        await Share.share({ title, text: message, url });
      } else if (navigator.share) {
        await navigator.share({ title, text: message, url });
      }
    } catch {
      // user cancelled or share failed — no action needed
    }
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
