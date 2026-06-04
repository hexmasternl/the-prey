# Game Start View — Design

## Context

The Games server module is complete for the core lifecycle: `POST /games` (create, empty lobby), `POST /games/{id}/lobby` (join), `POST /games/{id}/start` (designate hunter, requires ≥2 lobby players), `GET /games/{id}` (full game incl. lobby), plus state/location endpoints used by the background `GameEngineService`. Games are persisted in PostgreSQL via EF Core.

What is missing:

- The game model has **no shareable game code** — only a Guid. The lobby flow needs an 8-digit code players can read out loud.
- The creator is **not in the lobby** after creation; the server requires ≥2 lobby players to start, so the creator must join too.
- The app has **no UI** for creating a game. `MainPage.OnPlayClicked` shows a "coming soon" alert. App-side `IGameService` only has the engine methods (`PushLocationAsync`, `GetGameStateAsync`).

Relevant conventions (CLAUDE.md + hexmaster guidelines): CQRS via `ICommandHandler`/`IQueryHandler`, Minimal APIs, OTel on every handler, xUnit+Moq+Bogus tests, MAUI pages with logic in services, `AppLocalizer` strings (en + nl), singletons for services / transients for pages.

The `playfield-select-view` change (separate, may land before or alongside this one) provides `PlayfieldSelectPage` + `PlayfieldSelectionContext` for picking the playfield a game runs on.

## Goals / Non-Goals

**Goals:**

- A player can configure and create a game from the app and land in a lobby view showing the 8-digit code.
- The server assigns a unique random 8-digit game code at creation and adds the creator to the lobby atomically.
- The lobby view live-updates the player list, lets the owner pick the hunter by tapping a name, and starts the game once ≥2 players joined.
- After start, the create-game navigation stack closes and a Game Progress placeholder page shows.

**Non-Goals:**

- Joining a game by code from another device (future `join-game` change — the code is displayed for that future flow).
- The actual Game Progress UI (map, countdowns, hunter/prey state) — placeholder only; the background `GameEngineService` integration with that page is a separate change.
- Real-time push (SignalR/WebSocket) lobby updates — polling is sufficient at lobby scale.
- Changing server-side configuration validation rules — the picker choices all satisfy the existing rules.

## Decisions

### D1 — Game code: 8-digit numeric string, generated server-side, unique index + collision retry

- `Game` gains `GameCode` (string, exactly 8 digits, may include leading zeros — hence string, not int). Generated in the `CreateGameCommandHandler` with a cryptographic RNG (`RandomNumberGenerator.GetInt32`), not derived from the id.
- Postgres column `game_code` with a **unique index**. The handler retries generation a bounded number of times (e.g. 5) when the insert hits the unique violation; with 10^8 codes and low game volume, collisions are vanishingly rare.
- `GameDto` and `GameSummaryDto` expose `GameCode`.
- *Alternative considered*: alphanumeric code (more entropy in fewer chars) — rejected; the user explicitly asked for 8 digits, and digits are easier to read out / type on a numpad.
- *Alternative considered*: uniqueness among active games only — rejected for now; a global unique index is simpler and capacity (100M) is not a concern.

### D2 — Creator joins the lobby server-side at creation (atomic), not via a second app call

`CreateGameRequest` gains `DisplayName` (required) and `ProfilePictureUrl` (optional); the handler adds the creator as the first lobby player in the same persistence operation. This avoids a create-then-join race and the broken intermediate state of an empty-lobby game when the second call fails.

- *Alternative considered*: app calls `POST /games/{id}/lobby` right after create — rejected: two round-trips, and a failure leaves an unjoinable-looking game; server-side is one transaction.

### D3 — Hunter designation is local UI state in the lobby view, sent on start

The server already takes `HunterUserId` in `StartGameRequest` and only designates roles at start. The lobby view defaults the hunter selection to the creator ("configured to be a hunter") and tapping a player moves the selection; nothing is sent to the server until **Start now**. No new server endpoint needed.

- *Alternative considered*: persisting hunter pre-selection on the game so other lobby members see it — deferred; without the join-by-code flow there are no other live lobby viewers yet.

### D4 — Lobby refresh via polling `GET /games/{id}`

The lobby page polls the existing `GetGame` endpoint every ~5 seconds (timer paused when the page is not visible). The response carries the full lobby and the status; if status flips to `InProgress` (someone else started — not possible yet, but cheap to honor) the page navigates forward the same as a local start.

- *Alternative considered*: SignalR — overkill for a lobby of a handful of players; revisit with the Game Progress work.

### D5 — Pickers in minutes; convert to API units on submit

The view presents all five timing options in minutes (per the request). The API expects `GameDuration`, `HunterDelayTime`, `FinalStageDuration` in **minutes** and the two location intervals in **seconds**. The page maps the interval picks (3/5/10 → 180/300/600; 1/2/3 → 60/120/180) when building `CreateGameRequest`. Defaults: 60 / 10 / 10 / 300 / 120, both penalty toggles **true** (sent explicitly — the server defaults them to false when omitted).

Fixed-choice selection uses segmented-button-style option rows (styled `Button`s/`RadioButton`s per the app's existing design tokens), not free-form entry — every combination of offered values passes server validation by construction.

### D6 — Navigation shape

- `MainPage` Play → `GameStartPage` (route `game-start`). If no playfield is selected yet, the page surfaces a "choose playfield" affordance that pushes `PlayfieldSelectPage` (from `playfield-select-view`); Create stays disabled until a playfield is selected. If that change has not landed yet, the affordance is the integration point and Create remains disabled.
- Create success → navigate to `GameLobbyPage` (route `game-lobby`), replacing the start page in the stack so back from the lobby doesn't return to a stale form.
- Start success (or observed `InProgress`) → navigate to `GameProgressPage` (route `game-progress`) with the navigation stack reset (`//`-style or `PopToRoot` + push) so all create-game views are gone.
- New singleton `GameCreationContext` (mirrors `PlayfieldEditingContext`) carries the created `Game` model between pages instead of serializing it through query params.

### D7 — App service surface

Extend `IGameService`/`GameService` (existing typed HTTP client, uses `IAuthService.GetAccessTokenAsync()`):

- `CreateGameAsync(CreateGameOptions)` → `Game` (POST /games)
- `GetGameAsync(Guid id)` → `Game?` (GET /games/{id}, null on 404)
- `StartGameAsync(Guid id, Guid hunterUserId)` → `Game?` (POST /games/{id}/start)

New app models: `Game`, `GameLobbyPlayer` (id, display name, profile picture) — plain classes in `Models/`, mapped from the API JSON. The creator's `DisplayName` comes from the authenticated user profile via `IAuthService`.

### D8 — Server tests

New/updated tests in `HexMaster.ThePrey.Games.Tests`: game-code format (8 digits), regeneration on collision (repository mock signaling duplicate), creator present in lobby after create, `DisplayName` validation, DTO mapping carries `GameCode`. Existing `CreateGameCommandHandlerTests` updated for the new command shape.

## Risks / Trade-offs

- [Game-code collision under concurrency] → unique index + bounded retry in the handler; failure after retries surfaces as a 500 (acceptable at this scale, logged via OTel).
- [`playfield-select-view` not yet implemented] → `GameStartPage` keeps Create disabled without a selection and isolates the dependency behind the selection-context integration point; the rest of the flow is fully buildable and testable.
- [EF migration adds a non-null unique column to an existing table] → dev/test databases only at this stage; migration backfills existing rows with generated codes (or the table is empty in practice). No production data exists.
- [Polling battery/network cost] → 5s interval only while the lobby page is visible; timer stops on `OnDisappearing`.
- [Breaking API change to `CreateGameRequest` (new required `DisplayName`)] → the app is the only client and ships in lockstep with the server; no versioning needed.

## Open Questions

- None blocking. Whether other lobby members should see the pending hunter selection is deferred to the join-game change.
