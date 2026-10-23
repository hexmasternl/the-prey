# Design

## Context

- `MainMenuScreen` renders `PrimaryButton(main_menu_play_now)` unconditionally; `onPlayNow` navigates to the `play` placeholder route in `ThePreyNavHost`.
- `MainMenuViewModel` combines session, sign-out and position flows into `MainMenuUiState`. Location permission reaches it only through `onLocationPermissionResult(granted)`, today fed by the screen's own permission check (fine **or** coarse). The in-flight `app-location-foreground-service-warning` change moves the prompt into a `LocationConsentHost`; this change only *reads* permissions and never prompts.
- The app has no games API client yet. `UserApi` shows the pattern: a Retrofit interface returning `Response<T>`, created from the shared authenticated `retrofit` in `AppContainer`.
- `GET /games/active` returns `200 GameStatusDto` for a `Started`/`InProgress` game the caller participates in, `404` otherwise (`GameEndpoints.GetActiveGame`).
- `ThePreyTheme` maps `error = Hunter` (#FF2F1F) and exposes `ThePreyTheme.colors.hunter`; there is no red button yet.
- The manifest declares fine and coarse location only.

## Goals / Non-Goals

**Goals:**
- A single, unit-testable function that turns (permissions, active-game result) into the button state and the error to show.
- A games API client the later `app-location-service` change can extend (`locations`, `gamestate`) instead of adding a second one.

**Non-Goals:**
- The real play and game screens. `Play Now` keeps opening the `play` placeholder; `Resume Game` opens a new placeholder route carrying the game id.
- Requesting any runtime permission. Foreground location is requested by the consent flow; background location is granted only in system settings (see Decision 3).
- Starting the tracking service on resume. That belongs to `app-location-service`.

## Decisions

### 1. Two inputs, one derived state

The view model holds two independent flows:

```kotlin
enum class LocationAccess { Missing, ForegroundOnly, Granted }   // fine-only counts; coarse-only = Missing
sealed interface ActiveGameCheck { Loading; None; Active(gameId); Failed }
```

`playAction(access, check): PlayActionState` is a pure top-level function returning the button (`label = PlayNow | ResumeGame`, `style = Signal | Hunter`, `enabled`) plus an optional `PlayActionError` (`LocationMissing`, `BackgroundLocationMissing`, `CheckFailed`). Rules, in priority order:

| access \ check | Loading | None | Active | Failed |
|---|---|---|---|---|
| Granted | grey Play Now | green Play Now | red Resume Game | grey Play Now + CheckFailed |
| ForegroundOnly | grey + BackgroundLocationMissing | grey Play Now + BackgroundLocationMissing | grey Resume Game + BackgroundLocationMissing | grey + BackgroundLocationMissing |
| Missing | grey + LocationMissing | grey Play Now + LocationMissing | grey Resume Game + LocationMissing | grey + LocationMissing |

A permission error wins over `CheckFailed`: fixing settings is the player's first step, and the check is re-run on resume anyway. `uiState` gets a `playAction` field computed in the existing `combine`.

*Alternative:* derive the state in the composable. Rejected: the table is the core of the feature and has to be covered by JVM unit tests like the rest of `MainMenuViewModel`.

### 2. Re-check on `ON_RESUME` of the menu destination

`MainMenuScreen` observes its `LocalLifecycleOwner` (the NavBackStackEntry) and on every `ON_RESUME`:
1. reads `ACCESS_FINE_LOCATION` and, on API 29+, `ACCESS_BACKGROUND_LOCATION` with `ContextCompat.checkSelfPermission` and calls `viewModel.onLocationAccess(access)`; below API 29 a fine grant implies background;
2. calls `viewModel.refreshActiveGame()`, which sets `Loading` and launches the request, cancelling any request still in flight.

The back-stack entry resumes when the menu is first shown, when the player comes back from a sub-screen, and when the app returns from system settings, which covers all spec triggers with one hook. Retry calls `refreshActiveGame()` directly.

`onLocationAccess` also drives the position readout (`Granted` or `ForegroundOnly` → readout runs), replacing `onLocationPermissionResult(Boolean)`. When the consent change lands, its host's grant callback triggers the same re-read.

*Alternative:* check once in `init`. Rejected: the spec requires a fresh check on every visit and after returning from settings.

### 3. Background location only via system settings

`ACCESS_BACKGROUND_LOCATION` is added to the manifest. The app never launches a runtime request for it: on Android 11+ the system only grants "Allow all the time" from the settings page, and on Android 10 the in-dialog option would bypass the error-then-settings flow the requirements ask for. "Open settings" fires `Settings.ACTION_APPLICATION_DETAILS_SETTINGS` with `package:nl.hexmaster.theprey`. Because the screen re-reads permissions on resume, the button updates as soon as the player returns.

This resolves the open point in the consent change's Decision 2 in favour of background location, and contradicts `app-location-service`'s "No `ACCESS_BACKGROUND_LOCATION`" line; that proposal gets a one-line update in the tasks.

### 4. `GamesApi` + `GameRepository` under `data/game/`

```kotlin
interface GamesApi { @GET("games/active") suspend fun getActiveGame(): Response<ActiveGameDto> }
@Serializable data class ActiveGameDto(val gameId: String)   // ignoreUnknownKeys drops the rest
interface GameRepository { suspend fun activeGame(): ActiveGameResult }   // Active(id) | None | Failed
```

The repository maps 200 → `Active`, 404 → `None`, any other status or `IOException` → `Failed`. Cancellation is rethrown. The view model depends on `GameRepository` so tests use a fake, as with `FakeSession` and `FakeLocationSource`. A 401 after a failed refresh already ends the session through `AuthInterceptor`/`TokenAuthenticator`, so the menu disappears; the repository just reports `Failed`.

### 5. Hunter style on `PrimaryButton`

`PrimaryButton` gets `style: PrimaryButtonStyle = PrimaryButtonStyle.Signal`; `Hunter` uses `ThePreyTheme.colors.hunter` as container and `MaterialTheme.colorScheme.onError` as content. Disabled colors stay the same for both, so "greyed out" looks identical in every state. No new hard-coded colors.

### 6. Error panel under the button

A `PlayActionNotice` composable directly below the play button: Hunter-tinted label text (PT Mono, `labelMedium`) with the message and one `SecondaryButton` (Open settings or Retry, 48 dp). It uses `liveRegion = Polite` semantics so TalkBack announces it when it appears. The disabled button gets a `stateDescription` naming the reason, because disabled buttons alone tell screen-reader users nothing.

### 7. Resume route

`Routes.GAME = "game/{gameId}"` with a placeholder screen (title "Resume Game") until the game screen exists. `MainMenuScreen` takes `onResumeGame: (String) -> Unit`.

## Risks / Trade-offs

- [Play policy: background location needs a declaration and review; it can be rejected if the use is not clear] → The disclosure from the consent change already states background collection during a game; the Play Console declaration must cite it. Release of this change waits on that declaration.
- [Players without "Allow all the time" can no longer start or resume a game] → Intended. The error names the exact setting and the button opens the page where it lives.
- [Grey flash on every resume while the check runs] → Accepted; the request is small and it keeps the "disabled until checked" rule simple. If it becomes annoying, keep the last result visible during a refresh.
- [Network down at launch blocks playing] → The Retry action and the re-check on resume recover; a game cannot run offline anyway.
- [Interaction with the in-flight consent change] → Both touch `MainMenuScreen`'s permission wiring. Whichever lands second replaces the screen-local fine/coarse request with the consent host and keeps the `ON_RESUME` re-read from Decision 2.

## Migration Plan

App-only release. Existing installs that only granted "While using the app" see the background location error and the Open settings button on first launch after the update. Rollback = previous app version; the extra manifest permission is harmless.
