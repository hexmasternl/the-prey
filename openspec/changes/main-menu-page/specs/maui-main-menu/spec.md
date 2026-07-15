## ADDED Requirements

### Requirement: Main menu is the app's landing hub

The MAUI app SHALL present the main menu as the destination shown after the startup bootstrap completes, replacing the placeholder home stub. The main menu SHALL be shown for the signed-out, signed-in-with-no-active-game, and signed-in-with-active-game outcomes alike, and SHALL reflect the current state through its buttons rather than routing each state to a different page.

#### Scenario: Bootstrap resolves to the main menu when signed in without a game

- **WHEN** the startup bootstrap establishes a session and the backend reports no active game
- **THEN** the main menu is shown
- **AND** it presents the signed-in button set

#### Scenario: Bootstrap resolves to the main menu when an active game exists

- **WHEN** the startup bootstrap establishes a session and the backend reports an active game
- **THEN** the main menu is shown (not routed directly into the game)
- **AND** the Resume Game button is available

#### Scenario: Bootstrap resolves to the main menu when signed out

- **WHEN** the startup bootstrap cannot establish a session (no refresh token, refresh failed, or the access token is rejected)
- **THEN** the main menu is shown in its signed-out state

### Requirement: Panning tactical map background

The main menu SHALL display a tactical elevation/operations map as a full-screen background. Because the map raster is larger than the viewport, the background SHALL slowly and continuously pan (animate) behind the menu content, and SHALL be dimmed so the title and buttons stay legible. The panning SHALL NOT block or interfere with button input.

#### Scenario: Background pans behind the menu

- **WHEN** the main menu is displayed
- **THEN** the tactical map fills the screen behind the content
- **AND** it slowly pans in a continuous animation
- **AND** the map is dimmed so the title and buttons remain readable

#### Scenario: Background does not capture input

- **WHEN** the user taps a menu button over the panning map
- **THEN** the button receives the tap
- **AND** the map background does not intercept the gesture

### Requirement: App title and tactical styling

The main menu SHALL show the title "The Prey" at the top of the screen in the tactical display style, and SHALL follow The Prey tactical design language (void/dark base, phosphor-green signal accents, monospace typography). All visual treatment SHALL be applied via the app's central `Colors.xaml` / `Styles.xaml` resources; the page SHALL NOT declare colors, sizes, opacity, glow, or borders as inline/local properties.

#### Scenario: Title present

- **WHEN** the main menu is displayed
- **THEN** the title "The Prey" is shown at the top using the tactical display style

### Requirement: Screen layout regions

The main menu SHALL arrange its content over the panning map background in three regions: a top HUD header, a vertically-centered hero block aligned to the left, and a bottom-aligned button roster. The top header SHALL carry the "The Prey" title and, in the top-right corner, the telemetry readout. The hero block SHALL carry the slogan and the tagline. The buttons SHALL sit at the bottom of the screen.

#### Scenario: Regions positioned

- **WHEN** the main menu is displayed
- **THEN** the telemetry readout sits in the top-right corner
- **AND** the slogan and tagline are aligned to the left and vertically centered
- **AND** the button roster is aligned to the bottom of the screen

### Requirement: GPS telemetry readout

The main menu SHALL show a GPS position readout in the top-right corner as small, dim PT Mono text, formatted as degrees with hemisphere — for example `052° N // 004° E` — derived from the device's current location. When location permission is denied or no position fix is available, the readout SHALL show a neutral placeholder (e.g. `---° N // ---° E`) rather than an error or a blank.

#### Scenario: Position available

- **WHEN** the device location is available
- **THEN** the top-right readout shows the current latitude and longitude in degrees with N/S and E/W hemisphere, in small dim PT Mono text

#### Scenario: Position unavailable or permission denied

- **WHEN** location permission is denied or no position fix can be obtained
- **THEN** the readout shows a neutral placeholder in the same style
- **AND** the menu remains fully usable

### Requirement: Field manual version line

Beneath the GPS readout the main menu SHALL show a line reading `OPERATIONAL FIELD MANUAL — V <version>`, where `<version>` is the running application's version, in the same small dim PT Mono style.

#### Scenario: Version shown

- **WHEN** the main menu is displayed
- **THEN** a line `OPERATIONAL FIELD MANUAL — V <version>` is shown beneath the GPS readout using the actual app version

### Requirement: Hero slogan

The main menu SHALL show the slogan on two lines, left-aligned and vertically centered: line one is "STAY" followed by "HIDDEN", line two is "HUNT" followed by "SMART". The words "STAY" and "HUNT" SHALL use the regular tactical text color and the words "HIDDEN" and "SMART" SHALL use the signal-green color. The per-word colors SHALL be applied via central color resources, not inline literals.

#### Scenario: Slogan rendering

- **WHEN** the main menu is displayed
- **THEN** the slogan reads "STAY HIDDEN" on the first line and "HUNT SMART" on the second
- **AND** "STAY" and "HUNT" use the regular text color while "HIDDEN" and "SMART" use signal green
- **AND** the slogan block is left-aligned and vertically centered

### Requirement: Tagline

Beneath the slogan the main menu SHALL show a dim PT Mono tagline: "A GPS-based, real-world hide-and-seek game. Enter the playfield. Don't get caught."

#### Scenario: Tagline shown

- **WHEN** the main menu is displayed
- **THEN** the tagline "A GPS-based, real-world hide-and-seek game. Enter the playfield. Don't get caught." is shown beneath the slogan in dim PT Mono text

#### Scenario: No inline visual properties

- **WHEN** the main-menu XAML is reviewed
- **THEN** its visual treatment (colors, button styling, background dimming, glow, sizing) is applied through named styles and `StaticResource` color keys
- **AND** no color, opacity, glow, border, or size literal is declared inline on the page's visual elements

### Requirement: Button roster and styling

The main menu SHALL present the action buttons Log In, Resume Game, Start Game, Playfields, Settings, Log Out, and Exit, styled by role: Log In and Start Game as filled signal-green buttons, Resume Game as a filled hunter-red button, and Playfields, Settings, Log Out, and Exit as outline buttons (dark base with a signal-green border).

#### Scenario: Button styles by role

- **WHEN** the main menu is displayed with all buttons visible
- **THEN** Log In and Start Game use the filled signal-green style
- **AND** Resume Game uses the filled hunter-red style
- **AND** Playfields, Settings, Log Out, and Exit use the outline (green-bordered) style

### Requirement: Sign-in state drives visibility and enablement

The main menu SHALL show the Log In button only when the user is not signed in (no refresh token, or a refresh-token exchange failed). In the signed-out state, every button except Log In and Exit SHALL be disabled. When the user is signed in, the Log In button SHALL be hidden and the Playfields, Settings, Log Out, and Exit buttons SHALL be enabled.

#### Scenario: Signed-out state

- **WHEN** the main menu is shown and the user is not signed in
- **THEN** the Log In button is visible
- **AND** every button except Log In and Exit is disabled

#### Scenario: Refresh failure keeps the user signed out

- **WHEN** the app has a stored refresh token but the refresh-token exchange failed
- **THEN** the main menu treats the user as signed out
- **AND** shows the Log In button with only Log In and Exit enabled

#### Scenario: Signed-in state

- **WHEN** the main menu is shown and the user is signed in
- **THEN** the Log In button is hidden
- **AND** the Playfields, Settings, Log Out, and Exit buttons are enabled

### Requirement: Active-game state drives Resume vs Start

When the user is signed in, the main menu SHALL show exactly one of Resume Game or Start Game: Resume Game (and hide Start Game) when the user has an active ongoing game, and Start Game (and hide Resume Game) when the user has no active game. When the user is signed out, neither Resume Game nor Start Game SHALL be enabled.

#### Scenario: Active game in progress

- **WHEN** the user is signed in and has an active game
- **THEN** the Resume Game button is shown and enabled
- **AND** the Start Game button is hidden

#### Scenario: No active game

- **WHEN** the user is signed in and has no active game
- **THEN** the Start Game button is shown and enabled
- **AND** the Resume Game button is hidden

#### Scenario: Signed out hides gameplay entry

- **WHEN** the user is signed out
- **THEN** neither Resume Game nor Start Game is enabled

### Requirement: Gameplay entry is withheld until session state resolves

While the main menu is still resolving sign-in and active-game state (the active-game check has not yet completed), neither Resume Game nor Start Game SHALL be actionable, and the menu SHALL show a busy indication. Resume Game and Start Game SHALL become available only once the active-game check has completed, and then only per the resolved active-game state.

#### Scenario: Check in progress

- **WHEN** the main menu is still resolving sign-in and active-game state
- **THEN** neither Resume Game nor Start Game is enabled
- **AND** a busy indication is shown

#### Scenario: Check completes

- **WHEN** the active-game check completes for a signed-in user
- **THEN** the busy indication stops
- **AND** exactly one of Resume Game or Start Game becomes available per the resolved active-game state
