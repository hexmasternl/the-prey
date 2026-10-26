# Proposal

## Why

During a game players glance at the HUD constantly — the hunter checks the radar and tag range, the prey watches the hunter's last ping and their own penalty clock. The phone's normal screen timeout locks the display between glances, so every look costs an unlock and the game feels sluggish exactly when it matters. Players should be able to choose to keep the screen on while they play, without the app keeping it on anywhere else.

## What Changes

- A new **Display** section on the Settings screen with a switch **Keep screen on during a game** and a one-line explanation that it uses more battery. The switch is off by default.
- The choice is stored on the phone (Preferences DataStore, like the hints flags). It is not tied to the account, is not sent to the server, and survives sign-out.
- When the switch is on, the game screen keeps the display awake while a game is shown that has not ended: the start phase (head start countdown) and the running game, for both hunter and prey. The phone's own screen timeout applies again as soon as the player leaves the game screen, the result dialog appears, or the switch is turned off — also when it is turned off mid-game, since settings cannot be opened from the game but the value is observed live.
- Every other screen (main menu, lobby, playfields, settings, …) keeps the phone's normal timeout. The setting never wakes a locked screen and does not keep the device or CPU awake; it only stops the display from timing out while the game is in front.
- English and Dutch strings; the website and repo docs mention the setting.

## Capabilities

### New Capabilities
- `game-screen-always-on`: the player's on-device choice to keep the display awake while a game is in progress, its place in Settings, and exactly when the app holds and releases the screen.

### Modified Capabilities
<!-- None: the in-progress game view and settings screen specs live in unarchived changes; this capability owns the new behaviour on its own. -->

## Impact

- **Android app:**
  - New `data/display/` store (`DisplayPreferencesStore` + DataStore implementation, `display_preferences` file), wired in `AppContainer`.
  - `ui/settings/SettingsViewModel` + `SettingsScreen`: new Display section with a switch.
  - `ui/game/GameScreen`: keeps the view's `keepScreenOn` set while the setting is on and the game has not ended; cleared on exit and on dispose.
  - `ThePreyNavHost`: passes the store to the settings and game destinations.
  - Strings in `res/values/strings.xml` and `res/values-nl/strings.xml`.
  - Unit tests for the store, the view model and the pure "hold the screen?" rule.
- **Play Store:** no new permission — `keepScreenOn` / `FLAG_KEEP_SCREEN_ON` needs none (no `WAKE_LOCK`); no Data safety change (nothing leaves the phone).
- **Website / docs:** `how-to/get-started` Settings description and a FAQ entry, in `content/en` and `content/nl`.
- **Backend:** none.
