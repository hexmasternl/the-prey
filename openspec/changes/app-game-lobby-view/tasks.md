# Tasks

## 1. Server: broadcast the ready reset

- [x] 1.1 In `UpdateGameSettingsCommandHandler`, capture the non-owner participants with `IsReady == true` before `game.UpdateSettings`. After the `configuration-changed` publish, publish `participant-changed` (`game.ToParticipantDto(userId)`) for each of them. Keep the existing OTel activity. Verify `dotnet build server/src/the-prey.slnx` succeeds.
- [x] 1.2 Add tests in `HexMaster.ThePrey.Games.Tests/UpdateGameSettings/`: two ready players and one waiting produce one `configuration-changed` and exactly two `participant-changed` messages with `isReady: false`, and no ready players produce no `participant-changed`. Verify `dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/` passes.
- [x] 1.3 In `docs/api/realtime.md`, note under `participant-changed` that a settings change broadcasts one per reset player. In `docs/api/endpoints.md`, list `PUT /games/{id}/config`, `POST /games/{id}/lobby/ready` and `POST /games/{id}/hunter` (with request bodies), and fix the 4-digit code. Verify by reading that the routes match `GameEndpoints.cs`.

## 2. Android data layer: lobby commands and models

- [x] 2.1 Extend `data/game/GameStateDto.kt`: `GameDto` gets `gameCode`, `ownerUserId`, `outcome`. `GameConfigurationDto` gets all seven fields. `ParticipantDto` gets `displayName`, `isReady`, `hasActivePenalty`. Add `GameStatuses.LOBBY`/`READY` and request DTOs (`UpdateGameConfigRequest`, `SetHunterRequest(newHunterUserId)`, `StartGameRequest(hunterUserId)`, `NotificationConnectionDto(url)`). Verify `./gradlew testDebugUnitTest` still passes, including the existing tracking tests that parse snapshots.
- [x] 2.2 Add `updateConfig`, `setReady`, `setHunter`, `start`, `leave` and `getNotificationsToken` to `GamesApi`. Add them to `GameRepository` / `ApiGameRepository` as a `CommandResult` (`Ok(GameDto)`, `Left`, `Forbidden`, `NotFound`, `Conflict`, `Rejected`, `NetworkError`). Verify with unit tests against a fake `GamesApi` that cover each status code mapping.

## 3. Android Game State Service

- [x] 3.1 Add `data/game/realtime/` models: the `RealtimeEnvelope` (`v`, `type`, `gameId`, `seq`, `data` as `JsonElement`), the Web PubSub frame parsing (`message`/`group`, `ack`) and the typed payloads (participant, participant-removed, configuration-changed, game-ended). Verify with unit tests that decode the JSON samples from `docs/api/realtime.md`.
- [x] 3.2 Implement the pure `GameStateReducer` (design decision 2) and verify unit tests cover: participant join, replace and remove; removal of the current user → `Gone(Removed)`; configuration merge that keeps participants; `game-ended` Cancelled vs completed; `resync-requested`; unknown `v`; `seq` gap, regression and baseline after a snapshot; unknown type ignored.
- [x] 3.3 Implement `WebPubSubClient` behind a `RealtimeConnection` interface: token fetch, OkHttp WebSocket with the `json.webpubsub.azure.v1` subprotocol, `joinGroup` with ack/`Duplicate` handling, and group-message filtering. Verify with a unit test that a fake `WebSocket` listener emits joined and envelope events and sends the expected `joinGroup` frame.
- [x] 3.4 Implement `GameStateService` (design decision 1): `session(gameId)` with `WhileSubscribed(5_000)`, snapshot on start and after each join, 3-minute resync, backoff reconnect from 1 s to 30 s with a fresh token, `403` → `Gone(Removed)` with no more reconnects, `replaceGame`, `resync`, `close`, and the `TrackingController.onGameEnded` call on game end. Wire it in `AppContainer`. Verify with `TestScope` virtual-time tests and a fake connection: periodic resync at 3 min, resync on reconnect, gap → snapshot, backoff schedule, socket closed 5 s after the last subscriber, and `onGameEnded` called once.

## 4. Lobby state mapping and view model

- [ ] 4.1 Extract or reuse the create screen's settings option model (`ui/games/create/`) so the lobby can share the option sets, the seconds ↔ minutes conversion and the final-ping rule. Verify the existing create-screen tests still pass.
- [ ] 4.2 Implement the pure `LobbyUiState.from(snapshot, myUserId, pending)` (design decision 5): owner from `ownerUserId`, rows with role, ready (owner always ready) and you tags, Set ready enabled or disabled, Start enabled only on `Ready`, and the hint priority (players → hunter → getting ready). Verify with unit tests for each spec scenario of the participant list, Set ready and Start.
- [ ] 4.3 Implement `LobbyViewModel`: collect `gameStateService.session(gameId)`, read `myUserId` from `ProfileStore`, and add the commands `changeSetting`, `designateHunter` (no-op on the current hunter or for non-owners), `setReady`, `start` and `leave`. Allow one command at a time, call `replaceGame` on success and show an error plus `resync()` on failure. Emit one-shot events for `Started` → navigate, `Gone` → exit with a reason, and share. Verify with view-model unit tests using fake repository and service: the full PUT body for a single change, the final-ping lowering, locking while pending, a resync after a rejected start, a leave confirmed on 204 and on 404, and staying in the lobby on a network error.

## 5. Lobby screen, navigation and strings

- [ ] 5.1 Build `ui/games/lobby/LobbyScreen.kt`: loading and error-with-retry states, the code with a Share button (`ACTION_SEND` chooser), the owner's segmented settings or the read-only settings, the player rows with tags in `ThePreyTheme.colors` (owner taps a row to choose the hunter), the Set ready or Start button with its hint, a reconnecting indicator and the Back confirmation dialog (separate owner text). Follow the `the-prey-design-system` skill. Verify with Compose previews for the owner and non-owner states and `./gradlew lintDebug`.
- [ ] 5.2 In `ThePreyNavHost.kt`, replace the `Routes.LOBBY` placeholder with `LobbyScreen`. On `Started`/`InProgress`, navigate to `Routes.game(gameId)` with `popUpTo(Routes.LOBBY) { inclusive = true }`. On exit, pop to `Routes.MAIN_MENU` and show the cancelled or removed message there. Verify `./gradlew assembleDebug` succeeds and, on a device, that Back from the game route does not return to the lobby.
- [ ] 5.3 Add every lobby string to `values/strings.xml` and `values-nl/strings.xml` (labels, units, tags, buttons, hints, dialogs, errors, gone messages, and `lobby_share_message` ending with `%1$s`), following the `the-prey-multilingual` skill. Verify `./gradlew lintDebug` reports no missing translations.

## 6. End-to-end check

- [ ] 6.1 With the Aspire AppHost running and two devices or emulators signed in as different users, create a game on A and join it on B. Verify that B appears on A live; that A's setting change shows on B and resets B's ready tag on both devices; that A's hunter tap shows on both; that Start enables only after B is ready and a hunter is chosen; that Start takes both devices to the game route; and that, in a second game, A leaving returns B to the main menu with the cancelled message.
