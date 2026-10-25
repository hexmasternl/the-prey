# Tasks

## 1. State and strings

- [ ] 1.1 Add `readyCount` to `LobbyUiState` (rows with `isReady`, owner included) and set it in `LobbyUiState.from`; add `LobbyUiStateTest` cases for 1 player (owner only → 1/1), 3 players with 2 ready, and a ready flag reset by a settings change. Verify with `./gradlew testDebugUnitTest` from `app/src`.
- [ ] 1.2 Add the new strings from design decision 6 (`lobby_share_description`, `lobby_players_ready` plurals, `lobby_settings_edit`, `lobby_settings_sheet_title`, the five `lobby_summary_*` formats) to `res/values/strings.xml` and `res/values-nl/strings.xml`, following the `the-prey-multilingual` skill. Verify every new key exists in both files and `./gradlew lintDebug` reports no missing translations.
- [ ] 1.3 Add `res/drawable/ic_share.xml` (24 dp Material share path) and verify it renders in a Compose preview.

## 2. Lobby layout

- [ ] 2.1 Rework `CodeCard` in `LobbyScreen.kt` into the compact header: label, then the code with an `IconButton` (`ic_share`, `lobby_share_description`, 48 dp target) directly after it on one line; remove the full-width Share button. Verify with the Owner and Player previews that the header is one code line high and the share flow still opens the chooser.
- [ ] 2.2 Reorder the content to code header → players → settings card, reduce block spacing to 24 dp, and give the players section title the player and ready counts (`lobby_players_ready`). Keep the owner hint under the title. Verify in a default-height phone preview (no `heightDp` override) that the first player rows are visible for owner and non-owner.
- [ ] 2.3 Add the settings summary card: bordered surface block with the `lobby_settings` title, an owner-only `lobby_settings_edit` affordance, and the five values as chips in a `FlowRow` using the `lobby_summary_*` strings; the whole card is clickable with `Role.Button`. Use `ThePreyTheme`/`MaterialTheme` colors only (the-prey-design-system skill). Verify with English and Dutch previews that the chips wrap without clipping.
- [ ] 2.4 Add the settings `ModalBottomSheet` opened by the card (`rememberSaveable` open state): `GameSettingsForm(enabled = lobby.canEdit)` for the owner, `GameSettingsSummary` for others, under `lobby_settings_sheet_title`; content reads the current `state.lobby` so live updates show in the open sheet, and changes call `onSettingChange` immediately without closing the sheet. Verify with a preview of the sheet content for owner and non-owner, and `./gradlew lintDebug`.
- [ ] 2.5 Remove `lobby_share` from both string files if nothing references it any more, and update the `LobbyScreen` KDoc to the new layout. Verify with `./gradlew lintDebug` (no unused-resource or missing-translation warnings for lobby strings).

## 3. Integration check

- [ ] 3.1 With the Aspire AppHost running and two devices or emulators, create a game on A and join it on B. Verify that both lobbies show the players directly below the code without scrolling; that the counts update when B joins and sets ready; that A changes a setting in the sheet and B's summary card (and B's open sheet) update and B's ready resets; that the share icon opens the share sheet; and that starting the game while B's sheet is open takes B to the game route.
