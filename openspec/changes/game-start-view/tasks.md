# Game Start View — Tasks

## 1. Server — game code & creator auto-join (Games module)

- [x] 1.1 Add `GameCode` to the `Game` domain model (string, exactly 8 decimal digits, leading zeros allowed) with validation; expose it through `GameDto` and `GameSummaryDto` and update `GameMappings`
- [x] 1.2 Extend `CreateGameRequest` and `CreateGameCommand` with `DisplayName` (required, non-empty) and optional `ProfilePictureUrl`; update `GameEndpoints.CreateGame` mapping
- [x] 1.3 Update `CreateGameCommandHandler`: generate the game code with `RandomNumberGenerator`, add the creator as the first lobby player, and retry code generation (bounded, e.g. 5 attempts) when the repository reports a duplicate code; instrument with OTel activity tags/status per guidelines
- [x] 1.4 Persistence: add `GameCode` column with a unique index in `GamesDbContext`/`GameEntityTypeConfiguration`, surface duplicate-code violations from `GameRepository` distinguishably, and add the EF Core migration
- [x] 1.5 Update/extend unit tests in `HexMaster.ThePrey.Games.Tests`: code format, collision retry, creator present in lobby after create, empty-display-name rejection, DTO mapping carries `GameCode`; update `GameFaker` and existing `CreateGameCommandHandlerTests`
- [x] 1.6 Run `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/` — all green

## 2. App — service layer & models

- [x] 2.1 Add app models: `Game` (id, game code, playfield id, owner id, status, lobby, configuration essentials) and `GameLobbyPlayer` (user id, display name, profile picture url) in `Models/`
- [x] 2.2 Extend `IGameService`/`GameService` with `CreateGameAsync`, `GetGameAsync` (null on 404), and `StartGameAsync`, using `IAuthService.GetAccessTokenAsync()` for tokens and converting interval minutes→seconds at the call boundary
- [ ] 2.3 Add singleton `GameCreationContext` service carrying the created game between the start, lobby, and progress pages; register in `MauiProgram.cs`

## 3. App — Start Game view

- [x] 3.1 Create `GameStartPage` (XAML + code-behind): fixed-choice option rows for the five timing options (defaults 60/10/10/5/2 minutes) and the two penalty toggles (default on), playfield picker affordance showing the selected playfield name, Create button
- [x] 3.2 Wire playfield requirement: Create disabled until a playfield is selected via the playfield selection context (`playfield-select-view` integration point); show selected name when set
- [x] 3.3 Implement Create: busy state, build request (explicit penalty booleans, converted intervals), call `CreateGameAsync`, store result in `GameCreationContext`, navigate to the lobby view replacing the start page in the stack; on failure show localized error and preserve selections
- [x] 3.4 Register `game-start` route in `AppShell`, transient page DI in `MauiProgram.cs`, and wire `MainPage.OnPlayClicked` to navigate to it

## 4. App — Waiting for Players (lobby) view

- [x] 4.1 Create `GameLobbyPage` (XAML + code-behind): game code on top, player list, Start now button at the bottom; register `game-lobby` route + DI
- [x] 4.2 Implement lobby polling: refresh via `GetGameAsync` every ~5s while visible, stop on `OnDisappearing`; update the player list from the response
- [x] 4.3 Implement hunter designation: creator marked as hunter by default, tapping a player moves the designation, exactly one designee with a visual marker
- [x] 4.4 Implement Start now: enabled only with ≥2 lobby players, sends `StartGameAsync` with the designated hunter, disabled while in flight; on failure show localized error and keep polling
- [x] 4.5 On successful start (or polled status no longer Lobby): navigate to the Game Progress placeholder with the create-game pages removed from the stack (back returns to the main menu)

## 5. App — Game Progress placeholder & localization

- [x] 5.1 Create placeholder `GameProgressPage` (title + game code, content deferred to a future change); register `game-progress` route + DI
- [x] 5.2 Add all new user-visible strings to `AppResources.resx` and `AppResources.nl.resx` and expose them via `AppLocalizer`

## 6. Verification

- [ ] 6.1 Build the server: `dotnet build src/Games/HexMaster.ThePrey.Games.Api/` and rerun the Games test suite
- [ ] 6.2 Build the MAUI app: `dotnet build src/App/ThePrey.Application.App/ThePrey.Application.App.csproj -f net10.0-android`
- [ ] 6.3 Manual flow check via Aspire: Play → configure → Create → lobby shows 8-digit code with creator listed → (second user joins via API) → Start now enabled → start → Game Progress placeholder shows, back goes to main menu
