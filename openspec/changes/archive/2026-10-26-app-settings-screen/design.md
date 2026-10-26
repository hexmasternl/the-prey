# Design

## Context

- `Routes.SETTINGS` in `ThePreyNavHost` shows `PlaceholderScreen`. The main menu already navigates there.
- The signed-in profile lives in `SessionState.SignedIn(profile: UserDto)`, owned by `SessionManager` and cached in `ProfileStore` (DataStore, excluded from backups). `UserDto` already carries `callsign` and `preferredLanguage`. The main menu shows the callsign, falling back to the display name.
- The server already has `PUT /users/settings` with `UpdateUserSettingsRequest(Callsign, PreferredLanguage)`. It returns the updated `UserDto`, a 400 validation problem (`errors: { callsign | preferredLanguage: [message] }`) for an invalid callsign or a language other than `en`/`nl`, and 404 for an unknown user. Callsign rule: `^[a-zA-Z0-9 \-_&$#@]{3,30}$`. Both fields are required, so a language update has to send a valid callsign too.
- `SessionManager` sends a language at sign-up derived from `Locale.getDefault()`. There is no in-app language choice and no `androidx.appcompat` dependency. `MainActivity` is a `ComponentActivity`; minSdk is 26.
- Location permission: today `MainMenuScreen` asks on its own the first time it is shown per process. The in-flight `app-location-foreground-service-warning` change moves that into a `LocationConsentHost` + `LocationConsentViewModel` with the disclosure dialog, a `LocationConsentStore` (`disclosureAccepted`), and a "show again while permission is missing" rule. The in-flight `app-play-now-button-behavior` change adds `ACCESS_BACKGROUND_LOCATION`, a `LocationAccess { Missing, ForegroundOnly, Granted }` reading, and an "Open settings" intent.

## Goals / Non-Goals

**Goals:**
- One place where a player who declined can grant location later, always behind the disclosure.
- Reuse the consent change's disclosure text, permission reading and settings intent instead of a second copy.
- Callsign and language logic in a plain view model, unit-tested like the existing ones.

**Non-Goals:**
- Changing the server. The settings endpoint is used as is.
- Other settings (units, sound, notifications per type, account deletion, sign out). Sign out stays on the main menu.
- A server-side record of consent. Consent stays device-local, as decided in the consent change.

## Decisions

### 1. Settings is a regular route with its own view model

`ui/settings/SettingsScreen.kt` + `SettingsViewModel` replace the placeholder on `Routes.SETTINGS`. The view model takes `SessionController` (for the current profile) and a new `ProfileSettings` port:

```kotlin
interface ProfileSettings {
    /** PUT /users/settings; on success stores and publishes the returned profile. */
    suspend fun updateSettings(callsign: String, language: String): SettingsResult
}
sealed interface SettingsResult { data class Saved(val profile: UserDto); data class Invalid(val message: String); data object Failed }
```

`SessionManager` implements it: it calls `UserApi.updateSettings`, saves the result in `ProfileStore` and sets `state = SignedIn(profile)`. Because every screen reads the name from the session state, the main menu shows the new callsign without extra wiring. 400 maps to `Invalid` with the first message from the problem's `errors`; any other status or `IOException` to `Failed`; cancellation is rethrown.

*Alternative:* a separate `UserRepository` that only writes `ProfileStore`. Rejected: the session state would keep the old profile until the next restore, so the main menu would show the old callsign.

### 2. Callsign validation mirrors the server, trimming first

A pure `validateCallsign(input): CallsignCheck` (`Valid(trimmed) | Invalid | Unchanged`) uses the same pattern as `User.CallsignPattern`. The client trims leading/trailing spaces before checking and sending, so "  Owl  " doesn't fail on the server for a reason the player can't see. Server validation still wins: an `Invalid` result shows the server message. The rule text is a translated string, not the server message, for the client-side case.

### 3. Per-app language through AppCompat

`AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags("nl"))` (empty list = System default) is the one API that works from minSdk 26 up:

- On Android 13+ it delegates to the platform `LocaleManager`; the choice also appears in the system's per-app language screen, and the system recreates the activity.
- On Android 12 and lower AppCompat applies it to `AppCompatActivity` instances and persists it when the manifest has the `AppLocalesMetadataHolderService` with `autoStoreLocales=true`.

Needed: `androidx.appcompat` in the version catalog, `MainActivity : AppCompatActivity`, the service entry in the manifest, and `androidResources { generateLocaleConfig = true }` with `res/resources.properties` (`unqualifiedResLocale=en`) so the locale config lists exactly `en` and `nl`. `AppCompatActivity` needs an AppCompat theme after the splash: `postSplashScreenTheme` gets a `Theme.AppCompat.DayNight.NoActionBar` parent; Compose draws everything, so the theme only affects window colors, which keep the current values.

The current choice is read with `AppCompatDelegate.getApplicationLocales()`, so there is no separate store to keep in sync. The language sent to the server is the effective one: the chosen tag, or for System default the device language mapped to `nl`/`en` with the same rule `AppContainer` already uses (extracted to one function).

*Alternatives:* (a) wrapping `Context` with a custom locale in `attachBaseContext` — fragile with Compose `LocalConfiguration` and doesn't show up in Android 13 system settings; (b) only `LocaleManager` — Android 13+ only.

### 4. Language sync is best-effort, reconciled on the next save

The language switch never waits for the network. After a change the view model calls `updateSettings(currentCallsign, language)` if the stored callsign is valid. On `Failed` it shows a small notice ("Saved on this phone, will sync later") and nothing else. Every later callsign save sends the effective language, and on Settings open the view model compares the effective language with `profile.preferredLanguage` and syncs if they differ. That covers offline changes and changes made in Android 13's system screen without a background job.

Activity recreation on a language change destroys the composition but not the `ViewModel` (it is scoped to the nav back-stack entry), so the in-flight sync survives.

### 5. Location section reuses the consent flow

The Location section uses the consent change's pieces rather than its own:

- **Reading:** a shared `readLocationAccess(context): LocationAccess` (`Missing`, `ApproximateOnly`, `ForegroundOnly`, `Granted`) and `notificationsAllowed(context)`. This extends the play-now change's enum with `ApproximateOnly` so the status can say why coarse isn't enough; the play action still treats `ApproximateOnly` as missing. Re-read on `ON_RESUME` of the back-stack entry, as the main menu does.
- **Disclosure text:** one `LocationDisclosure` composable (string resources shared with the dialog) rendered in two containers: inside the consent dialog and as an always-visible warning panel here. The panel uses the design system's Hunter/warning tint with a warning icon, panel surface, PT Mono body. The texts cannot drift apart because they are the same resources.
- **Grant action:** a pure `grantStep(access, canAskFine): GrantStep` decides `RequestPrecise`, `OpenSettingsForPrecise`, `OpenSettingsForAllTheTime` or `None`. `canAskFine` comes from `shouldShowRequestPermissionRationale` combined with the consent store's `disclosureAccepted` flag (first request vs. permanently denied, as in the consent change's Decision 3). `RequestPrecise` launches `RequestMultiplePermissions(fine + coarse)` and then `POST_NOTIFICATIONS` on API 33+. Before launching, it records `disclosureAccepted = true`, since the panel above the button is the disclosure. `OpenSettings*` shows a one-line instruction and fires `ACTION_APPLICATION_DETAILS_SETTINGS`.

The background step never uses a runtime request, matching the play-now change (Decision 3 there).

### 6. Main menu prompts once per device

The consent store gets a second flag, `menuPromptShown`. The `LocationConsentHost` shows the disclosure automatically on the main menu only when `!menuPromptShown && !locationGranted`, and sets the flag as soon as it shows the dialog, whatever the player picks. After that the automatic trigger is off for good on this device (cleared only by clearing app data or reinstalling). The explicit paths still show the disclosure before any prompt: the Settings grant action, and the consent change's `requireLocation` gate for start/join. This narrows the consent change's "Disclosure is shown again while permission is missing" requirement; task 1 rewrites that requirement (and the main menu's "Location permission is requested from the main menu") so the specs agree before either change is archived.

If the consent change has not landed when this is implemented, the same flag gates the main menu's current self-ask in `MainMenuScreen` (replacing the per-process `rememberSaveable` flag), and the Settings section includes the disclosure strings itself; the consent change then reuses them.

## Risks / Trade-offs

- [Switching `MainActivity` to `AppCompatActivity` + AppCompat theme could change window/status-bar colors or the splash hand-off] → Keep `enableEdgeToEdge()`, set the post-splash theme's window background to the current color, and check splash → landing → main menu on API 26, 31 and 35 emulators.
- [Activity recreation on language change resets non-saveable UI state] → Settings state lives in the view model; the callsign field draft uses `rememberSaveable` / view-model state, so a half-typed callsign survives.
- [Player never sees the automatic prompt again after Not now] → Intended by the user's requirement. The play action shows a missing-permission error with a link, and Settings is one tap from the menu, so the path to grant stays visible.
- [Play policy: a permission request outside the first-run flow must still follow the disclosure] → The grant action is part of the same visible panel and is never offered without it (spec: *No location prompt without the disclosure*).
- [Callsign rule changes on the server] → The server message is shown on 400, so a stricter server still produces a clear error; the client rule is a convenience copy, noted next to the regex.
- [Language synced while a callsign is invalid or empty] → Not sent (server requires a valid callsign); applied locally and synced on the first callsign save.

## Migration Plan

App-only release; no server change. Existing installs: `menuPromptShown` is missing, so the main menu shows the disclosure once after the update when permission is missing, then never automatically. AppCompat's locale store starts empty, which means System default, the current behavior. Rollback: the previous version ignores the extra DataStore key and the stored app locale (on Android 13+ the system keeps the per-app language, which the old build also honors through resources).
