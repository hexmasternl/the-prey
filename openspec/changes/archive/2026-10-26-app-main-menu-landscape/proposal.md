# Proposal

## Why

The main menu is a single portrait column: readout, slogan, buttons, then the player name. When the phone is turned to landscape, the column far exceeds the short screen height and the player has to scroll past the slogan to reach the buttons. A side-by-side layout uses the width landscape gives and keeps every action in view.

## What Changes

- In landscape the main menu switches to a two-column layout:
  - The readout (position and "Operational field manual vX") stays at the top across the full width, as in portrait.
  - The slogan ("Stay hidden. Hunt smart.") and the description ("A GPS based…") sit in the left column.
  - The action buttons (Play now / Resume game with its notice, Playfields, Settings, Sign out, Quit) sit in the right column.
  - The player's display name sits centred at the bottom across the full width, below both columns.
- Portrait layout is unchanged.
- No text changes, so no new strings in English or Dutch.

## Capabilities

### New Capabilities
- `main-menu-layout`: how the app's main menu arranges its readout, slogan, actions and player name for portrait and landscape screens.

### Modified Capabilities
<!-- none -->

## Impact

- `app/src/app/src/main/java/nl/hexmaster/theprey/ui/mainmenu/MainMenuScreen.kt`: layout only; `MainMenuViewModel`, `MainMenuUiState` and the play-action logic are untouched.
- New Compose previews for landscape (English and Dutch).
- No backend, API or string-resource changes.
