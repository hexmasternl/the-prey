# Game Start View

## Why

The Games server module (create / join / start / state) exists and the app has a background game engine, but there is no way for a player to actually start a game from the app: the main menu "Play" button still shows a "coming soon" alert. This change builds the create-game flow — configuration view, server creation, waiting-for-players lobby — so a hunt can be set up end-to-end from the device.

## What Changes

- **New MAUI "Start Game" view** reachable from the main menu Play button. It offers fixed-choice pickers for the game configuration:
  - Game duration: 30 / 60 / 90 minutes (default **60**)
  - Hunter delay time: 5 / 10 / 15 minutes (default **10**)
  - Final stage duration: 5 / 10 / 15 minutes (default **10**)
  - Default location interval: 3 / 5 / 10 minutes (default **5**)
  - Final location interval: 1 / 2 / 3 minutes (default **2**)
  - `EnablePreyBoundaryPenalty` toggle (default **true**)
  - `EnableHunterBoundaryPenalty` toggle (default **true**)
- A **Create** button submits the configuration to the existing `POST /games` endpoint (intervals converted from minutes to the seconds the API expects). The server returns the created game, now including a unique random **8-digit game code**, and the creator is added to the lobby as a player with the **hunter** role pre-selected.
- **New "Waiting for Players" lobby view** shown after creation: game code on top, live player list (polled from the server), **Start now** button at the bottom. Start is enabled once at least two players (including the creator) are in the lobby. Tapping a player's name designates that player as the hunter.
- **Starting the game** calls the existing `POST /games/{id}/start` with the designated hunter; on success all create-game views close and a new **Game Progress** view is shown (placeholder page in this change; its content is a future change).
- **Server (`games` module)**: add a unique, randomly generated 8-digit game code assigned at creation, persisted with a uniqueness guarantee, and returned in the game model. Extend game creation so the creating player is atomically added to the lobby as its first player (creation request gains the creator's display name).
- The playfield for the game comes from the playfield selection flow (`playfield-select-view` change) — this view consumes the selected playfield and requires one before Create is enabled.

## Capabilities

### New Capabilities

- `game-creation-view`: the app's start-game view — configuration pickers with fixed choices and defaults, playfield requirement, Create button behavior, minute→second conversion, error handling, navigation to the lobby view.
- `game-lobby-view`: the app's waiting-for-players view — game code display, polled player list, hunter designation by tapping a player, Start-now gating (≥2 players, owner only), and post-start navigation to the Game Progress placeholder.

### Modified Capabilities

- `games`: game creation gains a unique random 8-digit game code on the game model, and the creating player is added to the lobby at creation time (request carries the creator's display name and optional profile picture).

## Impact

- **Server** (`src/Games/`):
  - `Game` domain model + `CreateGameCommand/Handler`: generate and carry the 8-digit `GameCode`; add creator as first lobby player.
  - `CreateGameRequest`, `GameDto`, `GameSummaryDto` (Abstractions): expose `GameCode`; request gains `DisplayName` / `ProfilePictureUrl`.
  - `Games.Data.Postgres`: new `GameCode` column with unique index + EF Core migration; collision-retry on insert.
  - Unit tests in `HexMaster.ThePrey.Games.Tests` for code generation, creator auto-join, and validation.
- **App** (`src/App/ThePrey.Application.App/`):
  - New `GameStartPage` and `GameLobbyPage` (XAML + code-behind), placeholder `GameProgressPage`, routes in `AppShell`, DI in `MauiProgram.cs`; Play button wired to the new flow.
  - `IGameService`/`GameService` extended with `CreateGameAsync`, `GetGameAsync` (lobby polling), `StartGameAsync`; new app models for game/lobby.
  - New localized strings in `AppResources.resx` / `AppResources.nl.resx` + `AppLocalizer` properties.
- **Dependencies**: relies on the `playfield-select-view` change for picking the playfield; joining a game *by code* from another device is intentionally out of scope (future join-game change) — the code is displayed so it can be shared once that flow exists.
