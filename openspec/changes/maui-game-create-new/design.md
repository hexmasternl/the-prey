## Context

The MAUI client (`src/HexMaster.ThePrey.Maui.App`) already routes a signed-in player with no active game to a `start-game` Shell route (`MainMenuViewModel.StartGameRoute`), but `StartGamePage` is a placeholder. This change turns it into the real game-configuration page and wires the create call.

The backend contract already exists and is authoritative: `POST /games` (`RequireAuthorization()`), body `CreateGameRequest(Guid PlayfieldId, string DisplayName, int GameDuration, int HunterDelayTime, int FinalStageDuration, int DefaultLocationInterval, int FinalLocationInterval, bool EnablePreyBoundaryPenalties = false, bool EnableHunterBoundaryPenalty = false, string? ProfilePictureUrl = null)`, returning `201 Created` with a `GameDto`. Per `GameConfigurationDto`, **durations are in minutes** (`GameDuration`, `HunterDelayTime`, `FinalStageDuration`) and **location intervals are in seconds** (`DefaultLocationInterval`, `FinalLocationInterval`). The owner is taken from the authenticated caller and joins the lobby as its first player under `DisplayName`.

The client seams this builds on already exist from prior changes: `IPlayFieldApiClient` (with `GetMyPlayFieldsAsync`, `SearchPublicPlayFieldsAsync`), `PlayFieldSummary(Id, Name, IsPublic)`, `IAccessTokenProvider`, `IUserApiClient.GetCurrentUserAsync` (source of `DisplayName`), and `IGameApiClient` (currently only `GetActiveGameAsync`). `PlayFieldsListViewModel` establishes the reusable pattern for the picker's search: `MinimumSearchLength = 3`, `DebounceDelay = 300 ms`, a `TimeProvider`, a superseding `CancellationTokenSource`. The result-union client style (`Success/Validation/Unauthorized/Error`) is established by `GameApiClient`/`PlayFieldApiClient`.

## Goals / Non-Goals

**Goals:**
- A game-configuration page reached from the Start Game button, with five fixed-choice option selectors (each defaulted) and a selected-playfield row.
- A playfield picker that opens on the user's own created playfields and supports a 3-character-minimum, 300 ms-debounced search that merges the user's own (private + public) and other owners' public playfields.
- A `Create Game` action that sources the caller's display name, converts ping minutes → seconds, sends `POST /games`, maps the result to typed outcomes, and on success navigates to the game route.
- View models fully unit-testable without platform/HTTP (all navigation, HTTP, and time behind interfaces / `TimeProvider`).

**Non-Goals:**
- The lobby, countdown, and game-in-progress pages, and the join-code flow (separate changes) — this change stops at navigating to the `game` route on success.
- Prey/hunter boundary-penalty toggles — sent as their `false` defaults; no UI here.
- Editing an existing game's settings, a map preview of the selected playfield, or creating a new playfield from the picker.
- Free-entry (custom) durations or intervals; only the fixed option sets are offered.

## Decisions

### D1: Fixed option sets modelled as typed choice lists with defaults in the view model
Each selector is a small immutable list of allowed values with an index/value selected in `StartGameViewModel`, defaulted per the proposal (Duration 30, Headstart 5, Endgame 10, Ping 2, Endgame-ping 1). The view binds each to a segmented/radio-style control; the VM holds the current `int` minutes value. The allowed sets are constants on the VM (`DurationOptions`, `HeadstartOptions`, `EndgameOptions`, `PingOptions`, `EndgamePingOptions`) so both the UI and the tests read from one source.

- **Why:** fixed, low-cardinality choices; a pure value + option list is trivially testable and keeps the XAML declarative.
- **Alternative:** free numeric entry with validation — rejected (proposal specifies fixed choices; adds needless validation surface).

### D2: Ping intervals entered in minutes, sent in seconds
The two ping selectors present minutes; on create the VM multiplies the selected minutes by 60 to fill `DefaultLocationInterval` / `FinalLocationInterval`. The three duration values are already minutes and are sent unchanged.

- **Why:** the backend documents these two fields in seconds; converting at the boundary keeps the UI in the units the user picked while honouring the contract. (Confirmed with the product owner.)
- **Consequence:** a single conversion point in the VM's create mapping; unit-tested by asserting the request carries `minutes * 60`.

### D3: Playfield picker = own list first, merged debounced search
The picker opens by loading the user's own playfields via `GetMyPlayFieldsAsync` (these include the user's private and public ones). While the trimmed query is `< 3` chars the picker shows that own list. At `≥ 3` chars (debounced 300 ms), it runs **two** reads concurrently — `SearchPublicPlayFieldsAsync(query)` for other owners' public fields and a **local, case-insensitive contains-filter** of the already-loaded own list — then **merges and de-duplicates by `Id`** (own entries win, so an owned public field keeps its `IsPublic` badge). A new keystroke supersedes the in-flight search via a fresh `CancellationTokenSource`, mirroring `PlayFieldsListViewModel`.

- **Why:** reuses the two existing client methods with no new backend endpoint; "returns all private and public playfields that match" is satisfied by (own private + own public, filtered locally) ∪ (public search). Local filtering of the own list avoids a second server round-trip and naturally includes private fields the public search cannot return.
- **Alternative:** a new combined server search endpoint — rejected as backend work outside this change's scope.
- **Testability:** the debounce, supersede, merge, and dedup all live in `SelectPlayfieldViewModel` over `TimeProvider` + a mocked `IPlayFieldApiClient`.

### D4: Picker hand-off and post-create navigation via a navigator seam
Opening the picker and returning the chosen `PlayFieldSummary` uses a navigator interface (mirroring the existing `IEditPlayfieldNavigator` / `ShellPlayfieldNavigator` pattern) plus a lightweight result channel (`TaskCompletionSource` / `IQueryAttributable` result) so `StartGameViewModel` `await`s the selection. Cancelling the picker returns nothing and leaves the previously selected playfield unchanged. After a successful create, the same navigator seam navigates to the `game` route.

- **Why:** keeps the view models decoupled from Shell and fully unit-testable; consistent with the app's existing navigator seams.

### D5: `CreateGameAsync` mirrors the existing result-union client pattern
Add to `IGameApiClient`: `Task<CreateGameResult> CreateGameAsync(CreateGameParameters request, string accessToken, CancellationToken ct)` where `CreateGameResult` is `Success(GameSummary)` / `Validation` / `Unauthorized` / `Error`. `GameApiClient` POSTs a `CreateGameRequest`-shaped JSON body with a Bearer header and maps `201`→Success (deserialize `GameDto`, project to `GameSummary(Id)`), `400`→Validation, `401`→Unauthorized, network/timeout/other→Error (catch `HttpRequestException`/`TaskCanceledException`) — identical style to `GetActiveGameAsync`. `EnablePreyBoundaryPenalties`/`EnableHunterBoundaryPenalty` are sent as `false`; `ProfilePictureUrl` as `null`.

- **Why:** consistency with the existing client seam; the VM renders each outcome as a distinct state.

### D6: Display name sourced from the current user, resolved at create time
`DisplayName` is read via `IUserApiClient.GetCurrentUserAsync` when the create command runs (not stored on the page). If the user has no settings yet (`NotFound`) the VM falls back to a sensible default display name so create still succeeds; `Unauthorized` from this call is treated like the create's `Unauthorized` outcome.

- **Why:** the create request needs a display name the config page does not otherwise collect; sourcing it at create time avoids a stale copy and an extra field on the page.

### D7: `Create Game` enablement is derived
`CanCreate` = a playfield is selected **AND** not busy. All five selectors always have a valid default, so they never block create. `MaxPoints`-style thresholds do not apply here.

## Risks / Trade-offs

- **Ping unit mismatch (minutes vs seconds) is easy to get wrong** → single conversion point in the VM (D2), unit-tested by asserting `minutes * 60` in the outgoing request.
- **Merged picker search could show duplicates or drop private matches** → dedup by `Id` with own-entries-win, and filter the own list locally so private fields are always searchable (D3); covered by VM tests.
- **`POST /games` succeeds but the returned `GameDto` fails to deserialize** → treat as `Error` for the create result; since the server did create the game, the user can re-enter via the menu's Resume path (the active-game check will now find it). Documented, not silently dropped.
- **Display-name lookup fails or returns nothing** → fall back to a default display name (D6); never block create solely on the profile read, but surface `Unauthorized` as the unauthorized state.
- **Debounce / supersede races in the picker** → reuse the proven `TimeProvider` + superseding `CancellationTokenSource` pattern from `PlayFieldsListViewModel`; unit-tested including rapid keystrokes.
- **No active-game guard on create** → the menu only routes here when there is no active game; if the user somehow creates a second, the backend is authoritative and returns a validation/conflict status that surfaces as the Validation/Error state.

## Migration Plan

Pure client addition. No backend, schema, or contract changes. Ships behind the existing `start-game` route; replacing the placeholder page is backward-compatible. No rollback concerns beyond reverting the client change.

## Open Questions

- Whether the config page should also expose the two boundary-penalty toggles later — deferred (Non-Goal); currently sent as `false`.
- Exact fallback display name when the profile read returns `NotFound` (e.g. a localized "Player") — cosmetic, resolved during implementation; does not change the VM contract.
