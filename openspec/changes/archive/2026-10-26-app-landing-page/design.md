# Design

## Context

- The Android app (`app/src`) is a fresh single-activity Compose app: `MainActivity` installs the core-splashscreen and shows `WelcomeScreen`. There is no networking, no navigation, no DI and no persistence yet. The in-flight `app-splash-screen` change adds a Compose launch sequence that hands off to "the home screen".
- The backend validates Auth0 JWTs in ServiceDefaults (authority `https://theprey.eu.auth0.com/`, audience `https://api.theprey.nl`, `MapInboundClaims = false`) and identifies the caller by `sub`. The Users API exposes `POST /users` (idempotent register: 201 created / 200 existing, requires `EmailAddress`) and `GET /users/me` (200 `UserDto` / 404).
- Known Auth0 trap from the previous client: if the API does not have "Allow Offline Access" enabled, Auth0 silently drops `offline_access` and issues no refresh token, producing a "zombie" session that looks signed in but cannot mint tokens.
- `AndroidManifest.xml` already sets `allowBackup="false"` and has backup / data-extraction rule files.

See `proposal.md` for motivation and `specs/` for the behavioral contract.

## Goals / Non-Goals

**Goals:**
- One app-scoped source of truth for "who is signed in" that every screen and the HTTP client observe.
- A launch route decision that uses local state only, so it fits inside the splash / launch sequence without network waits.
- A small, testable structure (session logic behind interfaces) that later features (lobby, games, realtime) plug into via the same authenticated HTTP client.

**Non-Goals:**
- Biometric unlock or app-level PIN in front of a restored session.
- Editing callsign or language (exists server-side; a settings screen comes later).
- Any real main-menu content beyond greeting and sign-out.
- Wiring the version gate (`app-version-gate`) into the Android client.
- Backend or Bicep changes.

## Decisions

### 1. Auth0.Android SDK (`WebAuthProvider` + `SecureCredentialsManager`)
Use the official Auth0.Android SDK (3.x, pinned in `libs.versions.toml`). `WebAuthProvider.login()` runs Authorization Code + PKCE in a Custom Tab; `SecureCredentialsManager` encrypts credentials with an Android Keystore-backed key, renews expired access tokens with the refresh token, handles refresh-token rotation, and serializes concurrent `getCredentials` calls so parallel requests share one renewal.
- *Alternative: AppAuth-Android + EncryptedSharedPreferences.* Provider-neutral, but we would hand-write token persistence, rotation and concurrency; `EncryptedSharedPreferences` is also deprecated. More code, more risk, no benefit while Auth0 is the only IdP.
- *Alternative: own login form with Resource Owner Password grant.* Rejected: the app would handle passwords, no MFA/social login, and it is discouraged for native apps.

Login parameters: scheme from build config, `audience = https://api.theprey.nl`, `scope = "openid profile email offline_access"`. After login the app checks that a refresh token was returned; if not, it treats the login as failed (clears credentials and logs a config warning) instead of creating a zombie session.

### 2. Redirect via Android App Links (`https` scheme)
Configure `manifestPlaceholders["auth0Domain"]` and `["auth0Scheme"] = "https"`, so the callback is `https://theprey.eu.auth0.com/android/nl.hexmaster.theprey/callback`. Auth0 serves the matching `assetlinks.json` once the app's SHA-256 fingerprints are registered on the Native application, so no other app can intercept the redirect.
- *Alternative: custom scheme (`theprey://`).* Works without fingerprints, but any app can register the same scheme. Kept available only as a fallback: the scheme is a Gradle property (`auth0Scheme`) so a local debug build can switch to a custom scheme if its debug fingerprint is not registered.

Domain, client id, scheme and API base URL are Gradle properties exposed as `BuildConfig` fields and manifest placeholders (defaults: `theprey.eu.auth0.com`, `https`, and the gateway `https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io/` until `api.theprey.nl` is bound). The client id of a Native app is public, so it may be committed as the default; CI can override it.

### 3. Single activity, Navigation Compose destinations
The "main menu" is a Compose destination in `MainActivity`, not a second Activity. Destinations: `landing` and `mainMenu` (launch-sequence screen from `app-splash-screen` sits in front when present). Navigating to `mainMenu` uses `popUpTo(0) { inclusive = true }`, so Back exits the app; session loss does the same towards `landing`.
- *Alternative: separate `MainMenuActivity`.* Matches the wording of the request but goes against the project's single-activity convention, duplicates theme/edge-to-edge setup and complicates shared session state. Behavior the player sees is identical (landing never reachable via Back).

### 4. `SessionManager` as the single source of truth
An app-scoped `SessionManager` exposes `StateFlow<SessionState>` with `Unknown`, `SignedOut(reason?)`, `SignedIn(profile)`. It owns:
- `restore()` at process start: `credentialsManager.hasValidCredentials()` (true when the access token is valid or a refresh token exists) **and** a cached profile → `SignedIn(cached)`, then refreshes in the background; credentials without a cached profile → stays `Unknown` while it identifies over the network (rare: crash between login and caching); otherwise `SignedOut`.
- `signIn(activity)`: web auth → save credentials → identify (decision 6) → cache → `SignedIn`.
- `signOut(activity)`: `WebAuthProvider.logout()` best-effort, then always clear credentials and profile → `SignedOut`.
- `onSessionLost()`: called by the HTTP layer; clears local state → `SignedOut(Expired)`.

`MainActivity` keeps the system splash on screen (`setKeepOnScreenCondition`) while state is `Unknown`; the local check takes milliseconds. A root composable maps state to the start destination and reacts to transitions. ViewModels (`LandingViewModel`, `MainMenuViewModel`) wrap the session manager for UI state (busy, error).

### 5. HTTP: OkHttp + Retrofit + kotlinx.serialization
- `AuthInterceptor` gets the access token from `SecureCredentialsManager` (synchronous `awaitCredentials()` on the OkHttp thread) and adds `Authorization: Bearer`.
- `TokenAuthenticator` handles `401`: one forced renewal (`forceRefresh = true`) and retry; if the response is still `401` or renewal fails with a non-network error, it calls `SessionManager.onSessionLost()`.
- Renewal errors are classified: network failures (`AuthenticationException.isNetworkError`) keep the session and surface as connection errors; invalid/revoked refresh tokens end the session. This is the Android equivalent of the old client's zombie-session guard.
- *Alternative: Ktor client.* Fine too, but Retrofit+OkHttp has first-class interceptor/authenticator hooks that map directly onto this flow. Serialization uses kotlinx.serialization (Kotlin-native, no reflection).

### 6. Identification flow
After login, build `CreateUserRequest` from the ID token (`credentials.user`: `email`, `isEmailVerified`, `givenName`, `familyName`) plus language (`"nl"` when the device locale is Dutch, else `"en"`), and call `POST /users`. Both 201 and 200 return `UserDto`, which becomes the profile — no extra `GET /users/me` round trip. Missing email → abort login with a specific error. On restore, the background refresh calls `GET /users/me`; on 404 it falls back to `POST /users`.

### 7. Profile cache in DataStore
The `UserDto` is stored as JSON in a Preferences DataStore in app-private storage, excluded from backups. It holds no secrets (tokens stay in the credentials manager) but contains the email address, so it is cleared on every sign-out/session loss.
- *Alternative: decode the ID token on each launch.* Gives name/email but not the backend `userId`/`callsign` that the game screens need.

### 8. Manual dependency wiring
A small `AppContainer` created in a new `ThePreyApplication` builds the credentials manager, OkHttp/Retrofit, `UserApi`, profile store and `SessionManager`. ViewModels get dependencies through a `ViewModelProvider.Factory`.
- *Alternative: Hilt.* Worth it when the object graph grows; adding the KSP/Hilt toolchain for five objects now is premature. Interfaces keep a later migration mechanical.

### 9. Testing
JVM unit tests (JUnit 4 + kotlinx-coroutines-test) with hand-written fakes for `CredentialsStore`, `UserApi`, `ProfileStore` and `WebAuthClient` interfaces that wrap the SDK. Covered: restore routing matrix, sign-in success/cancel/error/no-email/no-refresh-token, identification 200/201/404, offline restore, session loss vs. network error classification, sign-out offline. The SDK wrappers themselves are thin and verified manually on a device.

## Risks / Trade-offs

- [Auth0 API lacks "Allow Offline Access" → no refresh token, session dies after access-token expiry] → Login rejects sessions without a refresh token with a logged config error; the tenant checklist in tasks includes the setting.
- [App Links fingerprints not registered (debug keys, Play App Signing key) → redirect opens the browser instead of the app] → Register debug, upload and Play App Signing SHA-256 fingerprints; `auth0Scheme` Gradle property allows a custom-scheme debug build.
- [Keystore keys invalidated (device security change, some OEM bugs) → stored credentials undecryptable] → `SecureCredentialsManager` reports a crypto error; treat it as session lost and show the landing page (one extra sign-in, no crash).
- [Social connections without email → `POST /users` cannot register] → Explicit error on the landing page; Auth0 connections should require email.
- [Restored session with a cached profile shows the main menu even if the account was deleted server-side] → Background refresh / first API call returns 401 or 404 and corrects the state within seconds.
- [Blocking token fetch inside the OkHttp interceptor] → Runs on OkHttp's dispatcher threads, never the main thread; the SDK serializes renewals.

## Migration Plan

1. Auth0 tenant: create (or reuse) a Native application for `nl.hexmaster.theprey`; set Allowed Callback and Logout URLs to `https://theprey.eu.auth0.com/android/nl.hexmaster.theprey/callback`; enable Refresh Token grant with rotation; register SHA-256 fingerprints under Device Settings; enable "Allow Offline Access" on the `https://api.theprey.nl` API.
2. Ship the app update. Existing installs of the previously published app id have no compatible stored session, so every player signs in once.
3. Rollback: publish the previous build with a higher `versionCode`; no server state depends on this change.

## Open Questions

- When `api.theprey.nl` is bound to the gateway, switch the `apiBaseUrl` default to it (DNS for it does not exist yet).
