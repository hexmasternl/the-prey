# Proposal

## Why

The main menu's Settings button opens a placeholder. Players have no way to change their callsign (the handle other players see), to pick the app language independently of the phone, or to grant location access after they declined it. The last one matters most: the main menu should ask for location only once, so a player who said no needs a place to change their mind, and that place must show the same prominent disclosure Google Play requires before any location permission request.

## What Changes

- Replace the Settings placeholder with a real **Settings screen**, opened from the main menu, with three sections:
  - **Callsign**: shows the current callsign, lets the player edit it, validates it as the server does (3–30 characters: letters, digits, spaces, `-`, `_`, `&`, `$`, `#`, `@`) and saves it with `PUT /users/settings`. On success the cached profile is updated, so the main menu shows the new callsign right away. Server validation errors and network errors are shown in the section.
  - **Language**: choice between *System default*, *English* and *Nederlands*. The choice applies to the app immediately (per-app language, no restart needed by the player), survives restarts, and is also sent to the server as the player's preferred language.
  - **Location**: shows the current location access (off, approximate only, while using the app, all the time) and the notification permission, an always-visible **warning panel** with the location tracking disclosure (precise location, background/screen-off collection during an active game through a foreground service with a persistent notification, when it stops, why it is needed, link to the privacy page), and a **Grant location access** action. The action requests the runtime permission when Android still allows a prompt, and opens the app's system settings page when it does not (permanently denied, or "Allow all the time" which Android only grants there). The section refreshes when the player returns from system settings.
- **Main menu asks only once**: the location disclosure and permission prompt appear automatically on the main menu only the first time on a device. After that, whatever the player chose, the main menu never shows them again by itself. Missing permission is then fixed from Settings (or from the play action's error, which already links to system settings). This replaces the "show again on every start while permission is missing" rule of the in-flight `app-location-foreground-service-warning` change.
- New English and Dutch strings for everything on the screen.

## Capabilities

### New Capabilities
- `app-settings`: the player's Settings screen in the Android app: editing the callsign, choosing the app language, and reviewing and granting location access behind the prominent location disclosure.

### Modified Capabilities
<!-- None in openspec/specs. The related client rules live in in-flight changes that are not archived yet:
     - app-location-foreground-service-warning (location-tracking-consent): its "Disclosure is shown again while permission is missing" requirement is narrowed to "the main menu asks once"; the tasks update that change's spec.
     - app-main-menu: its "Location permission is requested from the main menu" requirement gets the same once-only wording.
     Server behavior (PUT /users/settings) is unchanged. -->

## Impact

- **Android app (`app/src`)**: new `ui/settings/` (screen, view model, location section), `UserApi.updateSettings` (`PUT /users/settings`) with `UpdateUserSettingsRequest`, a session/profile method that saves the new profile in `ProfileStore`, a device-local store for "main menu prompt already shown", the `settings` route in `ThePreyNavHost` instead of the placeholder, and the main menu's consent trigger limited to the first time.
- **Per-app language**: new `androidx.appcompat` dependency, `MainActivity` becomes an `AppCompatActivity`, a locale config (`generateLocaleConfig`) and the `AppLocalesMetadataHolderService` entry in the manifest so the choice persists on Android 12 and lower. On Android 13+ the same choice also shows up in the system's per-app language setting.
- **Strings**: new keys in `values/strings.xml` and `values-nl/strings.xml` (`the-prey-multilingual`). The disclosure wording is shared with the consent dialog, so the dialog, the settings panel, the privacy page and the Play Console declaration say the same thing.
- **Backend**: none. `PUT /users/settings` already exists and validates callsign and language.
- **Other in-flight changes**: `app-location-foreground-service-warning` and `app-main-menu` specs get their "ask again" rule replaced by "ask once on the main menu"; this change depends on the consent change's disclosure text and permission flow.
