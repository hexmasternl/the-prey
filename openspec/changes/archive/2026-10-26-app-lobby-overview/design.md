# Design

## Context

`LobbyScreen.kt` renders a `Column` with `verticalScroll` inside a `Scaffold` whose `bottomBar` is the Set ready / Start `ActionBar`. The column holds `CodeCard` (label, code in `displayMedium`, a 48 dp `SecondaryButton`), then a settings `Section` (owner: `GameSettingsForm`, five segmented rows with 24 dp gaps; others: `GameSettingsSummary`), then the players `Section`, with 32 dp between blocks. On a typical 640–700 dp tall phone, the top bar, the code card and the owner's settings form fill the viewport before the first player row. All lobby state comes from `LobbyUiState`, derived from the Game State Service snapshot; this change touches only how it is laid out. See proposal.md for the why.

## Goals / Non-Goals

**Goals:**
- First player rows visible without scrolling in portrait, for owner and non-owner.
- No change to commands, locking, error handling or realtime behaviour.
- Reuse `GameSettingsForm` and `GameSettingsSummary` unchanged.

**Non-Goals:**
- A landscape-specific two-column lobby (could follow the `app-main-menu-landscape` pattern later; the single scrolling list already works in landscape).
- Removing players, or any new lobby action.
- Changing the create-game screen.

## Decisions

### 1. Order: code → players → settings summary
Players are the live, actionable part of the lobby (who joined, who is ready, the owner's hunter tap); settings are set once and rarely revisited. Moving players up and collapsing settings fixes the overview for both roles.
*Alternatives:* tabs (Players / Settings) hide the settings entirely and add a tap to see what game you are in; a collapsible inline section still grows the page by the full form when opened and pushes the players off-screen again. A summary card plus bottom sheet keeps every value visible and the editor one tap away.

### 2. Settings summary card opens a `ModalBottomSheet`
The card is one `surface` block (same border and shape as the player list) with a header row (`lobby_settings` label, plus a trailing chevron or "Edit" affordance for the owner) and the five values as small chips in a `FlowRow` (e.g. "60 min game", "5 min head start", "10 min final stage", "Ping 3 min", "Final ping 1 min"). The whole card is one `clickable` with `Role.Button`. The sheet shows `GameSettingsForm(enabled = lobby.canEdit)` for the owner and `GameSettingsSummary` for others, under a sheet title.
- Sheet open state is `rememberSaveable` in `LobbyContent`; its content reads `state.lobby` so realtime updates and the response of a `PUT` appear live in the open sheet.
- Each change still dispatches `onSettingChange` immediately (no Save button), matching today's behaviour and the spec scenario "Owner saves updated settings". The sheet does not close on change, so the owner can adjust several settings in a row.
- When the screen leaves composition (Started / Exit events navigate away), the sheet goes with it; no extra handling needed.
- Error snackbars show in the `Scaffold`'s `SnackbarHost`, which sits under a modal sheet. Acceptable: the sheet's values snap back to the server's values, which is the visible signal, and the snackbar is visible when the sheet closes. If that proves confusing, a `SnackbarHost` inside the sheet can be added later without spec changes.
*Alternative:* a separate settings route. Rejected: more navigation, and the owner loses sight of the lobby context.

### 3. Compact code header
`CodeCard` becomes a single row: the code (still `displayMedium` with wide letter spacing, primary colour) followed by an `IconButton` with a new `ic_share.xml` (Material "share" path, tinted primary), and the `lobby_code_label` above it. The row is centred horizontally as the card is today. `contentDescription` uses a new `lobby_share_description` ("Share game code"). Padding drops from 20 dp to 16 dp vertically. Saves the 48 dp button plus 16 dp spacing.

### 4. Ready count derived in `LobbyUiState`
Add `readyCount: Int` (rows with `isReady`, the owner counted as ready because rows already encode that) next to `rows`. The title renders `lobby_players_count` as e.g. "Players · 3 · 2 ready" via a plurals resource for the player count. Deriving it in `LobbyUiState.from` keeps it unit-testable in `LobbyUiStateTest` rather than in the composable.

### 5. One scrolling container
Keep the `Column` + `verticalScroll` (the player list is bounded at 16 players, so `LazyColumn` buys nothing and would complicate the bordered list block), reduce the block spacing from 32 dp to 24 dp, and keep the `ActionBar` as the `Scaffold` `bottomBar` so `innerPadding` already prevents it from covering the last block. The owner hint "Tap a player to make them the hunter." stays under the players title.

### 6. Strings
New keys, each in `values/strings.xml` and `values-nl/strings.xml` (the-prey-multilingual skill): `lobby_share_description`, `lobby_players_ready` (plurals for the count line), `lobby_settings_edit`, `lobby_settings_sheet_title`, and summary chip formats `lobby_summary_duration`, `lobby_summary_hunter_delay`, `lobby_summary_final_stage`, `lobby_summary_ping`, `lobby_summary_final_ping` (each taking minutes). `lobby_share` remains used for the share-sheet flow only if still referenced; otherwise it is removed in both languages.

## Risks / Trade-offs

- [Settings less discoverable for the owner] → The card shows every value and an explicit "Edit" affordance; the start hint never asks for a settings change, so nothing blocks on it.
- [Snackbar hidden behind the sheet on a failed setting change] → Values revert visibly; see decision 2 for the follow-up option.
- [Chip text overflow in Dutch] → `FlowRow` wraps; verified with a Dutch preview.
- [Merge order with `app-game-lobby-view`] → That change is still open and its `game-lobby-ui` delta defines the requirement texts this change modifies. Archive it before archiving this one.

## Migration Plan

Client-only UI change shipped with the next app release. Rollback is reverting the commit; no data or API changes.
