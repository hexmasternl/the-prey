# Design

## Context

- The main menu's Play now calls `onPlayNow`, which navigates to `Routes.PLAY`, a `PlaceholderScreen`. `Routes.GAME` (`game/{gameId}`) is a placeholder for Resume game.
- `app-game-select-playfield` (designed, not yet built) adds `Routes.PLAYFIELD_SELECT` and `PlayFieldSelectResult`. `deliver(...)` writes a `PlayFieldSelection(id, name, isPublic)` as JSON into the caller entry's `SavedStateHandle`. `consume(...)` returns a `Flow` that emits it once and then removes it. When the picker is cancelled, it pops without writing anything.
- `GamesApi` / `ApiGameRepository` (`data/game/`) only read data so far. `ActiveGameResult` maps responses to a small sealed type (`Active` / `None` / `Failed`), catching `IOException` and `RuntimeException` and rethrowing `CancellationException`.
- Server: `POST /games` takes `CreateGameRequest(PlayfieldId, DisplayName, GameDuration, HunterDelayTime, FinalStageDuration, DefaultLocationInterval, FinalLocationInterval, EnablePreyBoundaryPenalties, EnableHunterBoundaryPenalty, ProfilePictureUrl?)`. It returns `201` with `GameDto`, or `400` with a validation problem when `GameConfiguration.Create` throws. Among other checks, `GameConfiguration.Create` requires `FinalLocationInterval <= DefaultLocationInterval` and `HunterDelayTime`, `FinalStageDuration < GameDuration`.
- The signed-in profile (`UserDto` with `callsign` and `displayName`) is cached in `ProfileStore`.
- `PrimaryButton` already supports a `busy` state.

Requirements: `specs/game-create-page/spec.md`.

## Goals / Non-Goals

**Goals:**
- Option values, defaults and the interval rule live in one plain Kotlin model that is unit-tested. The UI only renders it.
- The create flow is consistent across rotation, process death and the picker round trip.
- A request that is valid for the server is the only kind the UI can send.

**Non-Goals:**
- The lobby itself. This change only navigates to a `lobby/{gameId}` placeholder, which a later lobby change replaces.
- Boundary penalty toggles and a profile picture. Both penalties are sent as `false`, and no `profilePictureUrl` is sent.
- Remembering the last-used settings between games.
- Blocking creation when the player already owns a game in `Lobby`. The server allows it, and the main menu's active-game check only covers `Started`/`InProgress`.
- Any backend change.

## Decisions

### 1. `GameSettings` option model
`ui/games/create/GameSettings.kt`:
```kotlin
enum class GameSetting(val options: List<Int>, val default: Int) {
    Duration(listOf(30, 60, 90), 30),
    HunterDelay(listOf(3, 5, 10), 5),
    FinalStage(listOf(3, 5, 10), 10),
    PingInterval(listOf(2, 3, 5), 3),
    FinalPingInterval(listOf(1, 2, 3), 1),
}

data class GameSettings(
    val duration: Int = 30, val hunterDelay: Int = 5, val finalStage: Int = 10,
    val pingInterval: Int = 3, val finalPingInterval: Int = 1,
) {
    fun with(setting: GameSetting, minutes: Int): GameSettings   // ignores values not in options
    fun isEnabled(setting: GameSetting, minutes: Int): Boolean   // FinalPingInterval: minutes <= pingInterval
}
```
All values are minutes. `with(PingInterval, x)` clamps `finalPingInterval` to the largest allowed option `<= x` (spec: "Final stage choice follows a lowered ping interval"). With the current options, hunter delay and final stage are always smaller than the shortest duration, so they need no cross-rule. A unit test checks that every combination is valid (see Risks).
- **Alternative:** show a validation error on submit. Rejected: the player could build an invalid game and only learn about it from a server 400.
- **Alternative:** offer only final-stage options that fit, hiding the rest. Rejected: the control would jump around. Disabled options keep the layout stable and show that a rule applies.

### 2. Repository call and result
`GamesApi`:
```kotlin
@POST("games") suspend fun createGame(@Body request: CreateGameRequest): Response<GameDto>
```
`CreateGameRequest` is a `@Serializable` data class mirroring the server record (camelCase JSON). It holds `gameDuration`, `hunterDelayTime` and `finalStageDuration` in minutes, `defaultLocationInterval` and `finalLocationInterval` in seconds, and both penalty flags. The existing `GameDto` is reused for the response; only `id` is needed.

`GameRepository.createGame(newGame: NewGame): CreateGameResult`. `NewGame` (`data/game/`) holds `playFieldId`, `displayName` and the five values in minutes, so `data/` does not depend on `ui/`. The view model builds it from `GameSettings`. The result is `sealed interface CreateGameResult { data class Created(val gameId: String); data object Failed }`. Any non-2xx status, an empty body, an `IOException` or a parse failure is `Failed`, following `activeGame()`. Converting minutes to seconds happens in the repository, in one place, and is tested.
- **Alternative:** surface the server's validation problem text. Rejected for now: the UI only allows valid combinations, so a 400 indicates a bug or a server-side rule change, and one generic retryable error is enough. The detail is logged.

### 3. `CreateGameViewModel`
`ui/games/create/CreateGameViewModel(gameRepository, profileStore, savedStateHandle)` exposes:
```kotlin
data class CreateGameUiState(
    val playField: PlayFieldSelection? = null,
    val settings: GameSettings = GameSettings(),
    val submitting: Boolean = false,
    val failed: Boolean = false,
) { val canCreate get() = playField != null && !submitting }
```
plus a one-shot `events: Flow<CreateGameEvent>` (`Created(gameId)`) delivered through a `Channel`, so the navigation happens exactly once.
- Events: `onPlayFieldSelected(selection)`, `onSettingChange(setting, minutes)` (ignored while `submitting`), `create()`, `dismissError()`.
- `create()` ignores calls while `submitting` (spec: "a second request is not sent"). It reads the display name from `profileStore.read()` (`callsign`, falling back to `displayName` when the callsign is blank) and calls the repository. On `Created` it emits the event; on `Failed` it sets `failed = true`. Retry calls `create()` again.
- `playField` (id, name, isPublic) and the five settings are stored as primitive keys in `SavedStateHandle` (spec: "Choices survive rotation"). `submitting` is not stored: after process death the request outcome is unknown, so the player sees the form again. See Risks.

### 4. Opening the picker first
The screen observes `PlayFieldSelectResult.consume(entry.savedStateHandle)` and passes each emission to `onPlayFieldSelected`. Two behaviours depend on whether the screen has ever had a playfield:
- **Auto-open:** a `pickerLaunched` flag in `SavedStateHandle`. On first composition, if `playField == null && !pickerLaunched`, the screen sets the flag and calls `onChoosePlayField()`. The flag prevents a reopen loop after a cancel, and keeps a rotation of the picker from opening a second one.
- **Cancel on first open:** when the create entry is resumed with `pickerLaunched && playField == null`, the picker was cancelled before anything was chosen, so the screen calls `onBack()` (spec: "Cancelling the first choice returns to the main menu"). It checks this in a `LifecycleResumeEffect`, after collecting the result, so a delivered selection always arrives first: the result is written to the handle before the pop, and `consume` reads it synchronously when collection starts.
- **Alternative:** route Play now to the picker, and have the picker's caller navigate to the create screen with the selection as an argument. Rejected: changing the playfield later would need a second path, and the picker would have to know about game creation.
- **Alternative:** a two-step wizard. Rejected: the settings fit on one screen and all have defaults, so extra steps add taps without adding clarity.

### 5. Screen layout
`CreateGameScreen(viewModel, onBack, onChoosePlayField, onCreated: (String) -> Unit)`:
- `Scaffold` top bar "New game" with a back arrow (`onBack`). The content scrolls vertically.
- **Playfield row**: a `surface` card with the label "Playfield" and the chosen name in Special Elite, plus a change affordance. Tapping it calls `onChoosePlayField`. It shows "Choose a playfield" while empty.
- **Five setting blocks**: a label and helper line in PT Mono (for example "Hunter delay" / "Time the preys get before the hunter may move"), then a Material 3 `SingleChoiceSegmentedButtonRow` with one `SegmentedButton` per option. Each option's label is a plural string ("30 min"). Disabled options follow `GameSettings.isEnabled`. The whole row is disabled while submitting. Colours come from `MaterialTheme.colorScheme`; check the `the-prey-design-system` skill for the selected segment treatment.
- **Bottom bar**: an inline error panel when `failed` (message + Retry, which calls `create()`), then a full-width `PrimaryButton("Create game", enabled = canCreate, busy = submitting)`. It applies `navigationBarsPadding()`.
- `LaunchedEffect` collects `events` and calls `onCreated(gameId)`.

### 6. Navigation
- `Routes.PLAY` is replaced by `Routes.GAME_CREATE = "game_create"`, and `onPlayNow` navigates to it.
- `Routes.LOBBY = "lobby/{gameId}"` with `fun lobby(gameId)`, a `PlaceholderScreen` until the lobby change.
- `onCreated = { id -> navController.navigate(Routes.lobby(id)) { popUpTo(Routes.GAME_CREATE) { inclusive = true } } }`, so Back from the lobby returns to the main menu (spec: "Successful game creation").
- `onChoosePlayField = { navController.navigate(Routes.PLAYFIELD_SELECT) }`.
- The view model is created with `viewModel { CreateGameViewModel(gameRepository, profileStore, createSavedStateHandle()) }` and is scoped to the entry, so every Play now starts fresh with the defaults.

### 7. Strings
Per the `the-prey-multilingual` skill, these go in both `values/` and `values-nl/`:
- `game_create_title` (New game / Nieuw spel)
- `game_create_playfield` (Playfield / Speelveld)
- `game_create_playfield_choose` (Choose a playfield / Kies een speelveld)
- `game_create_duration` (Game duration / Speelduur)
- `game_create_hunter_delay` (Hunter delay / Vertraging jager)
- `game_create_final_stage` (Final stage / Eindfase)
- `game_create_ping_interval` (Location ping / Locatie-ping)
- `game_create_final_ping_interval` (Ping in final stage / Ping in eindfase)
- A one-line help text per setting.
- `game_create_minutes` plurals (%d min / %d min).
- `game_create_submit` (Create game / Spel aanmaken)
- `game_create_failed` (The game could not be created. Check your connection and try again. / Het spel kon niet worden aangemaakt. Controleer je verbinding en probeer het opnieuw.)
- `lobby_title` for the placeholder (Lobby / Lobby).

Retry reuses the existing retry string.

### 8. Docs
`docs/api/endpoints.md`: the `POST /games` request and response examples are replaced with the real `CreateGameRequest` body and a short `GameDto` excerpt (`id`, `joinCode`, `status: "Lobby"`), with the units noted (minutes vs seconds).

## Risks / Trade-offs

- [Option values change later and a combination becomes invalid for the server, for example a final stage that is not shorter than the duration] → a unit test enumerates every option combination and asserts it is valid under the server's rules, mirrored in the test. A new value that breaks a rule fails the build instead of returning a 400.
- [The process dies while the request is in flight, and the game is created but the app never learns its id] → the player sees the form again and may create a second game. That orphan lobby game is cleaned up after 48 h (`CleanUpAfter`). This is acceptable for a rare case. Recovering the game would need a "my lobby games" endpoint, which is out of scope.
- [The auto-open and cancel detection rely on lifecycle ordering] → the logic sits behind the `pickerLaunched` flag and the result delivery, both covered by view model tests. The navigation behaviour is checked manually (task 5).
- [The callsign is blank or the profile is not cached yet] → fall back to `displayName`. The signed-in flow always has a cached profile before the main menu is shown.
- [This change depends on `app-game-select-playfield`] → the tasks start by verifying that change is implemented. Its route and result contract are used exactly as designed there.

## Migration Plan

This is a client-only change that ships with the next app release. Play now changes from a placeholder to a working flow, and no server or data migration is needed. To roll back, release a build that points Play now back at the placeholder.
