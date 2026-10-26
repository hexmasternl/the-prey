---
name: the-prey-android-feature
description: Conventions for building features in The Prey's native Android app (Kotlin + Jetpack Compose, app/src) — package layout per feature, ViewModel + StateFlow/sealed-state patterns, Retrofit API + repository with sealed outcomes, hand-wired DI through AppContainer, navigation routes and nested graphs in ThePreyNavHost, deep links, Auth0 session and token handling, location permission/consent and the tracking foreground service, and JUnit + coroutines-test unit tests with hand-written fakes. Use whenever you add or change a screen, ViewModel, repository, Retrofit API, DataStore store, route, deep link, permission or background behaviour in the Android app, implement an app-* OpenSpec change, or write app unit tests.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Android Feature Development

The app lives in `app/src` (the Gradle root; open that folder in Android Studio). One module `:app`, package `nl.hexmaster.theprey`, single activity (`MainActivity`), Compose + Material 3, **no DI framework, no Hilt, no Room, no mocking library**. Copy the patterns below from the existing code rather than inventing new ones.

Related skills: strings → `the-prey-multilingual`; colors, fonts, components, tone → `the-prey-design-system`; live game updates → `the-prey-realtime-communication`; DTOs/routes/status codes → `the-prey-api-contract-sync`; permissions, Data safety, releases → `the-prey-play-store`; game constants → `the-prey-game-rules`; branches/commits → `the-prey-git-workflow`; running against a local backend → `the-prey-local-dev`.

## Where things live

All paths below are under `app/src/app/src/main/java/nl/hexmaster/theprey/`.

| Concern | Location | Example to copy |
|---|---|---|
| App-wide wiring (DI) | `AppContainer.kt`, created once in `ThePreyApplication` | `playFieldRepository`, `gameRepository` |
| Activity, intents | `MainActivity.kt` | `onInviteIntent`, `onResumeTrackingIntent` |
| Routes + nav graph | `ui/navigation/ThePreyNavHost.kt` (`object Routes`) | `Routes.LOBBY`, nested `Routes.PLAYFIELD` graph |
| Screens + ViewModels | `ui/<area>/<feature>/` | `ui/games/join/JoinGameScreen.kt` + `JoinGameViewModel.kt` |
| Shared composables | `ui/components/` | `PrimaryButton`, `SecondaryButton`, `PreyMark`, `tour/` |
| Theme | `ui/theme/` (`Color.kt`, `Type.kt`, `Theme.kt`) | `ThePreyTheme`, `ThePreyTheme.colors` |
| HTTP API + repository + DTOs | `data/<domain>/` | `data/playfield/PlayFieldApi.kt`, `PlayFieldRepository.kt` |
| Device-local stores | `data/<domain>/…Store.kt` (Preferences DataStore) | `data/hints/HintsStore.kt`, `deeplink/PendingInviteStore.kt` |
| Auth / session | `auth/` | `SessionManager`, `AuthInterceptor`, `TokenAuthenticator` |
| Location + consent | `data/location/`, `ui/consent/` | `LocationSource`, `LocalLocationConsent` |
| In-game background tracking | `tracking/` | `TrackingController`, `LocationTrackingService`, `TrackingStarter` |
| Deep links | `deeplink/` | `parseJoinLink`, `joinLink` |
| Unit tests | `app/src/app/src/test/java/nl/hexmaster/theprey/` mirroring main | `ui/games/join/JoinGameViewModelTest.kt` |
| Strings | `app/src/app/src/main/res/values/strings.xml` + `values-nl/strings.xml` | see `the-prey-multilingual` |
| Dependencies | `app/src/gradle/libs.versions.toml`, `app/src/app/build.gradle.kts` | version catalog only |

## A feature, layer by layer

### 1. Retrofit API (`data/<domain>/<Domain>Api.kt`)

- `interface` with `suspend` functions returning `retrofit2.Response<T>` (never bare `T`), so the repository can read status codes. Relative paths without a leading slash (`@GET("playfields/{id}")`); the base URL is `BuildConfig.API_BASE_URL`.
- Document every status the endpoint returns in KDoc (see `PlayFieldApi`).
- DTOs are `@Serializable data class`es in the same package (`PlayFieldDto.kt`). JSON is kotlinx.serialization with `ignoreUnknownKeys = true`; use `@SerialName` only when the wire name differs.
- Don't add auth headers: the shared OkHttp client in `AppContainer` adds them.

### 2. Repository (`data/<domain>/<Domain>Repository.kt`)

- An `interface` (what ViewModels and tests see) plus an `Api…Repository` implementation that takes the API (and `Json` when it decodes error bodies).
- Return **sealed outcomes**, not exceptions: `sealed interface SaveOutcome { Saved(..), Conflict(current), Failed }`, `LoadOutcome`, `CommandResult`, `JoinResult`, or `Result<T>` for simple lists. Map HTTP codes to outcomes in the repository, never in the ViewModel.
- Wrap calls like `ApiPlayFieldRepository.guarded`: rethrow `CancellationException`, map `IOException` and `RuntimeException` (unparsable body) to the failure outcome.
- 409 on last-write-wins saves carries the stored entity in the error body; decode it into `Conflict(current)` (see `save`). Problem `code` values are parsed in `GameRepository` (`problemCode(...)`).

### 3. Wire it in `AppContainer`

```kotlin
/** One-line KDoc saying who uses it. */
val fooRepository: FooRepository = ApiFooRepository(retrofit.create(FooApi::class.java))
```

- Expose the **interface type**. Keep Auth0, OkHttp, Retrofit and `json` private.
- A DataStore-backed store gets its own file: add `private val Context.fooDataStore by preferencesDataStore(name = "foo")` at the top. Backups already exclude everything (`res/xml/backup_rules.xml`, `res/xml/data_extraction_rules.xml`); add the new file name to the comment listing the DataStore files in both, and keep the blanket excludes.
- Long-lived work uses `appScope` (process-wide `SupervisorJob` on `Dispatchers.Main.immediate`), never `GlobalScope`.
- Pass new dependencies down explicitly: `MainActivity` → `ThePreyNavHost(...)` parameter → the `viewModel { ... }` factory. There is no service locator inside screens.

### 4. ViewModel (`ui/<area>/<feature>/<Feature>ViewModel.kt`)

- Plain `androidx.lifecycle.ViewModel` with constructor-injected interfaces; no `Application`/`Context` inside. Inject time as a lambda (`now: () -> Long = System::currentTimeMillis`, `SystemClock::elapsedRealtime`) so tests control it.
- State: `private val _uiState = MutableStateFlow(...)` + `val uiState: StateFlow<...> = _uiState.asStateFlow()`, updated with `_uiState.update { it.copy(...) }`. Use a `data class …UiState` for forms/lists (`PlayFieldsUiState`) and a `sealed interface` for step flows (`JoinUiState`: `AwaitingLocation`, `Joining`, `Error`, `Done(exit)`).
- Derived state from repositories/services: `combine(...).stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MS), Loading)` (`LobbyViewModel`, `GameViewModel`).
- One-shot effects (snackbar, share sheet): `Channel<Event>(Channel.BUFFERED)` exposed via `receiveAsFlow()` (`PlayFieldsViewModel.deleteFailed`, `CreateGameViewModel`).
- Navigation out of a screen is **state, not a callback from the ViewModel**: put an exit in state (`JoinUiState.Done(JoinExit.Lobby(id))`, `LobbyExit`, `GameExit`) and let the screen call its `onExit` in a `LaunchedEffect`.
- Pull pure decisions into `internal fun`s next to the ViewModel (`activeGameStep`, `joinStep`) or separate files (`PlayAction.kt`, `GameSettings.kt`) and unit-test those directly.
- Process death / rotation: take a `SavedStateHandle` (`createSavedStateHandle()` in the factory) and store small keys (`KEY_TAB`, `KEY_QUERY`).

### 5. Screen (`ui/<area>/<feature>/<Feature>Screen.kt`)

- Public `@Composable fun FooScreen(viewModel: FooViewModel, onX: ..., modifier: Modifier = Modifier)` that collects with `collectAsStateWithLifecycle()`, then delegates to a **private stateless `FooContent(ui, callbacks)`**. Wrap callbacks used in effects with `rememberUpdatedState`; handle Back with `BackHandler`.
- Previews go at the bottom under `// ---- Previews ----`, one per state **and a Dutch one** (`@Preview(name = "Error (Dutch)", locale = "nl")`), each wrapped in `ThePreyTheme { ... }`.
- Text only from `stringResource(R.string.…)` (→ `the-prey-multilingual`). Colors only via `MaterialTheme.colorScheme` / `ThePreyTheme.colors`, fonts via `MaterialTheme.typography`, buttons from `ui/components/Buttons.kt` (→ `the-prey-design-system`). Use `safeDrawingPadding()`: the activity is edge-to-edge.

### 6. Route (`ui/navigation/ThePreyNavHost.kt`)

- Add a constant to `object Routes` (with KDoc) and, for arguments, a builder: `const val LOBBY = "lobby/{gameId}"` + `fun lobby(gameId: String)`.
- Signed-in screens go inside `navigation(route = Routes.SIGNED_IN, ...)`; the root switches between `LANDING` and `SIGNED_IN` from `SessionState` (`routeFor`) — never navigate to landing yourself, sign out instead.
- Create the ViewModel in the destination: `viewModel { FooViewModel(repo, createSavedStateHandle()) }`. A ViewModel shared by several screens is scoped to the nested graph's entry (`playFieldDetailsViewModel(graphEntry, ...)`).
- Guard callbacks against double taps during transitions: `if (navController.currentBackStackEntry == entry) ...`.
- Results back to the caller go through `savedStateHandle` (`PlayFieldSelectResult.deliver/consume`); messages for the main menu via `returnToMainMenu(MainMenuMessage.X)` (add the enum entry and both strings).

## Auth, location, deep links, background

- **Auth:** `SessionManager` is the only source of the signed-in state (`SessionState.Unknown/SignedOut/SignedIn(profile)`); screens depend on the `SessionController` / `ProfileSettings` interfaces. `AuthInterceptor` adds `Bearer` tokens from `Auth0CredentialsStore`; `TokenAuthenticator` refreshes once on 401 and calls `onSessionLost()` if that fails. Never read or attach tokens in features. The caller's own profile (user id, callsign) comes from `ProfileStore.read()`. Auth0 values come from `BuildConfig.AUTH0_*` (Gradle properties in `build.gradle.kts`).
- **Location:** read positions through `LocationSource.positions()` (only active while collected). Wrap every start/join-game action in `LocalLocationConsent.current.requireLocation(onDeclined, onGranted)` — it shows the prominent disclosure (`ui/consent/`) before the system prompt. Permission checks go through `readLocationAccess(...)` / `PermissionChecker` so they are testable. Any new permission or disclosure change → `the-prey-play-store`.
- **Background tracking:** only `TrackingStarter` (from a visible activity) may start `LocationTrackingService`; `TrackingController` is the single authority over in-game reporting. Don't start services or post locations from feature code.
- **Deep links:** invitation links `https://theprey.nl/games/join/{guid}/{code}` are declared in `AndroidManifest.xml` (`autoVerify`), parsed by `deeplink/parseJoinLink` (keep it in step with `website/src/static/games/join/index.html`), stored in `PendingInviteStore` by `MainActivity` and opened as `Routes.JOIN` once signed in. A new link type follows the same path: parse function + unit test, store, nav-host `LaunchedEffect`.
- **Realtime:** screens never open sockets. Read live game state from `GameSessions` (`AppContainer.gameStateService`); follow `the-prey-realtime-communication`.

## Unit tests

- **JUnit 4 + `kotlinx-coroutines-test` only** (`libs.junit`, `libs.kotlinx.coroutines.test`). No Mockito/MockK, no Robolectric, no Compose UI tests. `unitTests.isReturnDefaultValues = true` lets JVM tests touch inert Android types.
- Names: `method_ShouldExpected_WhenCondition` (`uiState_ShouldWaitForLocation_WhenCreated`).
- ViewModel tests: `Dispatchers.setMain(UnconfinedTestDispatcher())` in `@Before`, `resetMain()` in `@After`, bodies in `runTest { }`. For state holders that take a scope, use `plusUnconfined(test)` from `ui/playfields/TestScopes.kt`.
- Hand-written fakes. Reuse before writing new ones: `auth/Fakes.kt` (`aProfile`, `FakeProfileStore`, `FakeUserApi`, `FakeCredentialsStore`), `ui/FakeSession.kt`, `data/game/UnusedGamesApi.kt` (+ `UnusedGameRepository`: override only what you use, everything else fails with "Not used"), `tracking/TrackingFakes.kt` (`httpError(code)`, `FakeGamesApi`), `ui/playfields/ScriptedPlayFieldRepository.kt`.
- Repository tests fake the Retrofit interface and return `Response.success(...)` / `Response.error(code, body.toResponseBody())`; cover every status, `IOException`, an unparsable body, and that `CancellationException` propagates (`PlayFieldRepositoryTest`).
- Pure functions (mapping, geometry, timing, parsing) get direct tests; keep Android types out of them so they need no device.

## Dependencies

Add libraries only through `gradle/libs.versions.toml` and `implementation(libs.…)`. Keep `okhttp` aligned with what Auth0.Android and Retrofit are built against (comment in the catalog). AGP 9 has built-in Kotlin: do not apply `kotlin-android`.

## Verify

Run from `app/src` (PowerShell: `.\gradlew.bat …`):

```powershell
./gradlew assembleDebug lintDebug testDebugUnitTest    # the gate; CI runs lintDebug testDebugUnitTest
./gradlew testDebugUnitTest --tests "nl.hexmaster.theprey.ui.games.join.*"   # one area while iterating
```

Reports on failure: `app/src/app/build/reports/` and `app/src/app/build/test-results/` (CI uploads both). On a device/emulator: `./gradlew installDebug`; test a deep link with `adb shell am start -a android.intent.action.VIEW -d https://theprey.nl/games/join/<guid>/1234 nl.hexmaster.theprey`. To point the app at a local backend see `the-prey-local-dev`.

## Typical shape of an app-* change

The `openspec/changes/app-*/tasks.md` files follow this order; mirror it: API + DTOs + repository with outcome mapping (repository tests for every status) → wire in `AppContainer` → ViewModel with pure decision functions (tests for every branch) → screen with previews in both languages → strings in EN + NL → route in `ThePreyNavHost` → `lintDebug testDebugUnitTest` → manual device check (record anything you could not check in `tasks.md` and the PR). Also update `docs/` and the website when behaviour changes (`the-prey-docs-sync`).

## Checklist

- [ ] API returns `Response<T>`; repository maps status codes to a sealed outcome; cancellation rethrown.
- [ ] New dependency exposed as an interface from `AppContainer` and passed down explicitly; new DataStore file listed in the backup-rules comments.
- [ ] ViewModel uses `StateFlow` state, exits as state, `Channel` for one-shot events, injected clock; no `Context`.
- [ ] Screen is a thin wrapper over a stateless content composable with EN + NL previews.
- [ ] No hard-coded text or colors; strings in `values/` and `values-nl/`.
- [ ] Route added to `Routes` with KDoc; navigation guarded by `currentBackStackEntry == entry`.
- [ ] Location-using actions wrapped in `requireLocation`; no direct service starts or token handling.
- [ ] Unit tests with hand-written fakes for repository, ViewModel and pure functions.
- [ ] `./gradlew assembleDebug lintDebug testDebugUnitTest` passes from `app/src`.
