## Why

The MAUI main menu already routes a signed-in player with no active game to a **Start Game** button, but the destination (`StartGamePage`) is only a placeholder — there is no way to actually configure and create a game. Players need to pick the game's tuning values (duration, headstart, endgame, GPS ping cadence) and choose the playfield to play in, then create the game so it can move on to the lobby.

## What Changes

- Replace the placeholder `StartGamePage` with a real **game-configuration page**, reached from the existing `start-game` route (shown when the user is signed in with no active game).
- The page presents **five option selectors**, each a fixed set of choices with a default pre-selected:
  - **Duration** — `30 / 60 / 90` minutes (default **30**).
  - **Headstart Time** — `5 / 10 / 15` minutes (default **5**).
  - **Duration Endgame** — `5 / 10 / 15` minutes (default **10**).
  - **GPS Ping interval** — `2 / 3 / 5` minutes (default **2**).
  - **GPS Ping at endgame** — `1 / 2 / 3 / 5` minutes (default **1**).
- **Playfield selection**: a row that opens a **playfield picker**. The picker opens showing the **playfields the current user created**. A **search bar** (minimum **3 characters**, **300 ms** debounce) searches across the user's **own (private + public)** playfields **and** public playfields from other owners, merged and de-duplicated. Tapping a playfield selects it and returns to the config page, where the chosen playfield's name is shown.
- A **Create Game** action, enabled only once a playfield is selected, sends `POST /games` with the chosen configuration and playfield; on `201 Created` it navigates to the game/lobby (`game` route). Validation / unauthorized / transient failures surface as error states without leaving the page.
- Extend the game client seam (`IGameApiClient`) with a **create-game** call that maps `201`/`400`/`401`/error to typed results. Extend the playfield client usage to drive the merged picker search.
- **Ping intervals are entered in minutes but sent in seconds** — the backend's `DefaultLocationInterval` / `FinalLocationInterval` are documented in seconds, so the app multiplies the chosen minutes by 60 before sending. Durations (game/headstart/endgame) are already in minutes and sent as-is.

## Capabilities

### New Capabilities
- `maui-game-create`: The game-configuration page reached from the Start Game button — the five option selectors and their defaults, the selected-playfield display, `Create Game` enablement (a playfield must be selected), sourcing the caller's display name, the minutes→seconds ping conversion, the authenticated `POST /games` create call with its `201`/`400`/`401`/error result mapping, and navigation to the game route on success.
- `maui-game-playfield-picker`: The playfield picker opened from the config page — the initial list of the user's own created playfields, the 3-character-minimum, 300 ms-debounced search that merges the user's own (private + public) playfields with public playfields from other owners (de-duplicated by id), selecting a playfield and returning it to the config page, and the empty / no-results / error states.

### Modified Capabilities
<!-- None as spec deltas. This change depends on the `main-menu-page`, `maui-playfields-list-page`, and `game-app-backend-service`/`game-start` changes for the `start-game` route, `IPlayFieldApiClient`, `IGameApiClient`, `IAccessTokenProvider`, and the `POST /games` contract; those capability specs are not yet archived, so the new behaviour is captured in the new capabilities above rather than as deltas to them. -->

## Impact

- **Depends on**:
  - `main-menu-page` — provides the `start-game` Shell route and the "signed-in, no active game" entry that reaches this page.
  - `maui-playfields-list-page` — provides `IPlayFieldApiClient` (`GetMyPlayFieldsAsync`, `SearchPublicPlayFieldsAsync`), `PlayFieldSummary`, `IAccessTokenProvider`, and the 300 ms / 3-char debounce pattern reused by the picker.
  - The backend `POST /games` (`CreateGame`) contract from the Games module: `RequireAuthorization()`, body `CreateGameRequest(PlayfieldId, DisplayName, GameDuration, HunterDelayTime, FinalStageDuration, DefaultLocationInterval, FinalLocationInterval, EnablePreyBoundaryPenalties=false, EnableHunterBoundaryPenalty=false, ProfilePictureUrl=null)`, returning `201 Created` + `GameDto`.
- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - `Pages/StartGamePage.xaml` (+ `.xaml.cs`): replace the placeholder with the five selectors, the playfield row, error/busy region, and the `Create Game` action.
  - New `Pages/SelectPlayfieldPage.xaml` (+ `.xaml.cs`): the picker with a search bar and result list.
  - New `ViewModels/StartGameViewModel.cs`: the five selected values (with defaults), the selected playfield, `CanCreate`, the ping minutes→seconds conversion, display-name sourcing, and the create command + result mapping.
  - New `ViewModels/SelectPlayfieldViewModel.cs`: the initial own-playfields load, the debounced merged search (`TimeProvider`-driven), result/empty/error states, and selection.
  - `Services/Api/IGameApiClient.cs` + `GameApiClient.cs`: add `CreateGameAsync(...)` → `CreateGameResult` (`Success(GameSummary)` / `Validation` / `Unauthorized` / `Error`) calling `POST /games`.
  - New `Services/Api/CreateGameResult.cs` and a minimal created-game projection (e.g. `GameSummary(Id)`) from the `201` `GameDto`.
  - `Services/Navigation/*`: a navigator seam for opening the picker and returning the selected `PlayFieldSummary`, and for navigating to the `game` route after create (mirrors the existing playfield navigators).
  - `Resources/Styles/Styles.xaml` + `Resources/Strings/*.resx`: styles for the selectors, playfield row, and picker, and localized strings for every label/option — no inline visual literals and no hard-coded user-facing text (single-source-of-truth styling + localization rules).
  - `MauiProgram.cs`: register the new page, view models, and picker route.
- **Backend**: no changes. Reuses `POST /games` (`CreateGame`).
- **Non-goals**: the lobby / game-in-progress pages and the join-code flow (separate changes); prey/hunter boundary-penalty toggles (sent as their `false` defaults); editing an existing game's settings; a map preview of the selected playfield; creating a new playfield from the picker (the list page owns creation); custom (free-entry) durations or intervals beyond the fixed option sets.
