# Design

## Context

- Settings (`ui/settings/SettingsScreen` + `SettingsViewModel`) has Callsign, Language, Location and Hints sections built from the private `SectionTitle` / `SectionCard` helpers. The view model is created in `ThePreyNavHost` with hand-wired dependencies from `AppContainer`.
- Device-local flags already use a dedicated Preferences DataStore per concern (`hints`, `location_consent`, …) behind a small interface (`HintsStore` / `DataStoreHintsStore`) with a hand-written fake in tests.
- `ui/game/GameScreen` renders `ActiveGameUi` from `GameViewModel`: `Loading`, `Error`, `Exit`, `Result` and `Ready` (which carries `ActiveGameState` with `GamePhase.Starting` / `Playing`). The lobby is a separate destination (`ui/games/lobby`).
- The app is single-activity; every screen draws in the same Compose host view, so any screen-on flag set from one destination must be cleared by that destination.

## Goals / Non-Goals

**Goals:**
- One on-device boolean, observed live by the game screen.
- The screen is held only while `GameScreen` composes a `Ready` game and released on every other path (result, exit, error, navigation away, process death).

**Non-Goals:**
- Keeping the CPU or GPS awake — location tracking already runs in the tracking foreground service.
- Dimming, brightness control or a battery saver mode.
- Syncing the setting to the server or across devices.
- Holding the screen in the lobby (explicitly out of scope per the request).

## Decisions

### 1. `View.keepScreenOn` via a `DisposableEffect`, not window flags or a wake lock
`GameScreen` adds a small composable `KeepScreenOn(enabled: Boolean)` that sets `LocalView.current.keepScreenOn = enabled` and resets it to `false` in `onDispose`. The flag is scoped to the visible window, so Android drops it automatically when the app goes to the background or the screen is turned off, and needs no permission.
- *Alternative:* `window.addFlags(FLAG_KEEP_SCREEN_ON)` from the activity — same effect, but needs the `Activity` and is easier to leak across destinations.
- *Alternative:* `PowerManager` wake lock — needs `WAKE_LOCK`, can outlive the UI and is deprecated for this purpose (`SCREEN_BRIGHT_WAKE_LOCK`). Rejected.

### 2. A pure rule decides when to hold the screen
`fun holdsScreen(ui: ActiveGameUi, enabled: Boolean): Boolean = enabled && ui is ActiveGameUi.Ready` lives next to `ActiveGameState` and is unit-tested. `Ready` covers both `GamePhase.Starting` and `Playing`; `Loading`, `Error`, `Exit` and `Result` release the screen. Keeping the rule pure keeps the Compose side to one line and the behaviour testable without instrumentation.

### 3. `DisplayPreferencesStore` in `data/display/`, own DataStore file
Interface `DisplayPreferencesStore { val keepScreenOnInGame: Flow<Boolean>; suspend fun setKeepScreenOnInGame(enabled: Boolean) }`, implemented by `DataStoreDisplayPreferencesStore` on a new `display_preferences` Preferences DataStore (app-private; add it to the backup exclusions where the other stores are excluded). Reads fall back to `false` on `IOException` like `HintsStore`; a failing write is logged and the switch reverts to the stored value.
- *Alternative:* add the key to the `hints` store — mixes concerns and `resetAll()` would have to skip it. Rejected.
- *Alternative:* name the store after the game (`GamePreferencesStore`) — "display" is the more durable home for future display options.

### 4. Settings wiring
`SettingsViewModel` takes the store, exposes `keepScreenOn` in `SettingsUiState` (collected from the flow) and `onKeepScreenOnChange(Boolean)`. `SettingsContent` gains a `DisplaySection` between Language and Location: a `SectionCard` with a row of label + Material 3 `Switch` (themed through `MaterialTheme.colorScheme`, the whole row toggleable for accessibility) and the battery note below in the body style.

### 5. Game wiring
`GameScreen` gets an optional `displayPreferences: DisplayPreferencesStore? = null` (mirroring `hints`), collects `keepScreenOnInGame` with `collectAsStateWithLifecycle(initialValue = false)` and calls `KeepScreenOn(holdsScreen(ui, enabled))`. `ThePreyNavHost` passes `appContainer.displayPreferencesStore` to both game destinations and to the settings view model. Previews and existing tests keep working through the `null` default.

## Risks / Trade-offs

- [Battery drain during long games] → off by default; the Settings note says it uses more battery; the screen still turns off with the power button.
- [Flag leaks to other screens through the shared host view] → `onDispose` always resets it, and the flag follows `ui` so it drops the moment the result shows.
- [Store read fails] → treated as off: the safe default is the phone's normal behaviour.
- [Burn-in on OLED from a static HUD] → acceptable for game-length sessions; the map and timers move.

## Migration Plan

Pure app change, no data migration: a missing key reads as off. Ships with the next internal-track build; rollback is reverting the commit.
