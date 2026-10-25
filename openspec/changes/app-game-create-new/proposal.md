# Proposal

## Why

In the Android app, Play now opens a placeholder, so a player cannot start a game. Creating a game is the first step of every session: choose where to play, set the timing, and open a lobby that friends can join. The playfield picker (`app-game-select-playfield`) is ready to be used. This change adds the screen that uses it and sends the create-game request.

The `game-create-page` capability already describes this screen, but for the removed Ionic client: the `/games/create` route, a modal picker, toasts, and other option values and defaults. It is updated to match the native app and the values that apply now.

## What Changes

- New **Create game screen** in the Android app, opened by the main menu's **Play now** button. The `play` placeholder is replaced.
- On open, the screen starts the **playfield selection screen**. If the player cancels before choosing a playfield, they go back to the main menu. After a playfield is chosen, the screen shows its name, and the player can tap it to choose a different one.
- Five settings, each a segmented choice with a default already selected:
  - **Game duration**: 30, 60 or 90 minutes. Default 30.
  - **Hunter delay**: 3, 5 or 10 minutes. Default 5.
  - **Final stage**: 3, 5 or 10 minutes. Default 10.
  - **Location ping interval**: 2, 3 or 5 minutes. Default 3.
  - **Final stage ping interval**: 1, 2 or 3 minutes. Default 1.
- The server requires the final-stage ping interval to be no longer than the normal one. Final-stage values that are longer than the chosen ping interval are disabled. If lowering the ping interval makes the current final-stage choice invalid, that choice moves down to the ping interval.
- **Create game** is enabled once a playfield has been chosen. Tapping it sends `POST /games` with the playfield, the player's callsign as display name, the durations in minutes and the ping intervals in seconds. While the request runs, the button shows a progress indicator and the form is locked.
- When the server answers **201 Created**, the app opens the **game lobby** for the new game. The create screen is removed from the back stack. Until the lobby screen exists, the lobby route shows a placeholder.
- If the request fails (network, validation problem, server error), the screen shows an error message with a retry option, and every choice is kept.
- New English and Dutch strings for all labels, units, the button and the errors.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `game-create-page`: updated for the native Android app. The option values and defaults change, and a rule is added that keeps the final-stage ping interval valid. The playfield is chosen first through the shared selection screen and is returned as a navigation result, replacing the modal. The request uses the current `POST /games` body. On success the app opens the lobby, replacing the `/games/:id/lobby` route. The toast is replaced by an inline error with retry. Play now opens the screen.

## Impact

- **Android app** (`app/src`):
  - New `ui/games/create/` (screen, view model, option model).
  - `createGame` added to `data/game/GamesApi.kt` and `GameRepository`, with request and response DTOs.
  - In `ui/navigation/ThePreyNavHost.kt`, the `play` placeholder is replaced by the create route, and a `lobby/{gameId}` placeholder route is added.
  - Strings in `values/strings.xml` and `values-nl/strings.xml`.
  - Unit tests.
- **Depends on** `app-game-select-playfield` (route `playfield_select` and the `PlayFieldSelectResult` contract). That change must be implemented first.
- **Backend:** none. The existing `POST /games` (`CreateGameRequest`, 201 with `GameDto`, 400 validation problem) is used as it is.
- **Docs:** the `POST /games` request and response example in `docs/api/endpoints.md` is out of date (`durationMinutes`, `code`). It is corrected to match `CreateGameRequest` and `GameDto`.
