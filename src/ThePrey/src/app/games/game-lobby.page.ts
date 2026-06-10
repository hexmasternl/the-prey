import { Component, computed, inject, OnDestroy, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
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
  ViewWillLeave,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { checkmarkCircle, chevronBack, personRemove, shareSocial } from 'ionicons/icons';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Capacitor } from '@capacitor/core';
import { Share } from '@capacitor/share';
import { GameConfigurationDto, GameDto, GamesService } from './games.service';
import { GameLocationService } from './game-location.service';
import { UserStateService } from '../users/user-state.service';
import { WebPubSubStream } from '../core/web-pubsub-stream';

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
export class GameLobbyPage implements ViewWillEnter, ViewWillLeave, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly locationService = inject(GameLocationService);
  private readonly userState = inject(UserStateService);
  private readonly http = inject(HttpClient);
  private readonly toastCtrl = inject(ToastController);
  private readonly translate = inject(TranslateService);

  readonly game = signal<GameDto | null>(null);
  readonly isLoading = signal(true);
  private stream: WebPubSubStream | null = null;

  // Config signals — initialised from game on load, kept in sync via Web PubSub
  readonly gameDuration = signal(60);
  readonly hunterDelay = signal(10);
  readonly endgameDuration = signal(10);
  readonly locationInterval = signal(5);
  readonly endgameInterval = signal(3);

  readonly gameId = computed(() => this.route.snapshot.paramMap.get('id') ?? '');

  /** Ownership is decided server-side (per caller) and surfaced on the DTO — no local derivation. */
  readonly isOwner = computed(() => this.game()?.isOwnerPlayer ?? false);

  readonly currentUserId = computed(() => this.userState.profile()?.userId ?? '');

  /** The host sees a Start button once at least one other operative has joined (2+ total). */
  readonly canShowStart = computed(() => this.isOwner() && (this.game()?.participants.length ?? 0) >= 2);

  /**
   * Whether the game may be started. The server computes this (enough players, a designated hunter,
   * and every non-host operative readied up) and exposes it on the DTO, so the client just reflects it.
   */
  readonly canStart = computed(() => this.game()?.isReadyToStart ?? false);

  readonly canShare = computed(
    () =>
      Capacitor.isNativePlatform() ||
      (typeof navigator !== 'undefined' && !!navigator.share),
  );

  constructor() {
    addIcons({ checkmarkCircle, chevronBack, personRemove, shareSocial });
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
    this.connectStream();
  }

  ionViewWillLeave(): void {
    this.closeStream();
  }

  ngOnDestroy(): void {
    this.closeStream();
  }

  private connectStream(): void {
    const gameId = this.gameId();
    this.streamLog(`connecting — gameId=${gameId}`);
    this.stream = new WebPubSubStream({
      gameId,
      http: this.http,
      onMessage: (envelope) => {
        const { type, data } = envelope;
        if (type === 'game-started') {
          this.onGameStarted(data);
        } else {
          this.onLobbyEvent(type, data);
        }
      },
      // The WebSocket was down — refetch the game so missed events can't strand us.
      // refreshGame also detects a game that started while we were disconnected
      // and runs the same navigation as onGameStarted.
      onReconnected: () => void this.refreshGame(),
      log: (msg) => this.streamLog(msg),
      logError: (msg, ...args) => this.streamError(msg, ...args),
    });
    void this.stream.start();
  }

  private onLobbyEvent(type: string, data: unknown): void {
    this.streamLog(`event '${type}' received`);
    // The Web PubSub envelope carries a full GameDto directly in `data`
    // (not wrapped in a `.payload` property like the old SSE events were).
    const game = data as GameDto;
    if (game && typeof game === 'object' && 'id' in game) {
      this.game.set(game);
      this.syncConfigFromGame(game);
      this.streamLog(`event '${type}' applied to game state`);
    } else {
      this.streamError(`event '${type}' did not carry a recognisable GameDto`, data);
    }
  }

  private onGameStarted(data: unknown): void {
    this.streamLog(`event 'game-started' received`);
    const game = data as GameDto;
    if (!game || typeof game !== 'object' || !('id' in game)) {
      this.streamError(`event 'game-started' did not carry a recognisable GameDto`, data);
      return;
    }

    // Reflect the new state locally and confirm the game actually left the lobby
    // before navigating. Keep the socket open if the status hasn't changed yet.
    this.game.set(game);
    this.syncConfigFromGame(game);
    if (game.status !== 'InProgress') {
      this.streamError(`event 'game-started' carried unexpected status '${game.status}' (expected 'InProgress') — not navigating`);
      return;
    }

    this.enterStartedGame(game);
  }

  /**
   * The game is verified InProgress — stop the lobby stream, start location tracking and
   * route the player to their role's view. Reached via the `game-started` event or via a
   * post-reconnect refresh that discovered the game started while we were disconnected.
   */
  private enterStartedGame(game: GameDto): void {
    const uid = this.currentUserId();
    this.closeStream();

    const isHunter = game.hunterUserId === uid;
    const isPrey = game.preys.includes(uid);
    this.streamLog(`game in progress — uid=${uid} isHunter=${isHunter} isPrey=${isPrey}`);

    // Start background location tracking before navigating so reporting begins the
    // moment the game starts. The in-game page's ionViewWillEnter is a no-op when a
    // session for this game is already active (idempotent start).
    if (isHunter || isPrey) {
      const startedAt = game.startedAt ? new Date(game.startedAt) : new Date();
      const endTime = new Date(startedAt.getTime() + game.configuration.gameDuration * 60_000);
      void this.locationService.start(game.id, endTime);
    }

    if (isHunter) {
      this.streamLog(`navigating to hunt view for game ${game.id}`);
      this.router.navigate(['/games', game.id, 'hunt'], { replaceUrl: true });
    } else if (isPrey) {
      this.streamLog(`navigating to play view for game ${game.id}`);
      this.router.navigate(['/games', game.id, 'play'], { replaceUrl: true });
    } else {
      this.streamError(`game in progress but current user ${uid} is neither hunter nor prey — staying put`);
    }
  }

  private async refreshGame(): Promise<void> {
    try {
      const game = await this.gamesService.getGame(this.gameId());
      this.game.set(game);
      this.syncConfigFromGame(game);
      if (game.status === 'InProgress') {
        this.streamLog('refresh found the game already in progress (missed game-started) — entering game');
        this.enterStartedGame(game);
      } else if (game.status === 'Completed') {
        this.streamLog('refresh found the game completed — leaving lobby');
        await this.leaveDeadLobby();
      }
    } catch (err) {
      // A vanished game (cleaned up server-side) leaves nothing to wait for. Anything
      // else is transient: stay and retry on the next reconnect.
      if (err instanceof HttpErrorResponse && (err.status === 404 || err.status === 410)) {
        this.streamLog(`refresh got ${err.status} — game no longer exists, leaving lobby`);
        await this.leaveDeadLobby();
      }
    }
  }

  /** The game can never resume from here (completed or deleted) — stop streaming and go home. */
  private async leaveDeadLobby(): Promise<void> {
    this.closeStream();
    await this.showError('GAME_LOBBY.GAME_GONE');
    this.router.navigate(['/home'], { replaceUrl: true });
  }

  async handleRefresh(event: RefresherCustomEvent): Promise<void> {
    await this.refreshGame();
    await event.target.complete();
  }

  private closeStream(): void {
    this.stream?.stop();
    this.stream = null;
  }

  // ── Stream diagnostics ───────────────────────────────────────────────────
  // Tagged so logs are easy to filter on-device (logcat / Chrome remote devtools).
  private readonly streamTag = '[LobbyStream]';

  private streamLog(message: string, ...args: unknown[]): void {
    console.info(`${this.streamTag} ${message}`, ...args);
  }

  private streamError(message: string, ...args: unknown[]): void {
    console.error(`${this.streamTag} ${message}`, ...args);
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

  async startGame(): Promise<void> {
    const g = this.game();
    if (!g || !this.isOwner() || !g.hunterUserId) return;
    try {
      // The owner is a participant too, so the resulting `game-started` Web PubSub event
      // drives navigation for everyone (owner included) via onGameStarted — no manual nav here.
      const game = await this.gamesService.startGame(this.gameId(), g.hunterUserId);
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
