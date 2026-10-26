# Proposal

## Why

The native Android app currently opens on a static "Standing by" welcome screen and has no notion of who the player is. Every game action on the backend is tied to the caller's Auth0 `sub` claim, so before any lobby, playfield or game screen can be built the app must sign players in safely, keep them signed in across launches, and know which player record belongs to them. Doing it now, while the launch flow is still small, means every later screen can assume an identified player.

## What Changes

- Replace the welcome screen with a **landing page**: the Prey mark, title and tagline plus a single "Sign in" call to action, styled with the design system.
- Sign in through **Auth0 Universal Login** (tenant `theprey.eu.auth0.com`, audience `https://api.theprey.nl`) opened in the system browser / Custom Tab, using Authorization Code + PKCE. The app never shows its own password field and never embeds a WebView.
- Request `offline_access` so the app receives a refresh token, and store credentials **encrypted with the Android Keystore**. Tokens are never written to logs, plain preferences or backups.
- After a successful sign-in, **identify the player** against the Users API: register/refresh the player record (`POST /users`, idempotent) and load the profile (`GET /users/me`). The profile is cached locally.
- Route signed-in players to a new **main menu** destination. In this change the main menu is a minimal screen that greets the identified player (callsign or display name) and offers **Sign out**; game features are added later.
- **Session restore on launch:** when the app starts and valid (or refreshable) stored credentials exist, the app skips the landing page and opens the main menu directly, using the cached profile immediately and refreshing it in the background. Expired-but-refreshable access tokens are renewed silently; revoked or unrecoverable sessions are cleared and the player lands on the landing page.
- **Sign out** clears local credentials and the cached profile and ends the Auth0 browser session, returning to the landing page.
- An authenticated HTTP client attaches a fresh bearer token to every API call and treats an unrecoverable token failure as "signed out" rather than as a network error.
- All new texts in `strings.xml` with Dutch translations.

## Capabilities

### New Capabilities
- `app-authentication`: how the Android app signs a player in, stores and renews their session securely, identifies them against the Users API, restores the session on later launches, and signs them out.
- `app-landing-page`: the first interactive screen a signed-out player sees, and the launch routing that decides between the landing page and the main menu.

### Modified Capabilities
<!-- None. app-version-gate describes the gating of home-screen actions on the version check; this change does not alter that requirement. The in-flight app-splash-screen change hands off to "the home screen", which after this change is whichever destination launch routing selects. -->

## Impact

- **Android app** (`app/src`): `MainActivity.kt`, new `auth/`, `data/` (API client, user repository), `ui/landing/` and `ui/mainmenu/` packages, navigation between destinations; `WelcomeScreen.kt` is removed; `AndroidManifest.xml` (Auth0 redirect activity placeholders); `app/build.gradle.kts` (Auth0 domain, client id, scheme, API base URL as build config); `strings.xml` (en + nl).
- **New dependencies:** Auth0.Android SDK, Navigation Compose, Lifecycle ViewModel Compose, an HTTP client (OkHttp + Retrofit with kotlinx.serialization), the Kotlin serialization plugin, DataStore (profile cache) and kotlinx-coroutines-test for unit tests.
- **Auth0 tenant configuration (manual, outside the repo):** a Native application for Android with the callback/logout URLs for `nl.hexmaster.theprey`, the Refresh Token grant enabled with rotation, "Allow Offline Access" enabled on the `https://api.theprey.nl` API, and the app's SHA-256 signing fingerprints (debug, upload and Play App Signing) registered for Android App Links.
- **Backend:** no code changes; uses the existing `POST /users` and `GET /users/me` endpoints and JWT validation in ServiceDefaults.
- **Interaction with `app-splash-screen`:** the launch sequence remains network-free; the session check it hands off to reads local, encrypted storage only.
