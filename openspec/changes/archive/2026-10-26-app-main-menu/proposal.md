# Proposal

## Why

Signed-in players currently land on a placeholder main menu that only greets them and offers "Sign out". The main menu is the hub every later feature hangs off (playing, managing playfields, settings), and it is the first screen a returning player sees on every launch, so it needs to carry the game's tactical identity and give players a clear choice of what to do next.

## What Changes

- Replace the placeholder main menu with a full-screen **tactical main menu**:
  - A dark background with an **animated tactical map** in dark-green tints (grid, contour rings, a slow radar-style sweep, faint waypoint markers), drawn in Compose and decorative only.
  - A **position readout** in the top-right corner: `-- ° N // -- ° E // NO SIGNAL` until the device location is known, then the live latitude/longitude with hemisphere letters (N/S, E/W). Asks for location permission when needed; falls back to the "no signal" text when denied or unavailable.
  - A dim line **"OPERATIONAL FIELD MANUAL vX.Y.Z"** below it, where `X.Y.Z` is the app's real `versionName`.
  - The slogan **"STAY HIDDEN. / HUNT SMART."** on two lines, first word in the text colour and second word in Signal green, followed by the short description "A GPS-based, real-world hide-and-seek game. Enter the playfield. Don't get caught."
  - A stack of buttons: **Play now** (primary), **Playfields**, **Settings**, **Sign out** and **Quit** (secondary).
  - The player's **callsign** (or display name) at the bottom in the text colour, with a **running-light** sweep in Signal green across the letters, repeated every few seconds.
- **Play now**, **Playfields** and **Settings** navigate to new destinations that are placeholders in this change; their real screens are separate changes.
- **Quit** closes the app (finishes the activity and removes the task); it does not sign the player out.
- **Sign out** keeps its existing behaviour (moved into the button stack, as in the design mockup).
- All animations stop under the system's reduced-motion setting; the screen remains fully readable without them.
- All new texts in `strings.xml` with Dutch translations. Only the coordinate notation itself (`°`, `N/S/E/W`, `//`) is fixed telemetry and not translated.
- Adds the `ACCESS_COARSE_LOCATION` / `ACCESS_FINE_LOCATION` permissions and the Google Play services location dependency.

## Capabilities

### New Capabilities
- `app-main-menu`: the signed-in player's hub screen — its layout and content, the live position readout and version line, the menu actions and where they lead, the player name display, and its animation and reduced-motion behaviour.

### Modified Capabilities
<!-- None in openspec/specs. The in-flight app-landing-page change defines "Main menu greets the identified player" (callsign + Sign out); this change keeps both and refines them in the app-main-menu spec. app-version-gate gates the home screen's buttons; this change does not alter that requirement and wires no gate itself (see design). -->

## Impact

- **Android app** (`app/src`): `ui/mainmenu/` (screen rewrite, view model, new tactical-map background and running-light name composables), new location provider under `data/location/`, `ui/navigation/ThePreyNavHost.kt` (new routes), `AppContainer.kt`, `AndroidManifest.xml` (location permissions), `strings.xml` (en + nl).
- **New dependency:** `com.google.android.gms:play-services-location` (fused location provider).
- **Backend:** none.
- **Privacy:** location is read only while the main menu is visible, shown on screen and never stored or sent anywhere by this change.
