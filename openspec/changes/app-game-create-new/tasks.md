# Tasks

## 1. Prerequisite

- [x] 1.1 Confirm `app-game-select-playfield` is implemented: `Routes.PLAYFIELD_SELECT`, `PlayFieldSelection` and `PlayFieldSelectResult.deliver/consume` exist in `app/src`, and `./gradlew testDebugUnitTest` passes from `app/src`. If they do not exist, stop and implement that change first.

## 2. Create-game API call

- [x] 2.1 Add the `@Serializable` `CreateGameRequest` (`playfieldId`, `displayName`, `gameDuration`, `hunterDelayTime`, `finalStageDuration`, `defaultLocationInterval`, `finalLocationInterval`, `enablePreyBoundaryPenalties`, `enableHunterBoundaryPenalty`) and `@POST("games") createGame(...)` returning `Response<GameDto>` to `data/game/`. Verify that a serialization unit test produces the camelCase JSON field names the server's `CreateGameRequest` expects.
- [x] 2.2 Add `NewGame` (values in minutes), `CreateGameResult` (`Created(gameId)` / `Failed`) and `GameRepository.createGame` to `ApiGameRepository`. Ping intervals are converted to seconds, penalties are `false`, and 201 maps to `Created`. Non-2xx, an empty body, `IOException` and parse errors map to `Failed`, and `CancellationException` is rethrown. Verify with unit tests against a fake `GamesApi` covering 201, 400, 500, an IO failure, and the minutes-to-seconds conversion (3 → 180, 1 → 60).
- [x] 2.3 Update the fake `GameRepository` used by existing tests (for example `MainMenuViewModelTest`) so the interface change compiles. Verify that `./gradlew testDebugUnitTest` passes.
- [x] 2.4 Correct the `POST /games` request and response examples in `docs/api/endpoints.md` to match `CreateGameRequest` and `GameDto`, noting minutes vs seconds. Verify the field names against `server/src/Games/HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/Requests.cs`.

## 3. Settings model and view model

- [x] 3.1 Implement `ui/games/create/GameSettings.kt` (the `GameSetting` enum with options and defaults, plus `GameSettings.with`/`isEnabled` and the final-ping clamp). Verify with unit tests for:
  - defaults (30/5/10/3/1);
  - ignoring out-of-range values;
  - final ping 3 disabled when ping is 2;
  - clamping 3 → 2 when ping is lowered to 2;
  - keeping 1 when ping is lowered to 2;
  - every option combination allowed by `isEnabled` satisfying the server's `GameConfiguration` rules (mirrored in the test).
- [x] 3.2 Implement `CreateGameViewModel` and `CreateGameUiState`:
  - selection, setting changes, `canCreate`, `create()` with a guard against a second submit, display name from `ProfileStore` (callsign, falling back to displayName), a one-shot `Created` event, and `failed` with retry;
  - `SavedStateHandle` persistence of the playfield, the settings and `pickerLaunched`.

  Verify with unit tests using a fake repository and profile store:
  - `canCreate` is false without a playfield;
  - the request values are correct;
  - `submitting` blocks a second submit and setting changes;
  - success emits `Created` once;
  - failure keeps the choices and retry resends the request;
  - state is restored from a pre-filled `SavedStateHandle`.

## 4. Screen, navigation and strings

- [x] 4.1 Add all `game_create_*` strings, the `game_create_minutes` plurals and `lobby_title` to `values/strings.xml` and `values-nl/strings.xml`, following the `the-prey-multilingual` skill. Verify that `./gradlew lintDebug` reports no missing translations.
- [ ] 4.2 Build `CreateGameScreen`: _(code and previews done; preview render check in Android Studio pending)_
  - top bar with back, the playfield card, five labelled `SingleChoiceSegmentedButtonRow`s with disabled options from `isEnabled`;
  - bottom bar with the inline error + Retry and a `PrimaryButton` with `busy`;
  - colours via `MaterialTheme.colorScheme` per the `the-prey-design-system` skill.

  Verify that Compose previews render for the empty, filled, submitting and failed states, in both light and dark themes.
- [x] 4.3 Wire picker behaviour in the screen: consume `PlayFieldSelectResult` from the entry's `SavedStateHandle`; auto-open the picker once when no playfield is set; call `onBack()` when resumed after a cancelled first pick; collect `events` into `onCreated`. Verify this through the view-model flag tests from 3.2 and the manual checks in 5.1.
- [x] 4.4 In `ThePreyNavHost.kt`, replace `Routes.PLAY` with `Routes.GAME_CREATE`. Point `onPlayNow` at it and add `Routes.LOBBY` (`lobby/{gameId}`) as a placeholder. On create, navigate to the lobby with `popUpTo(GAME_CREATE) { inclusive = true }`. Verify that `./gradlew assembleDebug` succeeds and no reference to `Routes.PLAY` remains.

## 5. Integration check

- [ ] 5.1 On a device or emulator against the backend, run `./gradlew lintDebug testDebugUnitTest assembleDebug` and check each of the following:
  - Play now opens the picker, and Cancel returns to the main menu;
  - selecting a playfield shows its name with the defaults selected;
  - with ping 2, final ping 3 is disabled and a selected 3 drops to 2;
  - rotation keeps every choice;
  - changing the playfield and cancelling keeps the old one;
  - airplane mode shows the error, and Retry works once back online;
  - Create game opens the lobby placeholder, and Back there returns to the main menu;
  - the game exists on the server with the chosen values (`GET /games/{id}/gamestate`);
  - the Dutch locale shows Dutch texts.
