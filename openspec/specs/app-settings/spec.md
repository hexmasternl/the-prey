# app-settings Specification

## Purpose
Gives a signed-in player of the Android app one place to change their callsign, choose the app language, and review or grant location access, with the location tracking disclosure Google Play requires shown before any location permission request.

## Requirements

### Requirement: Settings screen is opened from the main menu
The main menu's Settings action SHALL open the Settings screen with the sections Callsign, Language and Location. Back SHALL return to the main menu.

#### Scenario: Open settings
- **WHEN** a signed-in player taps Settings on the main menu
- **THEN** the Settings screen is shown with the Callsign, Language and Location sections

#### Scenario: Back to the main menu
- **WHEN** the player presses Back or the screen's back action on the Settings screen
- **THEN** the main menu is shown

### Requirement: Current callsign is shown and editable
The Callsign section SHALL show the player's current callsign in an editable field. When the player has no callsign yet, the field SHALL be empty and the section SHALL state that the display name is used until a callsign is set.

#### Scenario: Player with a callsign
- **WHEN** the Settings screen opens for a player whose callsign is "NightOwl"
- **THEN** the callsign field contains "NightOwl"

#### Scenario: Player without a callsign
- **WHEN** the Settings screen opens for a player with an empty callsign
- **THEN** the callsign field is empty and a hint says the display name is shown until a callsign is set

### Requirement: Callsign is validated before saving
The app SHALL accept a callsign only when, after trimming leading and trailing spaces, it is 3 to 30 characters of letters (a–z, A–Z), digits, spaces, `-`, `_`, `&`, `$`, `#` or `@`. Save SHALL be disabled while the value is invalid or unchanged, and an invalid value SHALL show a message that names the rule.

#### Scenario: Too short
- **WHEN** the player enters "ab"
- **THEN** Save is disabled and the field shows the callsign rule

#### Scenario: Not allowed character
- **WHEN** the player enters "Hunter!"
- **THEN** Save is disabled and the field shows the callsign rule

#### Scenario: Unchanged value
- **WHEN** the field contains the current callsign
- **THEN** Save is disabled

#### Scenario: Valid new value
- **WHEN** the player enters "Night Owl #7"
- **THEN** Save is enabled

### Requirement: Saving the callsign updates the player everywhere
Saving SHALL send the callsign together with the player's preferred language to the server's settings endpoint. On success the app SHALL store the returned profile on the device, show a confirmation, and show the new callsign wherever the app shows the player's name, including the main menu.

#### Scenario: Successful save
- **WHEN** the player saves the valid callsign "Night Owl #7" and the server accepts it
- **THEN** a confirmation is shown
- **AND** returning to the main menu shows "Night Owl #7" as the player name

#### Scenario: Save survives a restart
- **WHEN** the player saved a new callsign and restarts the app without network
- **THEN** the main menu shows the new callsign

### Requirement: Callsign save failures are reported
While a save is in progress the app SHALL show progress and SHALL NOT allow a second save. If the server rejects the callsign, the app SHALL show the server's message at the field. If the request fails for another reason, the app SHALL show a retryable error. In both cases the edited value and the previous stored callsign SHALL be kept.

#### Scenario: Server rejects the callsign
- **WHEN** the player saves a callsign and the server answers with a validation error
- **THEN** the validation message is shown at the callsign field and the stored callsign is unchanged

#### Scenario: No network
- **WHEN** the player saves a callsign without a network connection
- **THEN** an error says the callsign could not be saved, the edited value stays in the field, and Save can be tapped again

### Requirement: Player chooses the app language
The Language section SHALL offer System default, English and Nederlands, with the current choice selected. Choosing an option SHALL switch all app text to that language right away, without the player restarting the app, and the choice SHALL persist across app restarts. System default SHALL follow the device language, using Dutch when the device language is Dutch and English otherwise.

#### Scenario: Switch to Dutch
- **WHEN** a player on an English device selects Nederlands
- **THEN** the Settings screen and every screen after it are shown in Dutch
- **AND** after restarting the app the text is still Dutch

#### Scenario: Back to system default
- **WHEN** a player on an English device who chose Nederlands selects System default
- **THEN** the app text is English

#### Scenario: Language names are not translated
- **WHEN** the Language section is shown in either language
- **THEN** the options read "English" and "Nederlands"

### Requirement: Language choice is sent to the server
After the language changes, the app SHALL send the resulting language (`en` or `nl`) as the player's preferred language to the server's settings endpoint, together with the current callsign, and store the returned profile. A failed request SHALL NOT undo the language change in the app; it SHALL be sent again on the next successful settings save. When the player has no valid callsign yet, the language SHALL only be applied locally.

#### Scenario: Language synced
- **WHEN** a player with a callsign selects Nederlands while online
- **THEN** the server stores `nl` as the player's preferred language

#### Scenario: Offline language change
- **WHEN** a player selects Nederlands without a network connection
- **THEN** the app text switches to Dutch
- **AND** the next successful callsign save also sends `nl`

### Requirement: Location access status is shown
The Location section SHALL show the current location access as one of: Off, Approximate only, While using the app, or All the time. On Android 13 and later it SHALL also show whether notifications are allowed. The status SHALL be read again each time the screen becomes visible, including after returning from the system settings.

#### Scenario: Precise location while in use
- **WHEN** the player has granted precise location but not background location
- **THEN** the status reads "While using the app"

#### Scenario: Approximate only
- **WHEN** the player has granted only approximate location
- **THEN** the status reads "Approximate only" and explains that the game needs precise location

#### Scenario: Status refreshes after system settings
- **WHEN** the player changes location to "Allow all the time" in system settings and returns to the app
- **THEN** the status reads "All the time" without leaving the Settings screen

### Requirement: Location disclosure is always visible in settings
The Location section SHALL show the location tracking disclosure as a visually prominent warning panel, regardless of the current permission state. It SHALL state, in the app language: that The Prey collects the device's precise location; only during an active game; that collection continues while the app is closed, in the background or the screen is off, through a foreground service; that a persistent notification is shown while tracking runs; that tracking stops when the game ends or the player leaves; why the game needs it; and it SHALL link to the privacy page on theprey.nl in the app language. The wording SHALL match the disclosure the app shows before its first permission request.

#### Scenario: Disclosure shown with permission granted
- **WHEN** the player opens Settings with location "All the time"
- **THEN** the warning panel with the full disclosure is shown

#### Scenario: Disclosure shown with permission missing
- **WHEN** the player opens Settings with location Off
- **THEN** the warning panel with the full disclosure is shown above the grant action

#### Scenario: Privacy link
- **WHEN** the player taps the privacy link in the panel while the app language is Dutch
- **THEN** the Dutch privacy page on theprey.nl opens in the browser

### Requirement: Player grants location access from settings
When location access is not All the time, the Location section SHALL offer a Grant location access action placed directly below the disclosure. If the system can still show the location prompt, the action SHALL request precise location (and, on Android 13 and later, notifications after the location prompt is answered). Otherwise, and for the step from While using the app to All the time, the action SHALL explain the setting to pick and open the app's system settings page. The action SHALL NOT be offered when access is All the time.

#### Scenario: Declined once, prompt still available
- **WHEN** a player who tapped Not now on the main menu taps Grant location access
- **THEN** the system precise location prompt is shown

#### Scenario: Permanently denied
- **WHEN** the system will no longer show the location prompt and the player taps Grant location access
- **THEN** the app tells the player to allow precise location and opens the app's system settings page

#### Scenario: Upgrade to all the time
- **WHEN** location access is While using the app and the player taps Grant location access
- **THEN** the app tells the player to pick "Allow all the time" and opens the app's system settings page

#### Scenario: Already fully granted
- **WHEN** location access is All the time
- **THEN** no grant action is shown and the status confirms that tracking can run during a game

### Requirement: No location prompt without the disclosure
The Settings screen SHALL NOT trigger any system location permission prompt except through the Grant location access action, which is shown only together with the disclosure. Opening the screen SHALL NOT trigger a prompt.

#### Scenario: Opening settings
- **WHEN** a player without location permission opens Settings
- **THEN** no system permission prompt is shown until the player taps Grant location access

### Requirement: Player resets the first-time hints

The settings screen SHALL show a hints section with a "Show hints again" button. Tapping it SHALL mark the playfield area tour, the prey HUD tour and the hunter HUD tour as not seen, and SHALL confirm the reset on screen. Each tour SHALL then be shown the next time its situation occurs. The button and confirmation SHALL be available in English and Dutch.

#### Scenario: Reset all hints

- **WHEN** the player has seen all three tours and taps "Show hints again"
- **THEN** a confirmation that the hints will show again is displayed
- **AND** the next new playfield's area screen, the next in-progress game as prey and the next in-progress game as hunter each show their tour

#### Scenario: Reset when nothing was seen

- **WHEN** no tour has been seen yet and the player taps "Show hints again"
- **THEN** the confirmation is displayed and all three tours are still not seen

#### Scenario: Dutch device

- **WHEN** the app language is Dutch
- **THEN** the button reads "Hints opnieuw tonen" and the confirmation is in Dutch

#### Scenario: Reset failure

- **WHEN** the hints cannot be reset because device storage fails
- **THEN** the settings screen shows an error message and stays usable
