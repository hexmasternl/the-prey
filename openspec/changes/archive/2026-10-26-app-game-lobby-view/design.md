# Design

## Context

- `app-game-create-new` opens `lobby/{gameId}` (`Routes.LOBBY`, a `PlaceholderScreen`) after `POST /games`, with the create screen removed from the back stack. It also adds the five-setting option model in `ui/games/create/`: durations in minutes, ping intervals shown in minutes and sent in seconds, and a rule that lowers the final-stage ping when the normal ping goes below it.
- The Android app has no realtime code. `docs/api/realtime.md` and the `client-game-state-service` contract (change `game-state-realtime-communication-protocol`) describe the single Game State Service. `app-game-hud-prey` and `app-game-hud-hunter` both take a `StateFlow` and wait for the service to supply it. `TrackingController.onGameEnded(gameId)` is the hook the service has to call.
- `GamesApi` currently has `getActiveGame`, `getGameState` and `recordLocation`. The local `GameDto` / `ParticipantDto` / `GameConfigurationDto` are partial models (the JSON parser ignores unknown keys).
- The server already has every lobby command: `PUT /games/{id}/config`, `POST /games/{id}/lobby/ready`, `POST /games/{id}/hunter` (Lobby/Ready: owner designates), `POST /games/{id}/start` (`{ hunterUserId }`, only from `Ready`), `POST /games/{id}/leave` and `GET /games/{id}/notifications/token`. Each command returns the caller's `GameDto`, except leave (204). The status `Ready` is computed on the server: at least 2 players, a designated hunter who is a participant, and every non-owner ready.
- The participant `userId` is the internal user id, the same as `UserDto.userId` in `ProfileStore`. It is not the Auth0 `sub`.
- OkHttp 4.12 is already on the classpath. Its `WebSocket` needs no new dependency.

## Goals / Non-Goals

**Goals:**
- One Android `GameStateService` that the lobby uses now and the HUDs can use later without changes to the service.
- A lobby whose every visible fact comes from the latest server state. The only local state is "a request is in flight".
- Pure, unit-testable reducers and UI mapping. The socket and the clock are behind seams.

**Non-Goals:**
- Removing players, editing the playfield, or the boundary-penalty toggles in the lobby. Penalty flags are sent back unchanged.
- Joining a game by code or by link on Android (a separate change). Sharing sends only the code.
- Push notifications (FCM) as a fallback channel.
- Wiring the HUD routes to the service. The lobby hands off to `Routes.game(gameId)` as it is today.

## Decisions

### 1. `GameStateService` is an app-scoped, ref-counted session per game

`AppContainer` owns one `GameStateService`. `session(gameId): StateFlow<GameSession>` returns a shared flow built with `stateIn(appScope, SharingStarted.WhileSubscribed(5_000))`. While it has a subscriber, it runs the loop: snapshot, connect, apply deltas, and resync every 3 minutes. Five seconds after the last subscriber leaves, it closes the socket. Only one game is live at a time. Asking for another game id tears down the previous session.

`GameSession` is an immutable value: `Loading`, `Live(snapshot: FullGameStateDto, lastSeq: Long?, connection: Connected|Reconnecting)`, `Failed(retryable)`, `Gone(reason: Cancelled|Removed|Completed)`.

**Why:** the realtime skill demands one owner of the socket and one source of truth. The 5 s grace keeps the socket open across the lobby → game route hand-off and across configuration changes, so neither causes a reconnect. **Alternative:** a socket per screen (the old Ionic shape). Rejected: it duplicates state and is what the protocol change removed.

### 2. A pure reducer applies envelopes

`GameStateReducer.apply(state, envelope): Outcome` returns `Applied(newState)` or `Resync`. Rules from `docs/api/realtime.md`:
- `v != 1`, `seq <= lastSeq` or `seq > lastSeq + 1` → `Resync`. The first message after a snapshot sets the baseline, because the snapshot has no `seq`.
- `participant-joined` / `participant-changed` replace the participant by `userId` wholesale (or append it). `participant-removed` drops it. If it is the current user, the result is `Gone(Removed)`.
- `configuration-changed` merges the game-level slice and keeps the participant list.
- `game-ended` → `Gone(Cancelled)` when `outcome == "Cancelled"`, otherwise the state is marked `Completed`, which the lobby treats as gone. It also calls `TrackingController.onGameEnded(gameId)`.
- `resync-requested` → `Resync`.
- Unknown `type` with a supported `v` → ignored. That keeps clients forward compatible.

Gameplay messages (`locations-updated`, `prey-updated`) are reduced too, so the HUDs can use the same reducer later, but the lobby does not read them.

**Why:** all protocol logic in one pure function is tested exhaustively without a socket.

### 3. Command responses replace state, and commands go through the service

The lobby view model calls `GameRepository` commands. On a 200 it calls `gameStateService.replaceGame(gameId, gameDto)`. That swaps the `game` slice of the snapshot and keeps `lastSeq`. The broadcast the command produces arrives later and carries the same facts, so applying it changes nothing. After any failed command, the service is asked to `resync()`.

**Why:** "a client's own action is reflected from the command's HTTP response" (realtime.md). Resync after a failure covers the common cause, a state that changed underneath, for example a 400 from `start` because someone was just reset. **Alternative:** optimistic local edits. Rejected, because the server leads.

### 4. One command at a time, controls locked while it runs

`LobbyViewModel` holds `pending: LobbyCommand?`. While it is set, the settings, the row taps and the action button are disabled, and the busy control shows progress. Every setting tap sends the full configuration built from the latest server configuration plus that one change, after the create screen's final-ping rule. If the server value is not one of the options (for example a game made by another client), no segment is selected and the value is still shown.

**Why:** "apply on each change" (user decision) with rapid taps would otherwise race. Each PUT resets readiness, so sending them one at a time with the server's answer in between also keeps the visible state honest.

### 5. All lobby display rules live in a pure mapper

`LobbyUiState.from(snapshot, myUserId, pending)` decides `isOwner` (`ownerUserId == myUserId`, never the per-caller `isOwnerPlayer`, which broadcasts do not refresh), the rows (role = `hunterUserId == userId`; ready = `isReady || userId == ownerUserId`; `isMe`), the Set ready state, and the Start state with its hint. The hint is checked in this order: fewer than 2 players → "waiting for players", no hunter → "tap a player to choose the hunter", someone waiting → "players are getting ready". Start is enabled only on `status == "Ready"`. The hint never overrides the server.

`myUserId` comes from `ProfileStore.read()?.userId`. It is cached at sign-in, so no network call is needed.

**Why:** keeping the rules in one tested function means the composable only renders. Enabling Start on server status alone stops a local rule from disagreeing with the server's preconditions.

### 6. Web PubSub client

`WebPubSubClient` gets a URL from `GET /games/{id}/notifications/token` and opens `OkHttpClient.newWebSocket` with the header `Sec-WebSocket-Protocol: json.webpubsub.azure.v1`. On open it sends `{"type":"joinGroup","group":gameId,"ackId":1}` and treats `ack` success or the error `Duplicate` as joined. Only `type == "message" && from == "group"` frames are parsed as envelopes. On an unexpected close or failure it reconnects with backoff 1 s, 2 s, 4 s … capped at 30 s, using a fresh token each time, and runs a snapshot once joined. A `403` from the token or snapshot request becomes `Gone(Removed)` and stops reconnecting. The socket uses a dedicated OkHttp client derived from the app one (`newBuilder()`, no auth interceptor needed for the access URL, ping interval 0). The token request goes through the authenticated Retrofit API.

The socket sits behind an interface (`RealtimeConnection`: `connect(): Flow<RealtimeEvent>`), so the service is tested with a fake connection and `TestScope` virtual time for the backoff and the 3-minute timer.

### 7. Server: broadcast the ready reset

`UpdateGameSettingsCommandHandler` collects the non-owner participants with `IsReady == true` before `game.UpdateSettings(config)`. After `UpdateAsync` and the `configuration-changed` publish, it publishes `participant-changed` (`game.ToParticipantDto(userId)`) for each of them. There is no domain change.

**Why:** `configuration-changed` deliberately carries no participants, so without this, other clients show stale ready tags until the 3-minute resync. **Alternative:** clients reset ready flags locally when they receive a configuration change. Rejected, because it moves a server rule into the client. **Alternative:** publish `resync-requested`. Rejected: it makes every client fetch a full snapshot for a change that the per-participant deltas describe exactly.

### 8. Navigation, back and share

- `Routes.LOBBY` renders `LobbyScreen(gameId, onStarted, onExit)`. `onStarted` navigates to `Routes.game(gameId)` with `popUpTo(Routes.LOBBY) { inclusive = true }`. A `LaunchedEffect` keyed on the status fires it once for `Started` or `InProgress`.
- A `BackHandler` opens a confirmation dialog. The owner's text says the game is cancelled for everyone. On confirm: `POST /leave` (204 or 404 both count as left), then `gameStateService.close(gameId)`, then `onExit()` → `popBackStack(Routes.MAIN_MENU, inclusive = false)`. A 409 means the game already started, so the app resyncs, which then hands off.
- `Gone` shows a snackbar-style message on the main menu through a one-shot result. It is `lobby_cancelled` or `lobby_removed`.
- Share uses `Intent.ACTION_SEND` (`text/plain`) wrapped in `Intent.createChooser`. The text is `getString(R.string.lobby_share_message, code)`, so it is localized and ends with the code.

### 9. Visuals

Tags use `ThePreyTheme.colors`: hunter `Hunter`, prey `Prey`, waiting `Caution`, ready `Prey`, you `TextGhost` with a hairline `Line` border. The code is set in Special Elite at display size. The Start and Set ready buttons are the existing `PrimaryButton` (with `busy`). The settings reuse the create screen's segmented control composable, and the read-only rows reuse its labels and units. Follow the `the-prey-design-system` skill for spacing and the tactical tone.

## Risks / Trade-offs

- [A delta for state that is older than a just-applied command response arrives after it] → Deltas are complete for their slice and in `seq` order, and the next delta or resync corrects it. In the lobby the window is a fraction of a second, and the 3-minute resync bounds it in any case.
- [Rapid setting changes each reset readiness, which annoys the other players] → One command at a time, and the user accepted the reset (decision: apply on each change).
- [`isOwnerPlayer` / `isReadyToStart` in a snapshot or command response are per-caller and go stale after broadcasts] → The UI never reads them. Ownership and Start come from `ownerUserId` and `status`.
- [The socket is dropped while the app is in the background] → `WhileSubscribed` stops the session after 5 s in the background (lifecycle-aware collection). On return the app reconnects and runs a snapshot, which is the documented heal.
- [`app-game-create-new` is not implemented yet] → This change depends on its route and option model. If it lands first in a different shape, the lobby's tasks group 4 adapts to it. Implement `app-game-create-new` first.
- [Process death while the lobby is open] → The route argument restores `gameId`, and the service runs a new snapshot. The server still counts the player as a member, which is what they expect when they come back.

## Migration Plan

Deploy the server change first. It only adds broadcasts, and old clients ignore an extra `participant-changed`. Then ship the app. Rollback: the app falls back to the placeholder lobby by reverting the route, and the server broadcast is harmless on its own.
