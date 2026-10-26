# app-main-menu Specification

## Purpose
Defines the signed-in player's main menu: the hub screen that shows the game's tactical identity, the device's live position and app version, and lets the player choose what to do next.

## Requirements

### Requirement: Animated tactical map background
The main menu SHALL show a dark background with a tactical map drawn in dark-green tints (grid, contour lines, faint markers) that is continuously animated while the screen is visible. The background SHALL be decorative only: it carries no information, is not focusable and is ignored by accessibility services.

#### Scenario: Background animates while visible
- **WHEN** the main menu is shown and the system does not request reduced motion
- **THEN** the tactical map background visibly moves (for example a slow sweep) behind the content

#### Scenario: Background is invisible to accessibility
- **WHEN** a screen reader traverses the main menu
- **THEN** no element of the background is announced

### Requirement: Position readout shows no signal until a location is known
The top-right corner of the main menu SHALL show the device position as `<lat>° N|S // <lon>° E|W`. Until a location is known, it SHALL show `-- ° N // -- ° E // NO SIGNAL`. The hemisphere letters are fixed GPS notation; the "NO SIGNAL" status is shown in the device language.

#### Scenario: No location yet on a Dutch device
- **WHEN** the device language is Dutch and no location has been received
- **THEN** the readout shows `-- ° N // -- ° E // GEEN SIGNAAL`

#### Scenario: No location yet
- **WHEN** the main menu opens and no device location has been received
- **THEN** the readout shows `-- ° N // -- ° E // NO SIGNAL`

#### Scenario: Location received
- **WHEN** the device reports latitude 52.36761 and longitude 4.90412
- **THEN** the readout shows `52.3676° N // 4.9041° E` (`52,3676° N // 4,9041° E` on a Dutch device, as decimals follow the device language) and no "NO SIGNAL" text

#### Scenario: Southern and western hemispheres
- **WHEN** the device reports latitude -33.86882 and longitude -151.20929
- **THEN** the readout shows `33.8688° S // 151.2093° W` (absolute values with the hemisphere letter)

### Requirement: Position readout follows the device while visible
While the main menu is visible, the readout SHALL update as the device reports new locations. Location SHALL only be requested while the main menu is in the foreground, and the position SHALL NOT be stored or sent anywhere.

#### Scenario: Device moves
- **WHEN** a newer location is reported while the main menu is visible
- **THEN** the readout shows the newer coordinates

#### Scenario: App goes to the background
- **WHEN** the player leaves the main menu or puts the app in the background
- **THEN** the app stops requesting location updates for the main menu

### Requirement: Location permission is requested from the main menu
When the app does not hold location permission, the main menu SHALL ask for it (behind the location tracking disclosure) only the first time it is shown on a device, whatever the player then chooses. After that the main menu SHALL NOT ask by itself again on this device, also not after an app restart or a new sign-in; the player grants location later from the Settings screen. If the player denies it, or the device cannot provide a location, the readout SHALL keep showing the no-signal text and the rest of the main menu SHALL work normally.

#### Scenario: Asked once per device
- **WHEN** the player declined location on the main menu earlier and starts the app again without location permission
- **THEN** the main menu is shown without the disclosure or a system permission prompt
- **AND** the Settings screen offers the way to grant location access

#### Scenario: Permission denied
- **WHEN** the player denies the location permission
- **THEN** the readout shows `-- ° N // -- ° E // NO SIGNAL` and all menu buttons remain usable

#### Scenario: Permission granted
- **WHEN** the player grants the location permission
- **THEN** the readout switches to the device coordinates once a location is received

### Requirement: Version line shows the real app version
Below the position readout the main menu SHALL show a dim line `OPERATIONAL FIELD MANUAL v<version>`, where `<version>` is the installed app's version name. The words are shown in the device language.

#### Scenario: Version shown
- **WHEN** the installed app has version name `1.4.2`
- **THEN** the line reads `OPERATIONAL FIELD MANUAL v1.4.2`

### Requirement: Slogan and description
The main menu SHALL show the slogan on two lines — "STAY HIDDEN." and "HUNT SMART." — with the first word of each line in the primary text colour and the second word in the Signal accent, followed by the description "A GPS-based, real-world hide-and-seek game. Enter the playfield. Don't get caught." in the muted text colour. Both SHALL be shown in the device language (English or Dutch).

#### Scenario: English device
- **WHEN** the device language is English
- **THEN** the main menu shows "STAY HIDDEN." / "HUNT SMART." and the English description

#### Scenario: Dutch device
- **WHEN** the device language is Dutch
- **THEN** the slogan and description are shown in Dutch with the same two-colour styling

### Requirement: Menu actions
The main menu SHALL offer, in this order: "Play now" as the single primary action, then "Playfields", "Settings", "Sign out" and "Quit" as secondary actions. Every button SHALL have a touch target of at least 48×48 dp.

#### Scenario: Buttons shown
- **WHEN** the main menu is shown
- **THEN** it shows Play now (primary), Playfields, Settings, Sign out and Quit (secondary), in that order

### Requirement: Play now, Playfields and Settings open their destinations
Tapping "Play now", "Playfields" or "Settings" SHALL open that feature's destination. Pressing Back there SHALL return to the main menu.

#### Scenario: Open playfields
- **WHEN** the player taps "Playfields"
- **THEN** the Playfields destination is shown

#### Scenario: Back to the menu
- **WHEN** the player presses Back on the Play now, Playfields or Settings destination
- **THEN** the main menu is shown again

### Requirement: Sign out from the main menu
Tapping "Sign out" SHALL sign the player out as defined by app authentication and show the landing page. While sign-out is in progress the button SHALL show a busy state and SHALL NOT accept another tap.

#### Scenario: Sign out
- **WHEN** the player taps "Sign out" and sign-out completes
- **THEN** the landing page is shown and pressing Back closes the app

### Requirement: Quit closes the app
Tapping "Quit" SHALL close the app and remove it from the recent-apps list without signing the player out.

#### Scenario: Quit and reopen
- **WHEN** the player taps "Quit" and later launches the app again
- **THEN** the app restores the session and opens on the main menu

### Requirement: Player name with running-light animation
The bottom of the main menu SHALL show the player's callsign, or their display name when no callsign is set, in the primary text colour. Every 5 seconds a single highlight in the Signal accent SHALL sweep across the name once, letter by letter, and fade back to the text colour. The name SHALL update without restarting the screen when the profile changes.

#### Scenario: Name shown
- **WHEN** the signed-in player's callsign is "Night-Hawk_7"
- **THEN** "NIGHT-HAWK_7" is shown at the bottom of the main menu in the text colour

#### Scenario: Running light repeats
- **WHEN** the main menu stays visible for more than 10 seconds and reduced motion is off
- **THEN** a Signal-green highlight has swept across the name at least twice, with the name in the text colour between sweeps

#### Scenario: Profile refreshed
- **WHEN** the background profile refresh returns a changed callsign
- **THEN** the bottom of the main menu shows the new callsign

### Requirement: Reduced motion
When the system requests reduced motion (animations disabled), the main menu SHALL show the tactical map background as a still image and the player name without the running light. All text and actions SHALL remain available.

#### Scenario: Animations disabled
- **WHEN** the device's animator duration scale is 0
- **THEN** neither the background nor the player name animates, and the screen content is unchanged otherwise
