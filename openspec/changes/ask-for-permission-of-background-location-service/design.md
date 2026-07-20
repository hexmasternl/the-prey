## Context

Both clients already run a background location service that broadcasts the device position for the full duration of a game, and both request the OS location permission implicitly at tracking start:

- **MAUI** — `AndroidBackgroundExecutionHost.EnsurePermissionsAsync()` calls `Permissions.RequestAsync<Permissions.LocationWhenInUse>()` then `<Permissions.LocationAlways>()` (and `POST_NOTIFICATIONS` on API 33+) the moment `StartAsync` runs. iOS declares usage strings in `Info.plist`. There is no in-app explanation shown first.
- **Ionic** — `GameLocationService.start()` calls `BackgroundGeolocation.addWatcher({ requestPermissions: true, ... })`, which makes the plugin request the OS permission directly. Callers are `game-lobby.page.ts`, `game-hunter.page.ts`, and the prey page.

Google Play's **Prominent Disclosure & Consent** policy requires that, before the runtime permission request, the app show a prominent disclosure describing background location collection and its purpose, gated by an affirmative user action. Neither app does this today. This change inserts a consent gate ahead of the existing permission requests; it does not change how location is captured or reported.

**Product direction (this revision):** rather than gating at game start, the disclosure is a one-time hard gate at app entry, before the main menu — accept to use the app (remembered forever), decline to close it (or, where the platform forbids self-termination, to hit a blocking consent wall). It still precedes the OS permission prompt (raised later at game start), so it stays policy-compliant.

## Goals / Non-Goals

**Goals:**
- Show a prominent, purpose-explaining disclosure once at app entry, before the main menu, in both clients, with identical intent.
- Require explicit acceptance to enter the app; remember it permanently so it is never shown again.
- Decline blocks entry: close the app where the platform allows it, otherwise a full-screen non-dismissable consent wall.
- Localize (EN + NL) and style the disclosure and wall through each app's single source of truth.

**Non-Goals:**
- No change to location capture, cadence, reporting endpoints, the foreground-service/background behaviour, or the OS permission request at game start (which is unchanged).
- No backend, API, or realtime-protocol changes.
- Not a full privacy-policy or settings-screen redesign; only the entry disclosure gate.
- No change to the head-start / penalty flows.

## Decisions

### Decision (SUPERSEDED): Insert a consent gate immediately before the OS permission request
The original design gated the disclosure at game start, immediately before the OS prompt. This was implemented and then superseded (see below) at the product owner's direction. The game-start gate has been reverted; the OS location-permission request at game start is unchanged from its original form.

### Decision: A one-time hard disclosure gate at app entry, before the main menu
The disclosure is shown once at app entry, before the main menu (home) is reached, and is remembered permanently after acceptance. It still precedes any OS location-permission prompt (that prompt happens later, at game start), so it remains policy-compliant, while being a single, unmissable consent rather than a per-game prompt.

- **Why (product direction)**: The player must make an informed, blocking choice before using the app at all; consent should be captured once and never nag again.
- **MAUI**: gate in `WelcomeViewModel.BootstrapAsync()` after the session is established and before the post-boot navigation (before both `IInviteDeepLinkHandler.ReplayPendingAsync()` and `Shell.Current.GoToAsync("home")`), so both the deep-link and menu paths are covered.
- **Ionic**: a functional `canActivate` guard on the `home` route (and the gameplay entry routes, made a no-op by the persisted flag, to close the deep-link bypass), since there is no distinct welcome page — entry is `login` → `home`.
- **Alternative considered**: keep the game-start gate as well. Rejected — once consent is captured and remembered at entry, the game-start gate never fires; it would be dead code.

### Decision: Decline behaviour is platform-specific
On Android and desktop the app closes on decline (`Process.KillProcess`/platform quit in MAUI; `App.exitApp()` in Ionic). On iOS and the Ionic web/PWA build — where self-termination is disallowed (App Store rejection) or impossible (a browser tab cannot self-close) — a full-screen, non-dismissable consent wall is shown instead, offering only Accept; accepting records consent and continues.

- **Why**: Honours "the app closes on decline" where the platform permits it, without shipping an App-Store-rejectable `exit(0)` on iOS or an impossible tab-close on web.
- **Testability**: the disclosure/exit/wall behaviour lives behind an injectable seam (MAUI `ILocationConsentGate.EnsureConsentAsync`, which returns only when consent is granted; Ionic the guard + a consent-wall route), so the `WelcomeViewModel` / guard logic stays unit-testable and the platform-coupled parts stay excluded from the test build.

### Decision: Persisted consent is a single boolean flag
Persist a "consent accepted" flag per install (MAUI `Preferences`, Ionic `@capacitor/preferences`). The gate shows the disclosure only when the flag is unset; once accepted the flag is permanent and the disclosure never shows again. The OS-permission state is deliberately NOT consulted by the entry gate — the OS handles its own (re-)prompting at game start; the entry disclosure is a one-time informational consent, not a permission tracker.

- **Why**: Matches "never shown again" and keeps the gate a simple, single source of truth.

## Risks / Trade-offs

- **[Two independent implementations can drift]** → A single shared capability spec (`background-location-consent`) defines identical required behaviour for both clients; tasks implement to the same scenarios.
- **[iOS/web cannot self-terminate]** → Decline routes to a full-screen non-dismissable consent wall on those platforms instead of an `exit(0)` (App Store rejection) or an impossible tab close.
- **[Deep-link bypass of the menu]** → MAUI gates before the post-boot branch (covers the invite-link path too); Ionic applies the guard to the gameplay routes as well as `home`, made a no-op by the persisted flag.
- **[Gate must not block on a slow disclosure at cold start]** → The gate runs after session establishment on the welcome/bootstrap path and is a fast local check once accepted; the disclosure only appears on first run.
- **[Localization completeness]** → Add EN and NL copy (disclosure + wall accept) in the same change for both apps; both apps switch language at runtime, so a missing key would be immediately visible.

## Open Questions

- None outstanding. Decline behaviour (Android/desktop close; iOS/web blocking wall) confirmed by the product owner.
