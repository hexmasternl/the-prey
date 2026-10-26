# Spec Delta

## Purpose

Makes sure every player of the Android app is told clearly, and before any system permission prompt, that The Prey tracks their precise location in the background during an active game, and asks for consent in a way that meets Google Play's prominent-disclosure requirements.

## ADDED Requirements

### Requirement: Disclosure is shown after the first sign-in on a device
After a player signs in, if location permission is missing and the main menu has not shown the location tracking disclosure automatically on this device before, the app SHALL show the disclosure as a modal dialog before the main menu can be used. The dialog SHALL NOT be dismissible by tapping outside it or pressing Back; it SHALL close only through one of its actions.

#### Scenario: First sign-in on a fresh install
- **WHEN** a player completes sign-in and the disclosure has never been accepted on this device
- **THEN** the location tracking disclosure dialog is shown on top of the main menu
- **AND** the main menu cannot be used until the player picks an action

#### Scenario: Back and outside taps do not dismiss the dialog
- **WHEN** the disclosure dialog is shown and the player presses Back or taps outside the dialog
- **THEN** the dialog stays open

#### Scenario: Restored session with the disclosure already accepted
- **WHEN** the app starts with a stored session, the disclosure was accepted earlier on this device, and location permission is granted
- **THEN** the main menu is shown without the disclosure dialog

### Requirement: Disclosure content
The disclosure SHALL state, in the app's display language (English or Dutch): that The Prey collects the device's precise location; that collection happens only while the player is in an active game; that it continues while the app is closed, in the background or the screen is off; that a persistent notification is shown while tracking runs; that tracking stops when the game ends or the player leaves; and why the game needs it (showing players' positions to the hunter and checking the playfield boundaries).

#### Scenario: Required statements present in English
- **WHEN** the disclosure is shown on a device set to English
- **THEN** the text covers precise location, active game only, background/screen-off collection, the persistent notification, when tracking stops, and the gameplay purpose

#### Scenario: Required statements present in Dutch
- **WHEN** the disclosure is shown on a device set to Dutch
- **THEN** the same statements are shown in Dutch

#### Scenario: Disclosure links to the privacy policy
- **WHEN** the player taps the privacy policy link in the disclosure
- **THEN** the privacy page on theprey.nl opens in the browser, in the player's language

### Requirement: Location permission is requested only after the disclosure is accepted
The app SHALL NOT trigger the system location permission prompt unless the player has accepted the disclosure in the same flow. Accepting the disclosure SHALL immediately request precise location permission (fine and coarse together).

#### Scenario: Player accepts the disclosure
- **WHEN** the player taps Continue in the disclosure dialog
- **THEN** the dialog closes and the system location permission prompt is shown
- **AND** the disclosure is recorded as accepted on this device

#### Scenario: No prompt before disclosure
- **WHEN** a signed-in player reaches the main menu without having accepted the disclosure
- **THEN** no system location permission prompt is shown

### Requirement: Notification permission is requested with the location permission
On Android 13 and later, after the location permission prompt has been answered, the app SHALL request the notification permission, so the tracking notification can be shown during a game. On older versions no notification prompt is shown.

#### Scenario: Android 13 or later
- **WHEN** the player has answered the location permission prompt on Android 13 or later and notification permission is not granted
- **THEN** the system notification permission prompt is shown

#### Scenario: Android 12 or earlier
- **WHEN** the player has answered the location permission prompt on Android 12 or earlier
- **THEN** no notification permission prompt is shown

### Requirement: Declining keeps the app usable
If the player taps Not now in the disclosure, or denies the system location prompt, the app SHALL close the dialog and show the main menu. The app SHALL NOT read the device location. The main menu position readout SHALL show the no-signal state.

#### Scenario: Player taps Not now
- **WHEN** the player taps Not now in the disclosure dialog
- **THEN** the dialog closes, no system permission prompt is shown, and the main menu is usable
- **AND** the position readout shows the no-signal state

#### Scenario: Player denies the system prompt
- **WHEN** the player accepted the disclosure but denies the system location prompt
- **THEN** the main menu is usable, no location is read, and the position readout shows the no-signal state

### Requirement: Main menu shows the disclosure automatically only once
The main menu SHALL show the disclosure automatically only the first time on a device that it is shown while location permission is missing, whatever the player then chooses. After that, the app SHALL NOT show the disclosure or a location prompt by itself again on this device, also not after an app restart, a new sign-in or a revocation in system settings. The disclosure SHALL still be shown before every location permission request the player starts: before any action that starts or joins a game, and through the Grant location access action on the Settings screen. When the player accepted the disclosure but the system no longer shows the location prompt, those paths SHALL explain that location is off and offer to open the app's system settings page.

#### Scenario: Next app start after Not now
- **WHEN** the player tapped Not now earlier and starts the app again with a stored session
- **THEN** the main menu is shown without the disclosure dialog

#### Scenario: Permission revoked in system settings
- **WHEN** the main menu already showed the disclosure once, location permission has since been revoked, and the app returns to the foreground
- **THEN** no disclosure dialog and no system prompt is shown by itself
- **AND** the disclosure is shown again before location is requested from a start or join action or from Settings

#### Scenario: Permission permanently denied
- **WHEN** the player accepted the disclosure, the system will no longer show the location prompt (denied twice), and the player starts or joins a game
- **THEN** the app explains that location is off for The Prey and offers a button that opens the app's system settings page

### Requirement: Starting or joining a game requires location consent
Any action that starts or joins a game SHALL check that location permission is granted. If it is not, the app SHALL show the disclosure first and SHALL NOT continue into the game until permission is granted.

#### Scenario: Join without permission
- **WHEN** a player without location permission tries to start or join a game
- **THEN** the disclosure dialog is shown and the game is not entered
- **AND** if the player then grants permission, the start or join action continues

### Requirement: Disclosure state is device-local
Whether the disclosure was accepted SHALL be stored on the device only, outside app backups, and SHALL survive sign-out and sign-in with another account. Clearing app data or reinstalling SHALL reset it, so the disclosure is shown again.

#### Scenario: Sign out and sign in again
- **WHEN** a player who accepted the disclosure and granted permission signs out and signs in again, with the same or another account
- **THEN** the disclosure is not shown again

#### Scenario: Reinstall
- **WHEN** the app is reinstalled or its data is cleared
- **THEN** the disclosure is shown after the next sign-in
