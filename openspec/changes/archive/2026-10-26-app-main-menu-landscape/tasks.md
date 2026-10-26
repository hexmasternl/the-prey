# Tasks

## 1. Landscape layout

- [x] 1.1 Extract the player-name block (spacer + `RunningLightText` + bottom spacer) from `MainMenuContent` into a private `PlayerName` composable; verify that the existing portrait previews render as before
- [x] 1.2 In `MainMenuContent`, choose landscape when `maxWidth > maxHeight` and keep the current column for portrait; verify that the portrait previews are unchanged and that `./gradlew compileDebugKotlin` succeeds
- [x] 1.3 Add a private `LandscapeMenu` composable: `Readout` on top, a `Row` with `Slogan` (left, weight 1, without the 40 dp vertical padding) and `MenuActions` (right, weight 1, max 480 dp), and `PlayerName` centred at the bottom, all inside the same `verticalScroll` + `heightIn(min = maxHeight)` + `SpaceBetween` column; verify in a landscape preview that the arrangement matches the spec
- [x] 1.4 Base the slogan's `compact` style on the width available to it (the left column in landscape); verify that the Dutch landscape slogan doesn't wrap awkwardly in the preview

## 2. Previews and checks

- [ ] 2.1 Add landscape previews (`widthDp = 800, heightDp = 360`): Play now, blocked play with its notice (scrolls), and a Dutch (`locale = "nl"`) variant; verify that they render in Android Studio _(previews added, compile and lint green; render check in Android Studio pending)_ — **auto: needs human:** visual check of the preview in Android Studio
- [x] 2.2 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and verify that both pass
- [ ] 2.3 On a device or emulator, rotate the main menu both ways (also during sign-out and with a play notice shown); verify the spec scenarios: columns, centred name, scrolling, and state kept _(device check pending)_ — **auto: needs human:** needs a real device or emulator
