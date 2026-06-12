import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { IonContent, IonButton } from '@ionic/angular/standalone';
import { TranslatePipe } from '@ngx-translate/core';
import { GamesService } from './games.service';
import { UserStateService } from '../users/user-state.service';

type Verdict = 'victory' | 'defeat' | 'aborted' | 'complete';
type Role = 'hunter' | 'prey';

/**
 * Post-game debrief screen. Shown when a game ends (the `game-ended` event), replacing
 * the old straight-to-home navigation. The originating hunter/prey page passes the
 * server `outcome` and `survivorCount` plus the viewer's role as query params for an
 * instant render; the page then best-effort confirms them against the live game record
 * (so a direct navigation / refresh of this URL still resolves). Closing returns to home.
 */
@Component({
  selector: 'app-game-outcome',
  templateUrl: 'game-outcome.page.html',
  styleUrls: ['game-outcome.page.scss'],
  imports: [IonContent, IonButton, TranslatePipe],
})
export class GameOutcomePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gamesService = inject(GamesService);
  private readonly userState = inject(UserStateService);

  private gameId = '';

  /** The role the viewer played — drives whether an outcome reads as victory or defeat. */
  readonly role = signal<Role>('prey');
  /** Raw server outcome: HuntersWin | PreysWin | Cancelled | Undecided. */
  readonly outcome = signal<string>('Undecided');
  /** Number of prey still alive when the game ended (null until known). */
  readonly survivors = signal<number | null>(null);

  /** The viewer-relative verdict, derived from the outcome and the role they played. */
  readonly verdict = computed<Verdict>(() => {
    switch (this.outcome()) {
      case 'Cancelled':
        return 'aborted';
      case 'HuntersWin':
        return this.role() === 'hunter' ? 'victory' : 'defeat';
      case 'PreysWin':
        return this.role() === 'prey' ? 'victory' : 'defeat';
      default:
        return 'complete';
    }
  });

  /** Translation key for the big headline word. */
  readonly headlineKey = computed(() => {
    switch (this.verdict()) {
      case 'victory':
        return 'GAME_OUTCOME.VICTORY';
      case 'defeat':
        return 'GAME_OUTCOME.DEFEAT';
      case 'aborted':
        return 'GAME_OUTCOME.ABORTED';
      default:
        return 'GAME_OUTCOME.COMPLETE';
    }
  });

  /** Translation key describing the overall result, independent of the viewer's role. */
  readonly summaryKey = computed(() => {
    switch (this.outcome()) {
      case 'HuntersWin':
        return 'GAME_OUTCOME.HUNTERS_WIN';
      case 'PreysWin':
        return 'GAME_OUTCOME.PREYS_WIN';
      case 'Cancelled':
        return 'GAME_OUTCOME.CANCELLED';
      default:
        return 'GAME_OUTCOME.UNDECIDED';
    }
  });

  readonly roleKey = computed(() =>
    this.role() === 'hunter'
      ? 'GAME_OUTCOME.ROLE_HUNTER'
      : 'GAME_OUTCOME.ROLE_PREY',
  );

  async ngOnInit(): Promise<void> {
    this.gameId = this.route.snapshot.paramMap.get('id') ?? '';

    // Fast path: values handed over by the originating game page on the game-ended event.
    const qp = this.route.snapshot.queryParamMap;
    const role = qp.get('role');
    if (role === 'hunter' || role === 'prey') this.role.set(role);
    const outcome = qp.get('outcome');
    if (outcome) this.outcome.set(outcome);
    const survivors = qp.get('survivors');
    if (survivors !== null && survivors !== '') this.survivors.set(Number(survivors));

    // Authoritative confirmation / fill-in from the server (best-effort).
    await this.refreshFromServer();
  }

  /**
   * Confirm the outcome against the live game record. Handles a cold navigation to this
   * URL (no query params) and reconciles the role from the authoritative hunter id. The
   * game may already have been cleaned up server-side, so failures fall back silently to
   * whatever the query params provided.
   */
  private async refreshFromServer(): Promise<void> {
    if (!this.gameId) return;
    try {
      const game = await this.gamesService.getGame(this.gameId);
      if (game.outcome) this.outcome.set(game.outcome);

      const myId = this.userState.profile()?.userId;
      if (myId && game.hunterUserId) {
        this.role.set(game.hunterUserId === myId ? 'hunter' : 'prey');
      }

      if (this.survivors() === null) {
        const alive = game.participants.filter(
          (p) =>
            p.userId !== game.hunterUserId &&
            (p.state === 'Active' || p.state === 'Passive'),
        ).length;
        this.survivors.set(alive);
      }
    } catch {
      // Game record gone or unreachable — keep the query-param values.
    }
  }

  returnToBase(): void {
    this.router.navigate(['/home'], { replaceUrl: true });
  }
}
