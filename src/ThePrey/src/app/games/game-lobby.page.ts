import { Component, computed, inject, OnDestroy, signal } from '@angular/core';
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
import { AuthService } from '@auth0/auth0-angular';
import { Capacitor } from '@capacitor/core';
import { Share } from '@capacitor/share';
import { firstValueFrom } from 'rxjs';
import { GameConfigurationDto, GameDto, GamesService } from './games.service';
import { GameLocationService } from './game-location.service';
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
export class GameLobbyPage implements ViewWillEnter, ViewWillLeave, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly locationService = inject(GameLocationService);
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

  /** Ownership is decided server-side (per caller) and surfaced on the DTO — no local derivation. */
  readonly isOwner = computed(() => this.game()?.isOwnerPlayer ?? false);

  readonly currentUserId = computed(() => this.userState.profile()?.userId ?? '');

  /** The host sees a Start button once at least one other operative has joined (2+ total). */
  readonly canShowStart = computed(() => this.isOwner() && (this.game()?.lobby.length ?? 0) >= 2);

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
    await this.connectStream();
  }

  ionViewWillLeave(): void {
    this.closeStream();
  }

  ngOnDestroy(): void {
    this.closeStream();
  }

  private async connectStream(): Promise<void> {
    const gameId = this.gameId();
    this.streamLog(`connecting — gameId=${gameId}`);
    try {
      const token = await firstValueFrom(this.authService.getAccessTokenSilently());
      this.streamLog(`access token acquired — present=${!!token} length=${token?.length ?? 0}`);

      const es = this.gamesService.connectLobbyStream(gameId, token);
      this.eventSource = es;
      this.streamLog(`EventSource created — url=${this.redactToken(es.url)} readyState=${es.readyState}`);

      es.onopen = () => this.streamLog(`onopen — connection established, readyState=${es.readyState}`);

      es.addEventListener('lobby-updated', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('settings-updated', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('ready-updated', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('hunter-designated', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('hunter-changed', (e: MessageEvent) => this.onLobbyEvent(e));
      es.addEventListener('game-started', (e: MessageEvent) => this.onGameStarted(e));

      // Default (unnamed) SSE messages won't trigger the named listeners above. Logging them
      // surfaces events arriving under an unexpected/missing event name — a common reason
      // "events don't arrive" on the client even though the server is sending them.
      es.onmessage = (e: MessageEvent) =>
        this.streamLog(`onmessage (unnamed event) — data=${this.previewData(e.data)}`);

      es.onerror = () => {
        // readyState: 0=CONNECTING (retrying), 1=OPEN, 2=CLOSED (gave up). This tells us whether
        // the connection never opened, dropped and is retrying, or was closed for good.
        this.streamError(`onerror — readyState=${es.readyState} (0=connecting,1=open,2=closed)`);
        this.refreshGame();
      };
    } catch (err) {
      // SSE is best-effort; polling fallback via onerror
      this.streamError('connect failed before EventSource was established', err);
    }
  }

  private onLobbyEvent(event: MessageEvent): void {
    this.streamLog(`event '${event.type}' received — data=${this.previewData(event.data)}`);
    try {
      const data = JSON.parse(event.data);
      if (data?.payload) {
        this.game.set(data.payload);
        this.syncConfigFromGame(data.payload);
        this.streamLog(`event '${event.type}' applied to game state`);
      } else {
        this.streamError(`event '${event.type}' had no 'payload' field — keys=[${Object.keys(data ?? {}).join(', ')}]`);
      }
    } catch (err) {
      this.streamError(`event '${event.type}' failed to parse`, err);
    }
  }

  private onGameStarted(event: MessageEvent): void {
    this.streamLog(`event 'game-started' received — data=${this.previewData(event.data)}`);
    try {
      const data = JSON.parse(event.data);
      const game: GameDto | null = data?.payload ?? null;
      if (!game) {
        this.streamError(`event 'game-started' had no 'payload' field — keys=[${Object.keys(data ?? {}).join(', ')}]`);
        return;
      }

      // Reflect the new state locally and confirm the game actually left the lobby before
      // navigating away. The event name says "started", but we verify the payload agrees
      // (status === 'InProgress') so a stale or mis-routed event can't pull players into an
      // in-game view prematurely. Keep the stream open if the state hasn't changed yet.
      this.game.set(game);
      if (game.status !== 'InProgress') {
        this.streamError(`event 'game-started' carried unexpected status '${game.status}' (expected 'InProgress') — not navigating`);
        return;
      }

      const uid = this.currentUserId();
      this.closeStream();

      const isHunter = game.hunter?.userId === uid;
      const isPrey = game.preys.some(p => p.userId === uid);
      this.streamLog(`game-started — uid=${uid} isHunter=${isHunter} isPrey=${isPrey}`);

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
        this.streamError(`game-started but current user ${uid} is neither hunter nor prey — staying put`);
      }
    } catch (err) {
      this.streamError("event 'game-started' failed to parse", err);
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

  async handleRefresh(event: RefresherCustomEvent): Promise<void> {
    await this.refreshGame();
    await event.target.complete();
  }

  private closeStream(): void {
    if (this.eventSource) {
      this.streamLog(`closing stream — readyState=${this.eventSource.readyState}`);
    }
    this.eventSource?.close();
    this.eventSource = null;
  }

  // ── SSE diagnostics ──────────────────────────────────────────
  // Tagged so logs are easy to filter on-device, e.g. `adb logcat | grep LobbyStream`
  // (Capacitor forwards the WebView console to logcat) or in Chrome remote devtools.
  private readonly streamTag = '[LobbyStream]';

  private streamLog(message: string, ...args: unknown[]): void {
    console.info(`${this.streamTag} ${message}`, ...args);
  }

  private streamError(message: string, ...args: unknown[]): void {
    console.error(`${this.streamTag} ${message}`, ...args);
  }

  /** Redacts the JWT in the SSE URL so it never lands in logs. */
  private redactToken(url: string): string {
    return url.replace(/token=[^&]+/i, 'token=<redacted>');
  }

  /** A bounded preview of raw event data so logcat lines stay readable. */
  private previewData(data: unknown): string {
    const text = typeof data === 'string' ? data : JSON.stringify(data);
    if (text == null) return '<empty>';
    return text.length > 300 ? `${text.slice(0, 300)}…(${text.length} chars)` : text;
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
    if (!g || !this.isOwner() || !g.designatedHunterUserId) return;
    try {
      // The owner is a participant too, so the resulting `game-started` SSE event drives
      // navigation for everyone (owner included) via onGameStarted — no manual nav here.
      const game = await this.gamesService.startGame(this.gameId(), g.designatedHunterUserId);
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
