# Proposal

## Why

After creating a game, the Android app opens a `lobby/{gameId}` placeholder (`app-game-create-new`). Players cannot see who has joined, the owner cannot adjust the game, nobody can ready up, and the game cannot be started. The lobby connects game creation to gameplay. It is also the first Android screen that needs live server updates, so it introduces the app's realtime connection.

The `game-lobby-ui` and `game-share-invite` capabilities already describe a lobby, but for the removed Ionic client. They use the old unversioned event names, the Web Share API, an 8-digit code (it is 4 digits now), the wrong settings route and swipe-to-delete. This change updates them to match the native app.

## What Changes

- New **Game lobby screen** in the Android app, replacing the `lobby/{gameId}` placeholder. From top to bottom:
  - **Game code** with a **Share** button that opens the Android share sheet with an invitation and the code.
  - **Game settings**: game duration, hunter delay, final stage, location ping interval and final-stage ping interval. For the owner these are the same segmented choices as on the create screen. Each change is sent immediately with `PUT /games/{id}/config`. Everyone else sees them read-only.
  - **Players**: one row per participant with the callsign, a role tag (**hunter** in red, **prey** in green), a state tag (**waiting** in orange, **ready** in green), and a dim grey **you** tag on the current user's row. The owner taps a row to make that player the hunter (`POST /games/{id}/hunter`).
  - **Action button**: players who are not the owner see **Set ready** (`POST /games/{id}/lobby/ready`), which is disabled once they are ready. The owner sees **Start game**, which is disabled until the server reports the game `Ready` (a hunter is chosen, at least two players, everyone else ready). Tapping it calls `POST /games/{id}/start`.
- When the game reaches `Started`, every participant is taken to the game route.
- **Back** asks for confirmation and then calls `POST /games/{id}/leave`. For the owner this cancels the game. Other players whose game is cancelled, or who are no longer members, are returned to the main menu with a message.
- New **Android Game State Service**: the single source of truth from `docs/api/realtime.md`. It owns one Web PubSub socket per game (`GET /games/{id}/notifications/token`, `json.webpubsub.azure.v1`, `joinGroup`), loads `GET /games/{id}/gamestate`, applies the versioned deltas, and pulls a full snapshot every 3 minutes, on every (re)connect, on a `seq` gap, on `resync-requested` and on an unknown `v`. A command's HTTP response replaces local state. The server always wins.
- **Server fix**: a settings change resets non-owner ready flags, but it only broadcasts `configuration-changed`, which carries no participants. Other clients would keep showing those players as ready until the next resync. After a settings update the server now also broadcasts `participant-changed` for every participant whose ready flag was reset.
- New English and Dutch strings.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `game-lobby-ui`: rewritten for the native Android lobby. The code is 4 digits. The settings set is the five timing values, saved with `PUT /games/{id}/config` on each change. New tags for role, state and "you". The owner taps a row to choose the hunter. Set ready and Start game are added, and Start game is enabled from the server's `Ready` status. Updates use the versioned realtime protocol through the Game State Service. Hand-off happens on `Started`. A confirmed leave is added. Swipe-to-delete is removed.
- `game-share-invite`: the Web Share API is replaced by the Android share sheet. The message carries an invitation and the 4-digit game code.
- `lobby-ready-status`: the settings route is corrected to `PUT /games/{id}/config`, and the reset of ready flags is broadcast to the group as `participant-changed`.
- `lobby-player-management`: the settings route is corrected to `PUT /games/{id}/config`.

## Impact

- **Android app** (`app/src`):
  - New `data/game/realtime/` (Web PubSub socket client, envelope and delta models) and `data/game/GameStateService`, wired in `AppContainer`. It uses the OkHttp WebSocket that is already a dependency.
  - `GamesApi` / `GameRepository` get `updateConfig`, `setReady`, `setHunter`, `start`, `leave` and `getNotificationsToken`. The `GameDto`, `ParticipantDto` and `GameConfigurationDto` models are extended with the fields the lobby needs.
  - New `ui/games/lobby/` (screen, view model, pure lobby-state mapping). It reuses the settings option model from `ui/games/create/`.
  - In `ThePreyNavHost.kt`, the lobby placeholder is replaced, and the route moves on to `Routes.game(gameId)` on `Started`.
  - Strings in `values/strings.xml` and `values-nl/strings.xml`, plus unit tests.
- **Server** (Games module): `UpdateGameSettingsCommandHandler` publishes `participant-changed` for each reset participant. Unit test added. No API shape change.
- **Docs**: `docs/api/endpoints.md` lists `PUT /games/{id}/config`, `POST /games/{id}/lobby/ready` and `POST /games/{id}/hunter`. `docs/api/realtime.md` notes the ready-reset broadcast.
- **Depends on** `app-game-create-new` (lobby route and settings option model). The `client-game-state-service` contract from `game-state-realtime-communication-protocol` is implemented here for Android.
