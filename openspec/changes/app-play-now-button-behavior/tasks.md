# Tasks

## 1. Games API client

- [x] 1.1 Add `data/game/GamesApi.kt` (`GET games/active` returning `Response<ActiveGameDto>`), `ActiveGameDto(gameId)` and `GameRepository` with an `ActiveGameResult` (`Active(gameId)`, `None`, `Failed`) mapping 200/404/other/`IOException`, rethrowing cancellation; verify with a `GameRepositoryTest` using a fake `GamesApi` that covers 200, 404, 500, `IOException` and cancellation
- [x] 1.2 Wire `GamesApi` and `GameRepository` in `AppContainer` from the shared authenticated `retrofit` and pass the repository into `MainMenuViewModel` from `ThePreyNavHost`; verify `./gradlew assembleDebug` succeeds

## 2. Play action state

- [x] 2.1 Add `LocationAccess`, `ActiveGameCheck`, `PlayActionState`, `PlayActionError` and the pure `playAction(access, check)` function in `ui/mainmenu/PlayAction.kt` following the table in design Decision 1; verify with `PlayActionTest` covering every cell of the table, including permission errors winning over `CheckFailed`
- [x] 2.2 Extend `MainMenuViewModel`: replace `onLocationPermissionResult(Boolean)` with `onLocationAccess(LocationAccess)` (readout runs for `Granted` and `ForegroundOnly`), add `refreshActiveGame()` (sets `Loading`, cancels an in-flight request) and a `playAction` field in `MainMenuUiState`; update `MainMenuViewModelTest` and add tests for: Loading until the repository answers, None → green Play Now, Active → red Resume Game with the game id, Failed → CheckFailed, a second refresh cancelling the first, and readout gating; verify `./gradlew testDebugUnitTest` passes

## 3. UI

- [x] 3.1 Add `PrimaryButtonStyle` (`Signal`, `Hunter`) to `PrimaryButton` in `ui/components/Buttons.kt` using theme colors only (`the-prey-design-system` skill) and add a `@Preview` showing both styles enabled and disabled; verify the previews render and existing callers compile unchanged
- [x] 3.2 Add the English strings (`main_menu_resume_game`, location-missing and background-location-missing errors, check-failed error, `main_menu_open_settings`, `main_menu_retry`, disabled-state descriptions) to `values/strings.xml` and Dutch to `values-nl/strings.xml` following the `the-prey-multilingual` skill; verify `./gradlew lintDebug` reports no missing translations
- [x] 3.3 Render the play button from `uiState.playAction` in `MenuActions` and add the `PlayActionNotice` panel (message + Open settings or Retry, polite live region, `stateDescription` on the disabled button); add `@Preview`s for loading, Play Now, Resume Game, background-location error and check-failed; verify the previews render
- [x] 3.4 In `MainMenuScreen`, observe `ON_RESUME` of the lifecycle owner to read fine and (API 29+) background location and call `onLocationAccess` plus `refreshActiveGame()`; wire Open settings to `Settings.ACTION_APPLICATION_DETAILS_SETTINGS` and Retry to `refreshActiveGame()`; remove the old fine-or-coarse `onLocationPermissionResult` call but keep the existing one-time foreground prompt until the consent change replaces it; verify `./gradlew assembleDebug` succeeds

## 4. Navigation and manifest

- [ ] 4.1 Add `Routes.GAME = "game/{gameId}"` with a placeholder screen titled "Resume Game" and pass `onResumeGame = { navController.navigate("game/$it") }` to `MainMenuScreen`; verify the existing navigation tests pass and Back from the placeholder returns to the menu _(code done; device check pending)_
- [x] 4.2 Add `ACCESS_BACKGROUND_LOCATION` to `AndroidManifest.xml`; verify it appears in the merged debug manifest
- [x] 4.3 Update `openspec/changes/app-location-service/proposal.md` (and its design if it repeats it) to say background location is declared by this change instead of "No `ACCESS_BACKGROUND_LOCATION`"; verify `openspec validate app-location-service` still passes

## 5. End-to-end verification

- [ ] 5.1 On an Android 13+ emulator signed in against the dev backend, walk every scenario in `specs/main-menu-play-action/spec.md`: grey while checking, location denied, "While using the app" only, approximate only, Open settings → grant "Allow all the time" → return, revoke → return, no active game → green Play Now, active game (start one via the API) → red Resume Game opening the game route, airplane mode → check error → Retry, return from Playfields re-checks; repeat the error and label checks with the device in Dutch _(code done; device check pending)_
- [ ] 5.2 On an Android 9 (API 28) emulator verify that a fine-location grant alone enables the button (background implied) _(code done; device check pending)_
