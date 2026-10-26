# Tasks

## 1. Auth0 tenant and build configuration

- [x] 1.1 In the Auth0 tenant `theprey.eu.auth0.com`: create or reuse a Native application for `nl.hexmaster.theprey`, set Allowed Callback and Logout URLs to `https://theprey.eu.auth0.com/android/nl.hexmaster.theprey/callback`, enable the Refresh Token grant with rotation, and register the debug, upload and Play App Signing SHA-256 fingerprints; verify `https://theprey.eu.auth0.com/.well-known/assetlinks.json` lists the package and fingerprints
- [x] 1.2 Enable "Allow Offline Access" on the `https://api.theprey.nl` API in Auth0 and verify a test login requesting `offline_access` returns a refresh token
- [x] 1.3 Add Auth0.Android, Navigation Compose, Lifecycle ViewModel Compose, OkHttp, Retrofit, kotlinx.serialization (plugin, JSON, Retrofit converter), DataStore Preferences and kotlinx-coroutines-test to `gradle/libs.versions.toml` with exact stable versions and to `app/build.gradle.kts`; verify `./gradlew assembleDebug` succeeds
- [x] 1.4 Add Gradle properties `auth0Domain`, `auth0ClientId`, `auth0Scheme` (default `https`) and `apiBaseUrl` (default `https://api.theprey.nl/`), expose them as `BuildConfig` fields and Auth0 manifest placeholders, enable `buildConfig`; verify the generated `BuildConfig` contains the values and the merged manifest contains the Auth0 redirect activity with the right host/path
- [x] 1.5 Add `ThePreyApplication` with an `AppContainer` (manual wiring) and register it in `AndroidManifest.xml`; update `data_extraction_rules.xml` / `backup_rules.xml` to exclude the Auth0 credential storage and the profile DataStore; verify the app still launches to the current screen

## 2. Backend client and profile cache

- [x] 2.1 Add `@Serializable` `UserDto` and `CreateUserRequest` models matching the Users API, and a Retrofit `UserApi` with `POST users` and `GET users/me`; add a unit test that deserializes a sample `UserDto` JSON payload and serializes a `CreateUserRequest`
- [x] 2.2 Implement `ProfileStore` (DataStore, JSON-encoded `UserDto`) with `profile: Flow<UserDto?>`, `save`, `clear`; verify with a unit test against a temp-file DataStore that save/read/clear round-trip _(tested against an in-memory DataStore: the file-backed one cannot replace its file on Windows JVMs; on-device persistence is covered by 7.2)_
- [x] 2.3 Wrap the SDK behind interfaces `CredentialsStore` (has valid credentials, get/save/clear, force refresh) and `WebAuthClient` (login/logout) with Auth0 implementations using `SecureCredentialsManager` and `WebAuthProvider` (audience, `openid profile email offline_access`, scheme from `BuildConfig`); verify it compiles and fakes of both interfaces exist in the test source set

## 3. Session management

- [x] 3.1 Implement `SessionManager` with `StateFlow<SessionState>` (`Unknown`, `SignedOut(reason)`, `SignedIn(profile)`) and `restore()`: credentials + cached profile → `SignedIn` immediately; credentials without profile → identify first; none → `SignedOut`; verify with unit tests covering each branch
- [x] 3.2 Implement background profile refresh after restore (`GET /users/me`, 404 → `POST /users`, network failure keeps cached profile); verify with unit tests for 200, 404 and offline
- [x] 3.3 Implement `signIn(activity)`: web login → reject missing refresh token or missing email (clear credentials, typed error) → save credentials → `POST /users` with ID-token claims and device language (`nl`/`en`) → cache profile → `SignedIn`; map cancel to a silent no-op; verify unit tests for success (200 and 201), cancel, IdP error, no email, no refresh token and identification network failure
- [x] 3.4 Implement `signOut(activity)` (best-effort `WebAuthProvider.logout`, then always clear credentials and profile) and `onSessionLost()`; verify unit tests for online sign-out, offline sign-out and session loss all ending in `SignedOut` with cleared stores

## 4. Authenticated HTTP

- [x] 4.1 Add `AuthInterceptor` that attaches `Authorization: Bearer <access token>` from `CredentialsStore`, and wire it into the OkHttp client used by Retrofit; verify with a unit test using a fake `Interceptor.Chain` that the header is present
- [x] 4.2 Add `TokenAuthenticator` that on `401` force-refreshes once and retries, and calls `SessionManager.onSessionLost()` when renewal is rejected or the retry is still `401`; network-caused renewal failures propagate as `IOException` and keep the session; verify unit tests for retry-succeeds, still-401, revoked refresh token and offline renewal
- [ ] 4.3 Ensure no token or `Authorization` header is logged (no OkHttp body/header logging in release; redact `Authorization` in debug); verify by inspecting logcat during a debug sign-in _(code done: no HTTP logging interceptor, no token in any log call; logcat check pending a device sign-in)_ — **auto: needs human:** needs a real device or emulator

## 5. Landing page and main menu UI

- [x] 5.1 Build `LandingScreen` + `LandingViewModel` per the `the-prey-design-system` skill: Prey mark, "THE PREY", tagline, primary "Sign in" button, busy state disabling the button, inline translated error text; add Compose previews for idle, busy and error; verify previews render and the screen reads colors only from the theme
- [x] 5.2 Build `MainMenuScreen` + `MainMenuViewModel`: greets by callsign (fallback display name) from `SessionState.SignedIn`, updates when the profile changes, and offers "Sign out"; add a preview; verify a ViewModel unit test that a profile update changes the greeting
- [x] 5.3 Add all new strings (sign in, signing in, sign out, greeting, errors: network, no email, generic) to `res/values/strings.xml` and Dutch in `res/values-nl/strings.xml`; verify `./gradlew lintDebug` reports no missing translations
- [x] 5.4 Delete `ui/welcome/WelcomeScreen.kt` and the now-unused `welcome_*` strings; verify the build has no references left

## 6. Launch routing and navigation

- [ ] 6.1 Add Navigation Compose `NavHost` in `MainActivity` with `landing` and `mainMenu` destinations; call `sessionManager.restore()` at startup and keep the system splash on screen while `SessionState.Unknown`; choose the start destination from the restored state (after the `app-splash-screen` launch sequence when present); verify on a device that a signed-in cold start never shows the landing page _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 6.2 Observe session state in the root composable: on `SignedIn` navigate to `mainMenu` and on `SignedOut` navigate to `landing`, both with `popUpTo(0) { inclusive = true }`; verify on a device that Back from the main menu (and from the landing page after sign-out) exits the app _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [x] 6.3 Unit-test the pure start-destination function (`SessionState` → route) for `SignedIn`, `SignedOut` and expired-session cases

## 7. End-to-end verification

- [x] 7.1 Run `./gradlew lintDebug testDebugUnitTest assembleDebug` from `app/src` and verify all pass
- [ ] 7.2 On a device against the real API: first sign-in creates the user (`POST /users` 201), main menu greets the player; kill and relaunch → main menu directly; airplane mode relaunch → main menu with cached profile; sign out → landing page and next sign-in asks for credentials again _(manual device run pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 7.3 Revoke the refresh token for the test user in Auth0 (or delete the device grant), relaunch and trigger an API call; verify the app returns to the landing page without a "connection" error _(manual Auth0 + device check pending)_ — **auto: needs human:** needs a real device or emulator
