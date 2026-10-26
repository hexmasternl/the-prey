# Design

## Context

- The app is a single-activity Compose app. `ThePreyNavHost` switches between `landing` and `main_menu` based on `SessionController.state`, so "after sign-in" means the moment the state becomes `SessionState.SignedIn`. That happens after an interactive login and also when a stored session is restored at startup.
- Location is currently read only by the main menu position readout: `MainMenuViewModel.onLocationPermissionResult(granted)` gates a `LocationSource` flow. Nothing in the app asks for the permission yet. The manifest declares `ACCESS_FINE_LOCATION` and `ACCESS_COARSE_LOCATION`.
- Device-local state uses Preferences DataStore (`profileDataStore`). Backups exclude everything (`backup_rules.xml`, `data_extraction_rules.xml`), so a new DataStore file is device-local without extra rules.
- The foreground tracking service does not exist yet. This change only adds the disclosure and the permission flow it depends on.

## Goals / Non-Goals

**Goals:**
- A prominent disclosure that meets Google Play's requirements, shown before any runtime location prompt.
- One reusable consent flow that the main menu uses now and the start/join game actions use later.
- Testable decision logic (show or not, what to request next) kept out of Compose.

**Non-Goals:**
- Building the foreground service, its notification channel or its manifest `<service>` declaration.
- Deciding when the service starts and whether it needs `ACCESS_BACKGROUND_LOCATION`. Both belong to the service change (see Decision 2).
- Server-side storage of consent. The consent is about this device's permissions, not about the account.

## Decisions

### 1. A modal dialog over the main menu, not a separate route

The disclosure is a non-dismissible Material 3 `Dialog` (`dismissOnBackPress = false`, `dismissOnClickOutside = false`) shown by a `LocationConsentHost` composable that wraps the signed-in content in `ThePreyNavHost`. It is styled per the design system (panel surface, Special Elite heading, PT Mono body, primary + secondary buttons from `ui/components/Buttons.kt`).

*Alternative:* a dedicated `location_consent` route between landing and main menu. Rejected because the same flow has to appear again from game actions later, and a dialog host can be triggered from anywhere without changing the session-driven navigation.

### 2. The disclosure covers background tracking; this flow asks for the while-in-use permission

The tracking service (built in a separate change) has to keep sending location for the running game while the app is in the background, the screen is off, or the app has been swiped away from recents. A foreground service declared with `foregroundServiceType="location"` does that, and the disclosure therefore says plainly that location is collected in the background. Play's prominent-disclosure rule applies to it.

This flow requests `ACCESS_FINE_LOCATION` + `ACCESS_COARSE_LOCATION` and, on API 33+, `POST_NOTIFICATIONS`. Those are enough for a location foreground service that is *started while the app is visible* to keep tracking after the app goes to the background. Whether the service can always be started from the foreground (for example on lobby join) or also needs `ACCESS_BACKGROUND_LOCATION` (to start at game start while the app is already backgrounded) is decided in the service change. If that change needs background location, it adds a second step to this flow. The disclosure dialog stays the same and the existing requirements still apply. On Android 11+ that second step sends the player to the system settings page to pick "Allow all the time", and Play requires a background-location declaration.

### 3. Consent state = stored flags + live permission check

A `LocationConsentStore` (Preferences DataStore file `location_consent`) stores two booleans: `disclosureAccepted`, and `menuPromptShown` (added by `app-settings-screen`). The main menu shows the disclosure by itself only when:

```
showAutomatically = signedIn && onMainMenu && !menuPromptShown && !locationPermissionGranted
```

`menuPromptShown` is set as soon as the dialog is shown automatically, whatever the player picks, so the main menu asks once per device (spec: *Main menu shows the disclosure automatically only once*). The explicit paths ignore the flag and always show the disclosure before a request: the `requireLocation` gate for start/join actions, and the Settings screen's Grant location access action (where the disclosure is an always-visible panel above the button).

`disclosureAccepted` is used only to tell a first-time "Continue" apart from the case where the system will no longer prompt (`shouldShowRequestPermissionRationale == false` after a denial). In that case the dialog shows the "location is off, open settings" variant. Permission is still checked on every `ON_RESUME`, so a grant or revocation in settings updates the consent state, but a revocation does not bring the dialog back by itself.

*Alternative:* show the disclosure again on every start while permission is missing. Rejected by `app-settings-screen`: a player who said no would be asked over and over. Settings is the place to change their mind, and it shows the same disclosure before any request, which is what Play requires.

### 4. Logic in a plain view model, Android calls at the edge

`LocationConsentViewModel` exposes a `ConsentUiState` (`Hidden`, `Disclosure`, `OpenSettings`) and takes events (`onPermissionChecked(granted, canAsk)`, `onContinue()`, `onNotNow()`, `onPermissionResult(...)`). It emits one-shot effects (`RequestLocation`, `RequestNotifications`, `OpenAppSettings`). The composable does the `rememberLauncherForActivityResult(RequestMultiplePermissions())` calls and the `ContextCompat.checkSelfPermission` checks. This keeps the decision table unit-testable with JUnit, like the existing view models, with no instrumentation.

"Not now" hides the dialog (an in-memory flag in the view model). Because `menuPromptShown` is already set, the main menu does not bring it back on the next start; it comes back only before a game action or from the Settings grant action.

### 5. Main menu readout follows the consent result

`MainMenuScreen` no longer checks permission on its own. It reads the granted state from the consent host (passed down) and calls `onLocationPermissionResult` with it, so the readout starts as soon as the player grants the permission and never triggers a prompt.

### 6. Game gate API for later

The host exposes `requireLocation(onGranted: () -> Unit)` through a `CompositionLocal`. Start/join buttons wrap their action in it: if permission is granted the action runs right away, otherwise the disclosure is shown and the action runs after a grant. Nothing calls it yet. The tasks add it with a unit test so the game screens can use it as they are built.

## Risks / Trade-offs

- [Play reviewers judge the disclosure wording] → The text follows Google's template elements (data type, purpose, "even when the app is closed or not in use"). The same text goes into the privacy page and the Play Console declaration so all three match.
- [Some OEMs kill foreground services or delay location] → Out of scope here. The disclosure promises nothing about accuracy.
- [Coarse-only grant: on Android 12+ the player can choose "approximate"] → Treated as not granted for gameplay. The settings variant explains that precise location is needed. The main menu readout still works with coarse.
- [A player who refuses never sees the automatic dialog again] → Intended (`app-settings-screen`): the play action shows a missing-permission error with a settings link, and the Settings screen offers the disclosure and a grant action.

## Migration Plan

App-only change, shipped in a normal release. Existing installs have no `location_consent` file, so every current player sees the disclosure once after updating, which is what the policy wants. Rollback = previous app version; the extra DataStore file is harmless.
