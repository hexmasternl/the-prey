# Main View: Resume an Active Game

## Why

When a player kills or restarts the app (or their session is silently restored) while a hunt is running, the app currently dumps them on the main menu with no trace of the game — the engine is not running and the only way back is impossible because nothing navigates to the Game Progress view outside the create/start flow. The app must detect an ongoing game right after authentication and put the player back into it.

## What Changes

- **Server**: new `GET /games/active` endpoint that returns the caller's currently active game — the InProgress game in which the caller is a participant (hunter or prey) — or HTTP 404 when there is none.
- **App**: after the user logs in or a remembered session is restored (i.e. when the main menu is shown while authenticated), the app calls the new endpoint once:
  - when an active game is returned, the app derives the local player's role (by comparing its own user id with the game's hunter), and navigates to the **Game Progress view** with the game id, role, and playfield id;
  - when there is none (404), the main menu shows as today;
  - errors (network/server) are non-blocking: the main menu shows normally and the check is retried the next time the menu appears.
- **App**: `IAuthService` exposes the authenticated user's id (the token's `sub` claim) so the app can compare itself against game participants.
- The check never bounces a player who deliberately navigated back from the Game Progress view: when the game engine is already running, no auto-navigation happens.

## Capabilities

### New Capabilities

- `active-game-resume`: the app-side behavior — when and how the main view checks for an active game after authentication, role derivation from the user id, navigation to the Game Progress view, and the non-blocking error/skip rules.

### Modified Capabilities

- `games`: gains "retrieve the caller's active game" — a query returning the single InProgress game the authenticated caller participates in (most recently started when several exist), or 404.

## Impact

- **Server** (`src/Games/`):
  - New feature slice `Features/GetActiveGame/` (`GetActiveGameQuery` + handler), endpoint `GET /games/active` in `GameEndpoints`, registration in `GamesModuleRegistration`, OTel instrumentation, unit tests.
  - `IGameRepository` gains a lookup for a user's active game; implemented in `Games.Data.Postgres`.
  - Route ordering: `/games/active` must be mapped so it does not collide with `/games/{id:guid}` (the `:guid` constraint already prevents ambiguity).
- **App** (`src/App/ThePrey.Application.App/`):
  - `IGameService`/`GameService`: new `GetActiveGameAsync()`.
  - `IAuthService`/`AuthService`: new `UserId` property parsed from the access token's `sub` claim.
  - `MainPage`: active-game check on appearing while authenticated; navigation to `GameProgressPage`.
  - No new localized strings expected (silent check); add strings only if a transient "checking" indicator is desired.
- **Dependencies**: navigation target and parameters come from the `game-progress-view` change (`GameProgressPage` with game id, role, playfield id). The engine-running guard uses `GameStateContext.IsRunning` from app-background-service.
