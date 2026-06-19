import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '@auth0/auth0-angular';
import { firstValueFrom } from 'rxjs';
import { filter, take } from 'rxjs/operators';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonSpinner,
  IonToolbar,
  ViewWillEnter,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { chevronBack } from 'ionicons/icons';
import { TranslatePipe } from '@ngx-translate/core';
import { GameDto, GamesService } from './games.service';
import { UserStateService } from '../users/user-state.service';
import { getAppVersion, openPlayStore } from '../shared/app-update.util';

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
    IonIcon,
    IonSpinner,
  ],
})
export class GameJoinPage implements ViewWillEnter {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly gamesService = inject(GamesService);
  private readonly userState = inject(UserStateService);

  constructor() {
    addIcons({ chevronBack });
  }

  readonly gameId = signal<string | null>(this.route.snapshot.queryParamMap.get('gameId'));
  readonly joinCode = signal('');
  readonly isLoading = signal(false);
  readonly isSubmitting = signal(false);
  readonly game = signal<GameDto | null>(null);
  readonly errorWrongCode = signal(false);
  readonly gameNotFound = signal(false);
  readonly gameStarted = signal(false);
  /** Translation key for a join failure shown to the user (full lobby, server error, …); null when none. */
  readonly joinErrorKey = signal<string | null>(null);

  /** False until the version check resolves; the Join button stays disabled until then. */
  readonly versionChecked = signal(false);

  /** True only when the server returned 409 — the app must be updated to continue. */
  readonly updateRequired = signal(false);

  /**
   * Disabled by default: blocks the Join button while the version check is in flight
   * (`!versionChecked()`) and whenever an update is required. Any non-409 outcome clears it.
   */
  readonly versionBlocked = computed(() => !this.versionChecked() || this.updateRequired());

  readonly callsign = computed(() => this.userState.profile()?.callsign ?? null);

  readonly canJoin = computed(
    () =>
      this.joinCode().length === 4 &&
      !this.isSubmitting() &&
      this.gameId() != null &&
      this.callsign() != null &&
      this.game() != null &&
      !this.gameNotFound() &&
      !this.gameStarted() &&
      !this.versionBlocked(),
  );

  async ionViewWillEnter(): Promise<void> {
    // Gate the Join button on the version check (disabled by default until it resolves).
    void this.runVersionCheck();

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

    // This page is reachable via a shared join link, so the visitor may not be
    // authenticated yet. Attempt a silent session restore first; if there is no
    // valid session, send them to login and return here once they sign in. Only
    // fetch the game once we actually hold a token — otherwise the request fails
    // authentication and the game looks (wrongly) like it doesn't exist.
    const authenticated = await this.restoreSession();
    if (!authenticated) {
      this.isLoading.set(false);
      await this.router.navigate(['/login'], {
        queryParams: { returnUrl: this.router.url },
      });
      return;
    }

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

  /**
   * Report whether a session exists, waiting for the Auth0 SDK to finish its startup
   * session check (the silent refresh-token restore) first. We read `isAuthenticated$`
   * rather than calling `getAccessTokenSilently()` so this agrees with the login page's
   * own signal — otherwise the two disagree and bounce the user between join and login
   * in a loop.
   */
  private async restoreSession(): Promise<boolean> {
    await firstValueFrom(this.auth.isLoading$.pipe(filter((loading) => !loading), take(1)));
    return firstValueFrom(this.auth.isAuthenticated$.pipe(take(1)));
  }

  onCodeInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const digits = raw.replace(/\D/g, '').slice(0, 4);
    this.joinCode.set(digits);
    (event.target as HTMLInputElement).value = digits;
    this.errorWrongCode.set(false);
    this.joinErrorKey.set(null);
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
    this.joinErrorKey.set(null);
    try {
      const joined = await this.gamesService.joinGame(gameId, this.joinCode(), callsign);
      await this.router.navigate(['/games', joined.id, 'lobby']);
    } catch (err: unknown) {
      // The server returns a stable ProblemDetails `code`; map each to the right UI reaction.
      switch (this.errorCode(err)) {
        case 'player_already_joined':
          // Already a member of this lobby — just take them in.
          await this.router.navigate(['/games', gameId, 'lobby']);
          return;
        case 'game_already_started':
          this.gameStarted.set(true);
          return;
        case 'game_not_found':
          this.gameNotFound.set(true);
          return;
        case 'invalid_join_code':
          this.errorWrongCode.set(true);
          return;
        case 'lobby_full':
          this.joinErrorKey.set('GAME_JOIN.ERROR_FULL');
          return;
        default:
          this.joinErrorKey.set('GAME_JOIN.ERROR_SERVER');
          return;
      }
    } finally {
      this.isSubmitting.set(false);
    }
  }

  /**
   * Post the local version to the server gate. The Join button is disabled until this resolves;
   * only a 409 keeps it disabled (updateRequired), every other outcome enables it (fail-open).
   */
  private async runVersionCheck(): Promise<void> {
    try {
      const version = await getAppVersion();
      const result = await this.gamesService.checkAppVersion(version);
      this.updateRequired.set(result === 'update-required');
    } finally {
      this.versionChecked.set(true);
    }
  }

  /** Open the Play Store listing from the update-required banner. */
  openStore(): Promise<void> {
    return openPlayStore();
  }

  back(): void {
    this.router.navigate(['/home']);
  }

  /** Reads the stable error `code` from a ProblemDetails HTTP error body, if present. */
  private errorCode(err: unknown): string | null {
    if (err && typeof err === 'object' && 'error' in err) {
      const body = (err as { error: unknown }).error;
      if (body && typeof body === 'object' && 'code' in body) {
        const code = (body as { code: unknown }).code;
        return typeof code === 'string' ? code : null;
      }
    }
    return null;
  }
}
