# Spec Delta

## ADDED Requirements

### Requirement: Playfield details screen

The client SHALL present a dedicated details screen for a single playfield, opened by tapping the playfield in the playfields list on either tab. The screen SHALL download the full playfield from the server and display its name and a "Public" checkbox reflecting its current visibility. While downloading it SHALL show a loading indicator; when the download fails it SHALL show an error message with a retry action.

#### Scenario: Open from the Private tab

- **WHEN** the player taps one of their playfields on the Private tab of the playfields list
- **THEN** the details screen opens, downloads that playfield and shows its name and a Public checkbox matching its visibility

#### Scenario: Open from the Public tab

- **WHEN** the player taps a playfield in the Public tab's search results
- **THEN** the details screen opens, downloads that playfield and shows its name and a checked Public checkbox

#### Scenario: Loading indicator

- **WHEN** the details screen is open and the download has not completed yet
- **THEN** a loading indicator is shown instead of the details

#### Scenario: Download fails and retry succeeds

- **WHEN** the download fails because the server is unreachable or returns an error, and the player taps retry after the server is reachable again
- **THEN** an error message with a retry action is shown first, and after retrying the playfield's details are shown

#### Scenario: Playfield not found

- **WHEN** the details screen is opened for a playfield the server reports as not found (deleted, or private and owned by someone else)
- **THEN** the screen shows a "not found" message and a way back to the playfields list

### Requirement: Own playfields opened from the Private tab are editable

A playfield opened from the Private tab SHALL be editable: the player SHALL be able to change its name and, subject to the public-name rule, its Public checkbox. A playfield opened from the Public tab SHALL be read-only: the name SHALL NOT be editable, the checkbox SHALL be disabled and no Save action SHALL be offered, also when the player owns it.

#### Scenario: Private-tab playfield is editable

- **WHEN** the details screen shows a playfield opened from the Private tab
- **THEN** the name can be edited and Cancel and Save actions are shown

#### Scenario: Public-tab playfield is read-only

- **WHEN** the details screen shows a playfield opened from the Public tab
- **THEN** the name is shown as text that cannot be edited, the Public checkbox is disabled, and only a Close action is shown

#### Scenario: Own public playfield opened from the Public tab

- **WHEN** the player opens one of their own public playfields from the Public tab
- **THEN** the details are read-only

### Requirement: Public checkbox requires a public-eligible name

The Public checkbox SHALL be enabled only while the trimmed name follows the public-name rule the server enforces: a 2–4 letter uppercase country code, a comma and space, a city starting with an uppercase letter, a comma and space, and a field name starting with a letter or digit (`CC, City, Fieldname`). The screen SHALL show a hint with this format and an example. When the name stops following the rule, the checkbox SHALL be cleared and disabled.

#### Scenario: Eligible name enables the checkbox

- **WHEN** the player edits the name to "NL, Amsterdam, Vondelpark"
- **THEN** the Public checkbox is enabled and can be checked

#### Scenario: Ineligible name disables the checkbox

- **WHEN** the name is "Vondelpark"
- **THEN** the Public checkbox is disabled and unchecked, and a hint explains the format `CC, City, Fieldname` with an example

#### Scenario: Editing a public name into an ineligible one clears the checkbox

- **WHEN** the Public checkbox is checked and the player edits the name to "nl, amsterdam, vondelpark"
- **THEN** the checkbox becomes unchecked and disabled

#### Scenario: Surrounding spaces are ignored

- **WHEN** the name is "  NL, Amsterdam, Vondelpark  "
- **THEN** the Public checkbox is enabled

#### Scenario: Diacritics and digits are accepted

- **WHEN** the name is "BE, Liège, Parc de la Boverie 2"
- **THEN** the Public checkbox is enabled

### Requirement: Saving the playfield returns to the list

An editable details screen SHALL offer a Save action that sends the playfield's name, visibility and area points to the server in one update, carrying the last-updated time the screen loaded. Save SHALL be enabled only when the name, visibility or area differs from the loaded playfield and the trimmed name is not blank. On success the screen SHALL close, return to the playfields list and the list SHALL show the updated playfield.

#### Scenario: Save enabled only after a change

- **WHEN** an editable playfield has just been loaded and nothing has been changed
- **THEN** the Save action is disabled

#### Scenario: Blank name cannot be saved

- **WHEN** the player clears the name field
- **THEN** the Save action is disabled

#### Scenario: Successful save

- **WHEN** the player renames the playfield to "NL, Utrecht, Wilhelminapark", checks Public and taps Save, and the server accepts the update
- **THEN** the screen closes, the playfields list is shown and it lists the playfield with its new name and a Public badge

#### Scenario: Area edits are saved with the details

- **WHEN** the player changes the area on the playfield area screen, saves it there, and then taps Save on the details screen
- **THEN** the update sent to the server contains the new area points

#### Scenario: Busy while saving

- **WHEN** a save request is running
- **THEN** a progress indicator is shown and Save, Cancel and the fields cannot be used until the request completes

### Requirement: Failed saves keep the player's edits

When a save fails because of a network error, a validation error, a forbidden response or a server error, the details screen SHALL stay open with the player's edits intact and SHALL show a message that the playfield was not saved. When the server reports a conflict because the playfield was changed elsewhere since it was loaded, the screen SHALL replace its contents with the newer version the server returned and SHALL say that the playfield was changed elsewhere and the latest version is shown.

#### Scenario: Network error

- **WHEN** the player taps Save while the device is offline
- **THEN** the screen stays open with the edited name, visibility and area, and a message says the playfield was not saved and to try again

#### Scenario: Conflict

- **WHEN** the player taps Save and the server responds that a newer version exists
- **THEN** the screen shows the server's newer name, visibility and area, Save is disabled, and a message says the playfield was changed elsewhere

#### Scenario: Forbidden

- **WHEN** the player taps Save and the server responds that the player is not the owner
- **THEN** the screen stays open and a message says the playfield could not be saved

### Requirement: Cancel discards changes

The details screen SHALL offer a Cancel action (Close on a read-only screen). Cancel, the top-bar back arrow and the system Back gesture SHALL close the screen without sending anything to the server and return to the playfields list. Unsaved name, visibility and area edits SHALL be discarded.

#### Scenario: Cancel after editing

- **WHEN** the player edits the name, changes the area, and taps Cancel
- **THEN** the playfields list is shown, no update was sent, and reopening the playfield shows the original name and area

#### Scenario: System Back behaves as Cancel

- **WHEN** the player has unsaved edits and uses the system Back gesture
- **THEN** the screen closes without saving

### Requirement: Playfield Area entry point

The details screen SHALL show a "Playfield Area" button, on both editable and read-only screens, that opens the playfield area screen for the playfield. The area screen SHALL start from the details screen's current area, including area changes saved there earlier but not yet saved to the server.

#### Scenario: Playfield Area button is always visible

- **WHEN** the details screen shows any playfield
- **THEN** a "Playfield Area" button is visible

#### Scenario: Opening the area screen

- **WHEN** the player taps "Playfield Area"
- **THEN** the playfield area screen opens showing the playfield's area

#### Scenario: Reopening shows earlier unsaved area edits

- **WHEN** the player saved a changed area on the area screen, returned to details without saving there, and taps "Playfield Area" again
- **THEN** the area screen shows the changed area

### Requirement: Details texts follow the device language

All texts on the details screen SHALL be available in English and Dutch and SHALL follow the device language.

#### Scenario: Dutch device

- **WHEN** the device language is Dutch
- **THEN** the details screen shows Dutch labels, including "Openbaar" for the Public checkbox and "Speelveldgebied" for the Playfield Area button

## REMOVED Requirements

### Requirement: Display playfield metadata

**Reason**: Visibility no longer commits per toggle with a PATCH and rollback; the native details screen edits name and visibility as unsaved changes saved together with one update.
**Migration**: See requirements "Playfield details screen", "Public checkbox requires a public-eligible name", "Saving the playfield returns to the list" and "Failed saves keep the player's edits".

### Requirement: Display playfield area map

**Reason**: The native details screen has no embedded preview map. The area is shown full screen on the playfield area screen, opened with the "Playfield Area" button.
**Migration**: Use the "Playfield Area" button (requirement "Playfield Area entry point") to view the area.

### Requirement: Set Area entry point

**Reason**: Replaced by the "Playfield Area" button. It is also shown on read-only playfields and opens the area screen from the details screen's current, possibly unsaved, area.
**Migration**: See requirement "Playfield Area entry point".
