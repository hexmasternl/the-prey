# Spec Delta

## MODIFIED Requirements

### Requirement: Location disclosure is always visible in settings
The Location section SHALL show the location tracking disclosure as a collapsible warning panel, regardless of the current permission state. Collapsed, which is the default each time Settings opens, it SHALL show the warning triangle and "Why The Prey uses your location". Tapping the header SHALL expand it to the full disclosure (precise location, active game only, background/screen-off collection through a foreground service, the persistent notification, when tracking stops, why) with the privacy link; tapping again SHALL collapse it.

#### Scenario: Disclosure shown with permission granted
- **WHEN** the player opens Settings with location "All the time"
- **THEN** the collapsed warning panel with the warning triangle and "Why The Prey uses your location" is shown

#### Scenario: Disclosure shown with permission missing
- **WHEN** the player opens Settings with location Off
- **THEN** the collapsed warning panel is shown below the grant action

#### Scenario: Expand the panel
- **WHEN** the player taps the collapsed panel
- **THEN** it expands and shows the full disclosure, worded as the dialog the app shows before its first permission request, with the privacy link

#### Scenario: Collapse the panel
- **WHEN** the player taps the header of the expanded panel
- **THEN** it collapses back to the warning triangle and "Why The Prey uses your location"

#### Scenario: Accessible toggle
- **WHEN** a TalkBack user focuses the panel header
- **THEN** it is announced as a button with its expanded or collapsed state, and the whole header row is the touch target

#### Scenario: Privacy link
- **WHEN** the player taps the privacy link in the expanded panel while the app language is Dutch
- **THEN** the Dutch privacy page on theprey.nl opens in the browser

### Requirement: Player grants location access from settings
When location access is not All the time, the Location section SHALL offer a Grant location access action placed directly below the access status and above the disclosure panel. If the system can still show the location prompt, the action SHALL first show the disclosure dialog and request precise location (and, on Android 13 and later, notifications after the location prompt is answered) only after Continue. Otherwise, and for the step from While using the app to All the time, the action SHALL explain the setting to pick and open the app's system settings page. The action SHALL NOT be offered when access is All the time.

#### Scenario: Declined once, prompt still available
- **WHEN** a player who tapped Not now on the main menu taps Grant location access
- **THEN** the disclosure dialog is shown
- **AND** after Continue the system precise location prompt is shown

#### Scenario: Not now in the dialog
- **WHEN** the player taps Grant location access and then Not now in the disclosure dialog
- **THEN** the dialog closes, no system prompt is shown and the Settings screen is unchanged

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
The Settings screen SHALL NOT trigger any system location permission prompt except through Continue in the disclosure dialog that the Grant location access action opens. Opening the screen, expanding the panel or tapping Not now SHALL NOT trigger a prompt.

#### Scenario: Opening settings
- **WHEN** a player without location permission opens Settings
- **THEN** no system permission prompt is shown until the player taps Grant location access and then Continue

#### Scenario: Expanding the panel
- **WHEN** a player without location permission expands the disclosure panel
- **THEN** no system permission prompt is shown
