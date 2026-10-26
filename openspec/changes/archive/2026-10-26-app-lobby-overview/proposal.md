# Proposal

## Why

The lobby stacks three blocks in one column: a tall code card (label, code, full-width Share button), the five game settings, and the player list. For the owner the settings are five segmented controls, so on a phone the player list, which is what changes live and what the owner taps to choose the hunter, starts below the fold. Players have to scroll to see who has joined and who is ready, and many don't realise the page scrolls at all.

## What Changes

- **Compact code header.** The game code stays at the top as the most important element, with the Share action as an icon button on the same line, right after the code. The full-width Share button goes away, saving roughly 70 dp.
- **Players come first.** The player list moves directly below the code header. Its section title shows the live count and how many are ready (for example "Players · 2/3 ready"), so the lobby's state reads at a glance without counting tags.
- **Settings collapse into a summary card.** Below the players, the five settings are shown as one compact card of value chips (for example "60 min game", "5 min head start"). Tapping the card opens a bottom sheet: the owner gets the existing segmented settings form there, and other players get the read-only list. Saving behaviour (one `PUT /games/{id}/config` per change, controls locked while it runs, live updates for everyone) is unchanged.
- **The whole lobby is one scrolling list** below the top bar, with the Set ready / Start game bar pinned at the bottom as today, so a long player list is always reachable.
- New strings (ready count, settings summary chips, sheet title, share icon description) in English and Dutch.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `game-lobby-ui`: the code requirement places Share as an icon button after the code; the participant list requirement moves the list directly below the code and adds the player and ready counts; the configuration requirement shows the settings as a summary card that opens a bottom sheet (editable for the owner, read-only for others).

## Impact

- `app/src/app/src/main/java/nl/hexmaster/theprey/ui/games/lobby/LobbyScreen.kt`: layout of the content (code header, player section, settings card and bottom sheet).
- `LobbyUiState`: a derived ready count for the section title; covered by `LobbyUiStateTest`.
- `ui/games/create/GameSettingsForm.kt`: reused unchanged inside the sheet; a small summary-chip composable is added for the card.
- New drawable `ic_share.xml`.
- `res/values/strings.xml` and `res/values-nl/strings.xml`.
- Depends on `app-game-lobby-view` (still open): its `game-lobby-ui` delta must be archived first, because this change modifies the requirements as that change defines them.
- No backend, API or realtime protocol changes.
