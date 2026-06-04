## Context

`MainPage` is the app's first screen. When it appears unauthenticated it routes to the landing/login page; after a successful login or silent session restore the player lands back on the main menu. The Games module exposes `GET /games` (owned + joined summaries) and `GET /games/{id}`, but nothing answers "is there a hunt I should be in right now?". The Game Progress view (game-progress-view change) is the navigation target and already accepts game id, role, and playfield id; the game engine (`IGameEngineService`) is started by that page and is idempotent.

## Goals / Non-Goals

**Goals:**
- One server query that returns the caller's active game or 404.
- Automatic, silent resume: authenticated main menu → check → navigate into the running game.
- Never trap the player: deliberate back-navigation from the Game Progress view must not bounce them back in.
- Non-blocking failure behavior: server/network errors leave the menu fully usable.

**Non-Goals:**
- Resuming Lobby-state games (returning to a waiting lobby is a separate concern).
- Push notifications or background polling for game start while the app is closed.
- Multi-game arbitration UI: with several active games the server picks the most recently started.
- Any change to the Game Progress view itself.

## Decisions

### 1. Dedicated `GET /games/active` endpoint over client-side filtering of `GET /games`

**Decision**: Add a `GetActiveGameQuery(UserId)` slice returning the caller's single active game (`GameDto`) — the InProgress game where the caller is a participant; most recently started wins; null → 404.

**Rationale**: `GET /games` returns summaries of owned + joined games, which over time is a growing list the client would have to filter, and the summary lacks participant detail needed to derive the role. A dedicated query does the filtering where the data lives and returns the full `GameDto` in one round trip — exactly what the user asked for ("the server returns the game status").

**Alternative**: reuse `ListGames` + `GetGame` per candidate — rejected: N+1 requests on app start for stale-game-heavy accounts.

### 2. Repository-level lookup

**Decision**: `IGameRepository` gains `FindActiveForUserAsync(Guid userId, CancellationToken ct)` implemented in the Postgres adapter (filter `Status == InProgress` and participant match, order by `StartedAt` descending, take 1).

**Rationale**: Filtering in SQL avoids materializing every game a player ever touched. The repository interface stays in the module project per the project's port placement rule.

**Alternative**: reuse `ListForUserAsync` in the handler and filter in memory — acceptable fallback, but it loads complete aggregates (locations, penalties) for finished games too.

### 3. Route shape: `/games/active` alongside `/games/{id:guid}`

**Decision**: Map `GET /games/active` in the same group. The existing `{id:guid}` constraint guarantees "active" can never be parsed as a game id, so no ordering hack is needed.

### 4. Check runs in `MainPage.OnAppearing`, guarded

**Decision**: When the main menu appears and `IAuthService.IsAuthenticated` is true, the page fires a single non-blocking active-game check. Guards:
- skip when `GameStateContext.IsRunning` — the engine is already driving a session, so the player either is in the game or deliberately stepped out of the view;
- skip when a check is already in flight;
- a failed or 404 check re-arms, so the next `OnAppearing` (e.g. after a later login) tries again.

**Rationale**: `OnAppearing` is the single funnel for "user is on the main menu", covering both the post-login return (`GoToAsync("..")`) and the silent-restore return. The `IsRunning` guard is what prevents the bounce-loop: Game Progress → back → main menu → auto-navigate → … 

**Alternative**: hook the check into `LandingPage` right after `RestoreSessionAsync`/`LoginAsync` — rejected: two call sites instead of one, and it misses sessions that authenticate by other future means.

### 5. Role derivation via `IAuthService.UserId`

**Decision**: `IAuthService` exposes `UserId` (nullable `Guid`), parsed from the access token's `sub` claim (the service already base64-decodes the payload for expiry checks). The page compares it with `game.Hunter?.UserId` → `PlayerRole.Hunter`, otherwise `PlayerRole.Prey`.

**Rationale**: The role is not stored client-side and must survive app restarts; the token is the authoritative identity source the app already holds.

**Alternative**: have `GET /games/active` return a `yourRole` field — also valid; rejected to keep `GameDto` unchanged and because the user id has further uses (e.g. lobby self-highlighting).

### 6. Silent failure semantics

**Decision**: `GetActiveGameAsync` returns null on 404; network/5xx errors are caught in the page and treated as "no active game for now" (menu unaffected, retry on next appearance). `UnauthorizedException` also degrades silently — the existing menu lock/login flow handles unauthenticated state.

**Rationale**: An app-start convenience check must never block or alarm the player.

## Risks / Trade-offs

- **Bounce protection relies on `IsRunning`** → if the engine ever stops while the player remains on a non-game page (e.g. auth failure stop), the next menu visit auto-navigates again — acceptable, arguably desirable (the game *is* still active server-side).
- **Stale active game (ended seconds ago)** → the Game Progress page's own state sync receives 404/`GameEnded` and exits cleanly; no extra handling needed here.
- **Several active games** → server picks most recently started; edge case for players juggling games on one account. Documented, not surfaced in UI.
- **`sub` claim not a GUID** (e.g. future social logins) → `UserId` is nullable; a null user id skips auto-resume rather than guessing a role.
- **Parallel in-flight changes** (game-start-view, game-progress-view are unarchived) → this change only *consumes* `GameProgressPage` navigation; implementation should follow whichever route contract that change lands.

## Open Questions

- Should Lobby-state games also resume (back into the waiting lobby)? Deferred — needs the join-by-code flow story first.
- Is a brief "resuming game…" indicator wanted on the menu while the check runs? Current design: fully silent.
