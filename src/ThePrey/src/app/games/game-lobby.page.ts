import { Component, computed, effect, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonRefresher,
  IonRefresherContent,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonToolbar,
  RefresherCustomEvent,
  ToastController,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { chevronBack, personRemove, shareSocial } from 'ionicons/icons';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Capacitor } from '@capacitor/core';
import { Share } from '@capacitor/share';
import { GameConfigurationDto, GameDto, GamesService } from './games.service';
import { GameLocationService } from './game-location.service';
import { GameStateService } from './game-state.service';
import { UserStateService } from '../users/user-state.service';

/**
 * Base of the Android App Link for joining a game. Tapping
 * https://theprey.nl/games/join/<gameId> opens the app on the join screen when
 * installed (verified via /.well-known/assetlinks.json) and falls back to the
 * website otherwise. Unlike a custom scheme, this https link is tappable in
 * WhatsApp and other chat apps.
 */
const GAME_JOIN_LINK_BASE = 'https://theprey.nl/games/join';

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
    IonRefresher,
    IonRefresherContent,
    IonSelect,
    IonSelectOption,
    IonSpinner,
  ],
})
export class GameLobbyPage implements ViewWillEnter {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly locationService = inject(GameLocationService);
  private readonly userState = inject(UserStateService);
  private readonly gameState = inject(GameStateService);
  private readonly toastCtrl = inject(ToastController);
  private readonly translate = inject(TranslateService);

  /** The full game record, read solely from the single source-of-truth GameStateService. */
  readonly game = computed<GameDto | null>(() => this.gameState.state()?.game ?? null);
  readonly isLoading = computed(() => this.game() === null && !this.gameState.unavailable());

  // Config signals — mirror the game's configuration so ion-select bindings stay simple;
  // kept in sync from `game()` via the effect in the constructor.
  readonly gameDuration = computed(() => this.withFallback(this.game()?.configuration.gameDuration, 60));
  readonly hunterDelay = computed(() => this.withFallback(this.game()?.configuration.hunterDelayTime, 10));
  readonly endgameDuration = computed(() => this.withFallback(this.game()?.configuration.finalStageDuration, 10));
  readonly locationInterval = computed(() =>
    Math.round((this.game()?.configuration.defaultLocationInterval ?? 180) / 60),
  );
  readonly endgameInterval = computed(() =>
    Math.round((this.game()?.configuration.finalLocationInterval ?? 60) / 60),
  );

  readonly gameId = computed(() => this.route.snapshot.paramMap.get('id') ?? '');

  /** Ownership is derived locally (sticky across snapshots) — see GameStateService. */
  readonly isOwner = computed(() => this.gameState.state()?.isOwner ?? false);

  readonly currentUserId = computed(() => this.userState.profile()?.userId ?? '');

  /**
   * Whether the game may be started. The server computes this (enough players, a designated
   * hunter, and every non-host operative readied up) and exposes it on the DTO, so the client
   * just reflects it.
   */
  readonly canStart = computed(() => this.game()?.isReadyToStart ?? false);

  readonly canShare = computed(
    () =>
      Capacitor.isNativePlatform() ||
      (typeof navigator !== 'undefined' && !!navigator.share),
  );

  /** Guards against re-navigating/re-toasting on every subsequent state change once we've left. */
  private navigatingAway = false;

  constructor() {
    addIcons({ chevronBack, personRemove, shareSocial });

    // React to every applied change (snapshot or delta) from the single source of truth:
    // detect a status transition that means "the lobby is over" for this page.
    effect(() => {
      const game = this.game();
      if (!game || this.navigatingAway) return;
      if (game.status === 'Started' || game.status === 'InProgress') {
        this.navigatingAway = true;
        this.enterStartedGame(game);
      } else if (game.status === 'Completed') {
        this.navigatingAway = true;
        void this.leaveDeadLobby();
      }
    });

    // A terminal authorization failure (403/404/410) — the game is gone or we're no longer
    // a member. Leave rather than sitting on stale state.
    effect(() => {
      if (this.gameState.unavailable() && !this.navigatingAway) {
        this.navigatingAway = true;
        void this.leaveDeadLobby();
      }
    });
  }

  async ionViewWillEnter(): Promise<void> {
    await this.gameState.start(this.gameId());
  }

  /**
   * The owner has started the game (Started or InProgress) — route the player to their
   * role's view. The GameStateService keeps running (single connection persists across the
   * lobby → hunt/play navigation); location tracking starts only when the game is
   * InProgress (has a real startedAt) — during Started there is no clock yet.
   */
  private enterStartedGame(game: GameDto): void {
    const uid = this.currentUserId();
    const isHunter = game.hunterUserId === uid;
    const isPrey = game.preys.includes(uid);

    if ((isHunter || isPrey) && game.status === 'InProgress' && game.startedAt) {
      const startedAt = new Date(game.startedAt);
      const endTime = new Date(startedAt.getTime() + game.configuration.gameDuration * 60_000);
      void this.locationService.start(game.id, endTime);
    }

    if (isHunter) {
      this.router.navigate(['/games', game.id, 'hunt'], { replaceUrl: true });
    } else if (isPrey) {
      this.router.navigate(['/games', game.id, 'play'], { replaceUrl: true });
    } else {
      console.error(`[GameLobby] game left lobby but current user ${uid} is neither hunter nor prey — staying put`);
      this.navigatingAway = false;
    }
  }

  /** The game can never resume from here (completed, deleted, or we're no longer a member). */
  private async leaveDeadLobby(): Promise<void> {
    this.gameState.stop();
    await this.showError('GAME_LOBBY.GAME_GONE');
    this.router.navigate(['/home'], { replaceUrl: true });
  }

  async handleRefresh(event: RefresherCustomEvent): Promise<void> {
    await this.gameState.refreshNow();
    await event.target.complete();
  }

  private withFallback(value: number | undefined, fallback: number): number {
    return value !== undefined ? value : fallback;
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

  private async saveConfig(overrides: Partial<GameConfigurationDto>): Promise<void> {
    const g = this.game();
    if (!g || !this.isOwner()) return;
    try {
      const updated = await this.gamesService.updateConfig(this.gameId(), {
        ...this.buildConfig(g),
        ...overrides,
      });
      this.gameState.applyOwnMutation(updated);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
    }
  }

  async onDurationChange(e: Event): Promise<void> {
    await this.saveConfig({ gameDuration: +(e as CustomEvent).detail.value });
  }

  async onHunterDelayChange(e: Event): Promise<void> {
    await this.saveConfig({ hunterDelayTime: +(e as CustomEvent).detail.value });
  }

  async onEndgameDurationChange(e: Event): Promise<void> {
    await this.saveConfig({ finalStageDuration: +(e as CustomEvent).detail.value });
  }

  async onLocationIntervalChange(e: Event): Promise<void> {
    await this.saveConfig({ defaultLocationInterval: +(e as CustomEvent).detail.value * 60 });
  }

  async onEndgameIntervalChange(e: Event): Promise<void> {
    await this.saveConfig({ finalLocationInterval: +(e as CustomEvent).detail.value * 60 });
  }

  async designateHunter(userId: string): Promise<void> {
    if (!this.isOwner()) return;
    try {
      const game = await this.gamesService.setHunter(this.gameId(), userId);
      this.gameState.applyOwnMutation(game);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
    }
  }

  async removePlayer(userId: string): Promise<void> {
    if (!this.isOwner()) return;
    try {
      const game = await this.gamesService.removePlayer(this.gameId(), userId);
      this.gameState.applyOwnMutation(game);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
    }
  }

  async startGame(): Promise<void> {
    const g = this.game();
    if (!g || !this.isOwner() || !g.hunterUserId) return;
    try {
      // The owner is a participant too, so the resulting broadcast drives navigation for
      // everyone (owner included) via the state effect above — no manual nav here.
      const game = await this.gamesService.startGame(this.gameId(), g.hunterUserId);
      this.gameState.applyOwnMutation(game);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
    }
  }

  async markReady(): Promise<void> {
    try {
      const game = await this.gamesService.setReady(this.gameId());
      this.gameState.applyOwnMutation(game);
    } catch {
      await this.showError('GAME_LOBBY.ACTION_ERROR');
    }
  }

  isCurrentUser(userId: string): boolean {
    return userId === this.currentUserId();
  }

  isReady(userId: string): boolean {
    return this.game()?.participants.find(p => p.userId === userId)?.isReady ?? false;
  }

  async shareGame(): Promise<void> {
    const g = this.game();
    if (!g) return;
    const message = `${this.translate.instant('GAME_SHARE.MESSAGE')} ${g.gameCode}`;
    const title = this.translate.instant('GAME_SHARE.TITLE');
    // The recipient still needs the 4-digit code to join, so the message carries
    // both the code (in `message`) and the tappable link on its own line.
    const link = `${GAME_JOIN_LINK_BASE}/${g.id}`;
    try {
      if (Capacitor.isNativePlatform()) {
        // The Capacitor Share plugin only accepts http(s)/file URLs in `url`, so the
        // https App Link goes inside the text where chat apps will still linkify it.
        await Share.share({ title, text: `${message}\n${link}` });
      } else if (navigator.share) {
        await navigator.share({ title, text: message, url: link });
      }
    } catch {
      // user cancelled or share failed — no action needed
    }
  }

  back(): void {
    this.navigatingAway = true;
    this.gameState.stop();
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
