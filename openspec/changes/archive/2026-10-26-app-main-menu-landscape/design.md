# Design

## Context

`MainMenuContent` (in `ui/mainmenu/MainMenuScreen.kt`) already wraps its content in a `BoxWithConstraints` inside the safe-drawing insets, and uses `maxWidth` to pick a compact slogan style. Its content is split into private composables (`Readout`, `Slogan`, `MenuActions`, plus the `RunningLightText` player name), so they can be rearranged without changes. The app has no other orientation-specific layouts yet, and the manifest does not lock orientation.

## Goals / Non-Goals

**Goals:**
- A landscape arrangement that reuses the existing section composables unchanged.
- Portrait rendering stays as it is today.

**Non-Goals:**
- Landscape layouts for other screens.
- Window size classes or tablet-specific designs.
- Locking or forcing orientation.

## Decisions

- **Landscape = `maxWidth > maxHeight` from the existing `BoxWithConstraints`.** It needs no new dependency (the `material3-window-size-class` artifact isn't used yet), reacts to split-screen and freeform windows as well as rotation, and matches the spec's "wider than tall". Alternative: `LocalConfiguration.orientation`. Rejected because it reports the device orientation, not the window the menu actually has.
- **Two layout composables, chosen in `MainMenuContent`.** Portrait keeps the current `Column`. A new `LandscapeMenu` puts `Readout` on top, then a `Row` with `Slogan` (weight 1, vertically centred) and `MenuActions` (weight 1, capped at 480 dp like today, vertically centred), and the centred player name at the bottom. The player-name block moves into a small `PlayerName` composable so both layouts share it.
- **One outer vertical scroll, as in portrait.** The landscape column uses the same `verticalScroll` + `heightIn(min = maxHeight)` + `SpaceBetween` pattern, so it fills the screen when the content fits and scrolls as a whole when it doesn't (for example, a phone at about 360 dp tall with a notice shown). Alternative: scroll only the right column. Rejected because the player name would sit outside the scroll and could cover the buttons.
- **Slogan sizing:** in landscape the left column is about half the width. Keep the existing `compact` switch, but base it on the column width (`< 360.dp`), so `displaySmall` is used when the column is narrow. Drop the slogan's portrait vertical padding of 40 dp in landscape to save height.
- **Previews:** add landscape previews (e.g. `widthDp = 800, heightDp = 360`) for the default state and for the blocked-play notice state, plus one Dutch landscape preview (`locale = "nl"`), because the Dutch slogan and buttons are longer.

## Risks / Trade-offs

- [Short landscape phones (≈ 320–360 dp tall) won't fit the readout, the buttons and the name without scrolling] → the outer scroll keeps everything reachable. The 48 dp secondary buttons are kept for touch-target size rather than shrunk.
- [Rotation recreates the activity] → state lives in `MainMenuViewModel` and flows in via `uiState`, so the layout switch loses nothing. The running-light animation simply restarts.
- [Very wide windows, such as tablets in landscape] → both columns are capped at 480 dp and centred, so lines stay readable.
