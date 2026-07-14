## Why

After boot the MAUI app currently drops the player onto a bare `HomePage` stub ("NO ACTIVE OPERATION — Coming soon") and routes authenticated/active/unauthenticated states to three different pages. Players need a single, on-brand command hub — the main menu — that greets them on launch, reflects whether they are signed in and whether an operation is already running, and gives them one place to log in, resume or start a game, manage playfields, change settings, log out, or quit.

## What Changes

- Replace the `HomePage` stub with a full **main menu** styled in The Prey tactical aesthetic: an animated tactical-elevation **map background that slowly pans** (the map raster is larger than the viewport), the **"The Prey"** app title, a HUD header, a hero slogan, and a vertical roster of action buttons.
- Add a **HUD telemetry readout** in the top-right corner (small, dim, PT Mono): a live **GPS position** line — `xxx° N // xxx° E` from the device location — and beneath it an **`OPERATIONAL FIELD MANUAL — V XXX`** line where `XXX` is the actual app version.
- Add a left-aligned, vertically-centered **hero slogan** on two lines — **STAY** (regular text) **HIDDEN** (signal green) / **HUNT** (regular text) **SMART** (signal green) — with a dim PT Mono **tagline** beneath it: "A GPS-based, real-world hide-and-seek game. Enter the playfield. Don't get caught."
- **Bottom-align** the action-button roster.
- Add the button roster with distinct tactical styling:
  - **Log In** — filled signal-green
  - **Resume Game** — filled hunter-red
  - **Start Game** — filled signal-green
  - **Playfields**, **Settings**, **Log Out**, **Exit** — outline (void base + signal-green border)
- Drive **button visibility and enablement from session + game state**:
  - Signed out (no refresh token, or refresh failed): **Log In** is shown; every button except **Log In** and **Exit** is disabled.
  - Signed in: **Log In** is hidden; **Playfields**, **Settings**, **Log Out**, **Exit** are enabled.
  - Active game in progress: **Resume Game** is shown and **Start Game** is hidden. No active game: **Start Game** is shown and **Resume Game** is hidden.
- Wire each button's **action**: Log In runs the interactive Auth0 sign-in and refreshes menu state on success; Resume Game opens the active game; Start Game opens the (stub) start-game flow; Playfields and Settings open (stub) destinations; Log Out clears the stored session and returns the menu to its signed-out state; Exit quits the app.
- Make the main menu the **universal post-boot destination**: the welcome/bootstrap sequence routes to the main menu for the signed-out, no-game, and active-game outcomes alike (instead of forking to a separate login page or straight into the game), and the menu re-establishes and reflects state itself.

## Capabilities

### New Capabilities
- `maui-main-menu`: The main-menu landing hub — its tactical presentation (panning map background, title, button roster) and the state model that shows/hides and enables/disables each button based on sign-in and active-game state.
- `maui-menu-actions`: The behavior behind each menu action — log in, resume game, start game, playfields, settings, log out (clear session), and exit — and how each changes navigation or session state.

### Modified Capabilities
<!-- None in openspec/specs. The sibling (unarchived) maui-welcome-screen change routes active-game→game and unauthenticated→login; this change redirects the bootstrap to the main menu for all outcomes, adjusted within this change's tasks. No archived capability's requirements change. -->

## Impact

- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - `Pages/HomePage.xaml` (+ `.xaml.cs`): rebuilt into the main menu with the layered panning-map background and button roster.
  - New `ViewModels/MainMenuViewModel.cs`: exposes menu state (signed-in, has-active-game, busy) and the button commands; consumes `ISessionService` / `ITokenStore` and reuses the interactive-login flow.
  - `ViewModels/WelcomeViewModel.cs`: bootstrap routing adjusted so the main menu is the destination for all outcomes.
  - `Resources/Styles/Styles.xaml`: new button styles (hunter-red filled, outline/bordered) and a panning-map background treatment — no inline visual properties on the page (single-source-of-truth styling rule).
  - `Resources/Styles/Colors.xaml`: reuse existing `Tp*` tokens; add brush/keys only if needed for the outline button.
- **Session/menu state**: needs a way for the menu to read current sign-in + active-game state and to trigger interactive login and log-out. Reuses `ISessionService`, `ITokenStore`, and the existing PKCE login logic (extracted so both the login page and the menu can invoke it).
- **Device geolocation** (new dependency): the GPS readout uses MAUI `Geolocation` (`Microsoft.Maui.Devices.Sensors`), requiring location permission (Android `ACCESS_COARSE/FINE_LOCATION`; iOS `NSLocationWhenInUseUsageDescription`). The readout degrades gracefully to a placeholder when permission is denied or no fix is available. The version line reads `AppInfo.Current.VersionString`.
- **Assets**: uses the tactical map raster `Resources/Images/the-prey-background.png` introduced by the `maui-splash-screen` change (or an equivalent larger-than-viewport map raster) as the panning background.
- **Platform**: Exit uses the platform application-quit path (`Application.Current.Quit()`), acceptable on Android/desktop; on iOS a programmatic quit is discouraged, so Exit is best-effort / hidden per platform.
- **Backend**: no changes. Reuses existing `GET /games/active` (via `ISessionService`) and the Auth0 token/authorize endpoints.
- **Non-goals**: the Start Game, Playfields, and Settings destinations remain navigation stubs; live gameplay, playfield editing, and settings UI are out of scope for this change.
