# Proposal

## Why

The main menu's Play now button is always enabled, even when the app cannot track the player or when the player is already in a running game. A game needs precise location all the time, also in the background, so starting one without those permissions fails mid-game. A player who is already in a game should go back to it instead of starting a new one.

## What Changes

- The Play now button gets four states, decided from the location permissions and an active-game check:
  - **Disabled (grey)**: while the active-game check runs, and whenever location or background location is not granted.
  - **Play Now (green)**: no active game, both permissions granted.
  - **Resume Game (red)**: the server reports an active game for this player (`Started` or `InProgress`), both permissions granted. Tapping it opens that game.
- Every time the player lands on the main menu, the app asks the server for the player's active game (`GET /games/active`). The button stays disabled until that request completes.
- If the active-game check fails (no network, server error), the button stays disabled and the menu shows a short error with a Retry action.
- If location or background location ("Allow all the time") is not granted, the menu shows an error message under the button that explains which permission is missing and has an **Open settings** button that opens the app's system settings page. Permissions are checked again when the player returns to the app, so the button turns active as soon as both are granted.
- The app declares and checks `ACCESS_BACKGROUND_LOCATION`. Only precise (fine) location counts as granted; approximate only is treated as missing.
- New English and Dutch strings for "Resume Game", the permission errors, the check error and the buttons.

## Capabilities

### New Capabilities
- `main-menu-play-action`: the state of the main menu's primary play button, how it depends on the location and background location permissions and on the player's active game, what it shows in each state, where it leads, and the error messages with their recovery actions.

### Modified Capabilities
<!-- None in openspec/specs. The in-flight app-main-menu change defines a generic "Play now" primary action and its destination; this capability adds the enablement rules and the Resume Game variant on top of it. The in-flight app-location-foreground-service-warning change owns the disclosure and the foreground permission prompt; this change adds the background location check that its design (Decision 2) leaves open. -->

## Impact

- **Android app (`app/src`)**: `ui/mainmenu/` (view model state for the play action, button variants, error panel), a new games API client under `data/game/` with `GET /games/active` (404 means no active game), `AppContainer` wiring, a red (Hunter) variant in `ui/components/Buttons.kt`, a new `resume_game` route (placeholder until the game screen exists), and new strings in `values/strings.xml` and `values-nl/strings.xml`.
- **Manifest**: `ACCESS_BACKGROUND_LOCATION` added. This changes the decision in `app-location-service` ("No `ACCESS_BACKGROUND_LOCATION`"); that change should be updated to match.
- **Play Console**: declaring background location needs a background location declaration and a video. The in-app disclosure from `app-location-foreground-service-warning` already covers background collection.
- **Backend**: none. `GET /games/active` already exists and returns `GameStatusDto` or 404.
