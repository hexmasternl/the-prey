import { inject, Injectable, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { App } from '@capacitor/app';
import type { PluginListenerHandle } from '@capacitor/core';
import {
  GameConfigurationDto,
  GameDto,
  GameStateDto,
  GameStatusDto,
  GamesService,
  ParticipantDto,
} from './games.service';
import { GameStreamService, RealtimeEnvelope } from './game-stream.service';
import { UserStateService } from '../users/user-state.service';

/** Supported protocol major version — see `docs/api/realtime.md`. */
const PROTOCOL_VERSION = 1;

/** Periodic full-snapshot resync cadence mandated by the client-game-state-service spec. */
const PERIODIC_RESYNC_MS = 3 * 60_000;

/** Bounded exponential backoff for transient snapshot/token failures. */
const RETRY_MIN_DELAY_MS = 1_000;
const RETRY_MAX_DELAY_MS = 30_000;

/** One entry of a `locations-updated` batch. */
interface LocationEntry {
  userId: string;
  role: 'Hunter' | 'Prey' | string;
  latitude: number;
  longitude: number;
  state: string;
}

/** The game-level slice carried by `configuration-changed` (everything but participants). */
interface ConfigurationChangedPayload {
  id: string;
  gameCode: string;
  playfieldId: string;
  ownerUserId: string;
  status: string;
  configuration: GameConfigurationDto;
  hunterUserId: string | null;
  preys: string[];
  startedAt: string | null;
  createdAt: string;
  endsAt: string | null;
  cleanUpAfter: string;
  outcome: string;
  completedAt: string | null;
}

interface PreyUpdatedPayload {
  userId: string;
  event: 'tagged' | 'penalized' | 'penalty-cleared';
  state?: string | null;
  penaltyEndsAt?: string | null;
  reason?: string | null;
}

interface GameEndedPayload {
  outcome: string;
  survivorCount: number;
  completedAt: string | null;
}

/**
 * The single source-of-truth composite for the active game. `game.participants`
 * carries each participant's live location/state/penalty — `locations-updated` and
 * `prey-updated` deltas overlay directly onto it, so consumers read locations and
 * in-game state from `game.participants`, never from a separate copy.
 */
export interface GameLiveState {
  game: GameDto;
  /** Rich in-progress detail (poll cadence, playfield, hunterMayMoveAt, …) — null outside InProgress. */
  status: GameStatusDto | null;
  /** Role-specific view (hunter distance / prey locations) — null outside InProgress. */
  roleState: GameStateDto | null;
  /** Derived locally: sticky across snapshots once true, or ownerUserId === the caller. */
  isOwner: boolean;
  /** Epoch ms of the last applied `locations-updated` delta — null until one arrives. */
  lastLocationsUpdateAt: number | null;
}

/**
 * The app's single source of truth for the active game (`docs/api/realtime.md` §"The
 * client Game State Service"). Owns exactly one real-time connection (via
 * {@link GameStreamService}), loads a full snapshot on start, applies incoming protocol
 * deltas immutably onto that snapshot, and re-pulls a full snapshot on every (re)connect,
 * every three minutes, a sequence gap/regression, a `resync-requested` hint, or an
 * unsupported protocol version. The lobby, prey page, hunter page, and HUD all read
 * `state()` (or subscribe) instead of polling the server or opening their own connection.
 */
@Injectable({ providedIn: 'root' })
export class GameStateService {
  private readonly gamesService = inject(GamesService);
  private readonly stream = inject(GameStreamService);
  private readonly userState = inject(UserStateService);

  private readonly _state = signal<GameLiveState | null>(null);
  private readonly _unavailable = signal(false);

  /** The current composite snapshot, or null before the first load completes. */
  readonly state = this._state.asReadonly();
  /** True once a terminal authorization failure has been reported for this game. */
  readonly unavailable = this._unavailable.asReadonly();

  private gameId: string | null = null;
  private stopped = true;
  private lastSeq: number | null = null;
  private resyncTimer: ReturnType<typeof setInterval> | null = null;
  private resyncInFlight = false;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  /** Bumped on every start()/stop() so an in-flight retry loop from a previous game self-cancels. */
  private generation = 0;
  private resumeListener: PluginListenerHandle | null = null;

  private readonly subscribers = new Set<(state: GameLiveState) => void>();

  /**
   * Register for a push notification on every applied change. Subscribers are isolated —
   * one throwing does not prevent the others from being notified. Returns an unsubscribe
   * function; an unsubscribed consumer receives no further notifications.
   */
  subscribe(handler: (state: GameLiveState) => void): () => void {
    this.subscribers.add(handler);
    return () => this.subscribers.delete(handler);
  }

  /**
   * Starts (or, if already active for this exact game, no-ops) the single source of truth:
   * loads the full snapshot, opens the one real-time connection, and arms the periodic
   * resync. Safe to call from every page that depends on the active game — the lobby, prey,
   * and hunter pages all call this with the same game id as the player moves between them,
   * and only the first call for a given game actually (re)connects.
   */
  async start(gameId: string): Promise<void> {
    if (this.gameId === gameId && !this.stopped) return;
    this.teardown();

    this.gameId = gameId;
    this.stopped = false;
    this._unavailable.set(false);
    const myGeneration = ++this.generation;

    await this.loadSnapshotWithRetry(myGeneration);
    if (this.stopped || myGeneration !== this.generation) return;

    this.connectStream(gameId);
    this.armPeriodicResync();
    void this.registerResumeListener();
  }

  /** Stops the connection and all timers, and clears the held state. Call on final teardown. */
  stop(): void {
    this.teardown();
    this._state.set(null);
    this.gameId = null;
  }

  private teardown(): void {
    this.stopped = true;
    ++this.generation; // invalidate any in-flight load/retry loop
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
    if (this.resyncTimer) {
      clearInterval(this.resyncTimer);
      this.resyncTimer = null;
    }
    this.stream.disconnect();
    this.lastSeq = null;
    void this.resumeListener?.remove();
    this.resumeListener = null;
  }

  /**
   * Web PubSub delivers nothing while the app is backgrounded, and a socket suspended in the
   * background can be left silently half-open (its `onclose` never fires, so the transport's
   * own reconnect logic never kicks in). On resume we therefore force a fresh connection (not
   * just a resync) so a zombie socket can't strand the app. Centralized here — once — instead
   * of every page registering its own listener.
   */
  private async registerResumeListener(): Promise<void> {
    this.resumeListener = await App.addListener('appStateChange', ({ isActive }) => {
      if (isActive) this.onResume();
    });
  }

  private onResume(): void {
    if (this.stopped || !this.gameId) return;
    this.stream.connect(this.gameId);
    this.resyncNow();
  }

  private connectStream(gameId: string): void {
    this.stream.onMessage((envelope) => this.applyEnvelope(envelope));
    this.stream.onConnected(() => this.resyncNow());
    this.stream.onReconnected(() => this.resyncNow());
    this.stream.onUnavailable(() => this.markUnavailable());
    this.stream.connect(gameId);
  }

  private armPeriodicResync(): void {
    if (this.resyncTimer) clearInterval(this.resyncTimer);
    this.resyncTimer = setInterval(() => this.resyncNow(), PERIODIC_RESYNC_MS);
  }

  /** Fire-and-forget full resync; de-duplicated so overlapping triggers don't pile up requests. */
  private resyncNow(): void {
    if (this.stopped || this.resyncInFlight || !this.gameId) return;
    this.resyncInFlight = true;
    const myGeneration = this.generation;
    void this.loadSnapshotWithRetry(myGeneration).finally(() => {
      this.resyncInFlight = false;
    });
  }

  /**
   * Fetches a full snapshot and adopts it, retrying transient failures with bounded
   * exponential backoff. A terminal authorization failure (403 — no longer a member — or
   * 404/410, the game no longer exists for this caller) stops the service and reports
   * `unavailable` instead of retrying forever against a request that can never succeed.
   */
  private async loadSnapshotWithRetry(generation: number): Promise<void> {
    let attempt = 0;
    while (!this.stopped && generation === this.generation) {
      try {
        const { game, status, roleState } = await this.fetchOnce(this.gameId!);
        this.adoptSnapshot(game, status, roleState);
        return;
      } catch (err) {
        if (this.isTerminal(err)) {
          this.markUnavailable();
          return;
        }
        attempt++;
        const delay = Math.min(
          RETRY_MAX_DELAY_MS,
          RETRY_MIN_DELAY_MS * 2 ** Math.min(attempt - 1, 5),
        );
        await this.sleep(delay);
      }
    }
  }

  /**
   * One-shot manual resync for pull-to-refresh: a single attempt (no backoff loop, so the
   * refresher control doesn't hang), applied on success. Transient failures are swallowed —
   * the background retry/periodic timers keep the connection healthy regardless — while a
   * terminal failure still reports `unavailable` immediately.
   */
  async refreshNow(): Promise<void> {
    if (this.stopped || !this.gameId) return;
    try {
      const { game, status, roleState } = await this.fetchOnce(this.gameId);
      this.adoptSnapshot(game, status, roleState);
    } catch (err) {
      if (this.isTerminal(err)) this.markUnavailable();
    }
  }

  private async fetchOnce(
    gameId: string,
  ): Promise<{ game: GameDto; status: GameStatusDto | null; roleState: GameStateDto | null }> {
    const game = await this.gamesService.getGame(gameId);
    let status: GameStatusDto | null = null;
    let roleState: GameStateDto | null = null;
    if (game.status === 'InProgress') {
      status = await this.gamesService.getGameStatus(gameId).catch(() => null);
      roleState = await this.gamesService.getGameState(gameId).catch(() => null);
    }
    return { game, status, roleState };
  }

  /**
   * Applies a `GameDto` obtained directly from an HTTP command response (join, ready,
   * settings, hunter, start, remove-player — not a `GET /games/{id}` snapshot) so the
   * caller's own action is reflected instantly, without waiting for the resulting real-time
   * broadcast. Merges onto the current composite, preserving `status`/`roleState`.
   */
  applyOwnMutation(game: GameDto): void {
    const current = this._state();
    this.commit({
      game,
      status: current?.status ?? null,
      roleState: current?.roleState ?? null,
      isOwner: this.deriveOwner(game, current?.isOwner ?? false),
      lastLocationsUpdateAt: current?.lastLocationsUpdateAt ?? null,
    });
  }

  private adoptSnapshot(game: GameDto, status: GameStatusDto | null, roleState: GameStateDto | null): void {
    const next: GameLiveState = {
      game,
      status,
      roleState,
      isOwner: this.deriveOwner(game, this._state()?.isOwner ?? false),
      lastLocationsUpdateAt: this._state()?.lastLocationsUpdateAt ?? null,
    };
    // A snapshot has no seq of its own — reset the gap tracker so the next delta seeds a
    // fresh baseline instead of being compared against a seq from before the resync.
    this.lastSeq = null;
    this.commit(next);
  }

  private deriveOwner(game: GameDto, previousIsOwner: boolean): boolean {
    const currentUserId = this.userState.profile()?.userId ?? '';
    return game.isOwnerPlayer || previousIsOwner || (!!currentUserId && game.ownerUserId === currentUserId);
  }

  private markUnavailable(): void {
    this._unavailable.set(true);
    this.teardown();
  }

  /** 403 = not a member; 404/410 = the game no longer exists for this caller. Neither will heal on retry. */
  private isTerminal(err: unknown): boolean {
    return err instanceof HttpErrorResponse && (err.status === 403 || err.status === 404 || err.status === 410);
  }

  private sleep(ms: number): Promise<void> {
    return new Promise((resolve) => {
      this.retryTimer = setTimeout(resolve, ms);
    });
  }

  // ── Envelope application ────────────────────────────────────────────────

  private applyEnvelope(envelope: RealtimeEnvelope): void {
    if (envelope.v !== PROTOCOL_VERSION) {
      // Unsupported version — ignore the incremental effect and pull a full snapshot instead.
      this.resyncNow();
      return;
    }
    if (this.lastSeq !== null && (envelope.seq > this.lastSeq + 1 || envelope.seq <= this.lastSeq)) {
      // Gap or regression — the delta may be stale/out-of-order; resync instead of applying it.
      this.resyncNow();
      return;
    }
    this.lastSeq = envelope.seq;

    const current = this._state();
    if (!current) {
      this.resyncNow();
      return;
    }

    switch (envelope.type) {
      case 'participant-joined':
      case 'participant-changed':
        this.applyParticipantUpsert(current, envelope.data as ParticipantDto);
        break;
      case 'participant-removed':
        this.applyParticipantRemoved(current, (envelope.data as { userId: string }).userId);
        break;
      case 'configuration-changed':
        this.applyConfigurationChanged(current, envelope.data as ConfigurationChangedPayload);
        break;
      case 'locations-updated':
        this.applyLocationsUpdated(
          current,
          (envelope.data as { locations: LocationEntry[] })?.locations ?? [],
        );
        break;
      case 'prey-updated':
        this.applyPreyUpdated(current, envelope.data as PreyUpdatedPayload);
        break;
      case 'game-ended':
        this.applyGameEnded(current, envelope.data as GameEndedPayload);
        break;
      case 'resync-requested':
        this.resyncNow();
        break;
      default:
        // Unknown message type — protocol says ignore without disrupting the connection.
        break;
    }
  }

  private applyParticipantUpsert(current: GameLiveState, participant: ParticipantDto): void {
    if (!participant?.userId) return;
    const participants = current.game.participants.slice();
    const idx = participants.findIndex((p) => p.userId === participant.userId);
    if (idx >= 0) participants[idx] = participant;
    else participants.push(participant);
    this.commit({ ...current, game: { ...current.game, participants } });
  }

  private applyParticipantRemoved(current: GameLiveState, userId: string): void {
    const participants = current.game.participants.filter((p) => p.userId !== userId);
    this.commit({ ...current, game: { ...current.game, participants } });
  }

  private applyConfigurationChanged(current: GameLiveState, payload: ConfigurationChangedPayload): void {
    // The game-level slice deliberately omits participants and the per-caller
    // isOwnerPlayer/isReadyToStart flags — preserve them from the current state.
    const game: GameDto = {
      ...current.game,
      id: payload.id,
      gameCode: payload.gameCode,
      playfieldId: payload.playfieldId,
      ownerUserId: payload.ownerUserId,
      status: payload.status,
      configuration: payload.configuration,
      hunterUserId: payload.hunterUserId,
      preys: payload.preys,
      startedAt: payload.startedAt,
      createdAt: payload.createdAt,
      endsAt: payload.endsAt,
      cleanUpAfter: payload.cleanUpAfter,
      outcome: payload.outcome,
      completedAt: payload.completedAt,
    };
    this.commit({ ...current, game, isOwner: this.deriveOwner(game, current.isOwner) });
  }

  private applyLocationsUpdated(current: GameLiveState, locations: LocationEntry[]): void {
    if (!locations.length) return;
    const byId = new Map(locations.map((l) => [l.userId, l]));
    const participants = current.game.participants.map((p) => {
      const loc = byId.get(p.userId);
      if (!loc) return p;
      return {
        ...p,
        lastKnownLocation: { latitude: loc.latitude, longitude: loc.longitude },
        state: loc.state ?? p.state,
      };
    });
    this.commit({
      ...current,
      game: { ...current.game, participants },
      lastLocationsUpdateAt: Date.now(),
    });
  }

  private applyPreyUpdated(current: GameLiveState, payload: PreyUpdatedPayload): void {
    const participants = current.game.participants.map((p) => {
      if (p.userId !== payload.userId) return p;
      const next = { ...p };
      if (payload.event === 'tagged' && payload.state) next.state = payload.state;
      if (payload.event === 'penalized') next.hasActivePenalty = true;
      if (payload.event === 'penalty-cleared') next.hasActivePenalty = false;
      return next;
    });
    this.commit({ ...current, game: { ...current.game, participants } });
  }

  private applyGameEnded(current: GameLiveState, payload: GameEndedPayload): void {
    const game: GameDto = {
      ...current.game,
      status: 'Completed',
      outcome: payload.outcome,
      completedAt: payload.completedAt,
    };
    this.commit({ ...current, game });
  }

  private commit(next: GameLiveState): void {
    this._state.set(next);
    for (const handler of [...this.subscribers]) {
      try {
        handler(next);
      } catch (err) {
        console.error('[GameStateService] a subscriber threw; other subscribers are unaffected', err);
      }
    }
  }

  // ── Local per-recipient derivation helpers ──────────────────────────────

  /**
   * Participants the caller's role may see, per `docs/api/realtime.md`: a hunter renders
   * every prey blip; a prey renders only the hunter's blip. Always excludes the caller.
   */
  visibleParticipants(currentUserId: string): ParticipantDto[] {
    const s = this._state();
    if (!s) return [];
    const hunterUserId = s.game.hunterUserId;
    const iAmHunter = hunterUserId !== null && hunterUserId === currentUserId;
    return s.game.participants.filter((p) => {
      if (p.userId === currentUserId) return false;
      const isHunter = p.userId === hunterUserId;
      return iAmHunter ? !isHunter : isHunter;
    });
  }
}

/** Active preys = non-hunter participants still in play (Active/Passive). Derived, never stale. */
export function countActivePreys(game: GameDto): number {
  let count = 0;
  for (const p of game.participants) {
    if (game.hunterUserId !== null && p.userId === game.hunterUserId) continue;
    if (p.state === 'Active' || p.state === 'Passive') count++;
  }
  return count;
}
