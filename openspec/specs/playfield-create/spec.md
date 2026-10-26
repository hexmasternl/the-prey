# playfield-create Specification

## Purpose
Creating a new playfield in the Android app: the form opened from the Private tab's create button, with a name, a visibility and an area drawn on the map, which is stored on the server when the player saves it.

## Requirements

### Requirement: Create button opens an empty new-playfield form

Tapping the create button on the Private tab SHALL open the playfield details screen in create mode. It SHALL be titled "New playfield" and SHALL start with an empty name, Public unchecked and no area points. Nothing SHALL be downloaded or sent to the server when it opens.

#### Scenario: Open the new-playfield form

- **WHEN** the player taps the create button on the Private tab
- **THEN** the details screen opens titled "New playfield", with an empty name field, an unchecked Public checkbox and an area of 0 points

#### Scenario: No loading state

- **WHEN** the new-playfield form opens
- **THEN** the form is shown at once, without a loading indicator or a load error

### Requirement: New playfield follows the playfield form rules

The new-playfield form SHALL apply the same name and visibility rules as editing a playfield: the Public checkbox SHALL be enabled only for a public-eligible name, and changing the name to an ineligible one SHALL uncheck Public.

#### Scenario: Public needs an eligible name

- **WHEN** the player types "Vondelpark" as the name
- **THEN** the Public checkbox is disabled and unchecked

#### Scenario: Eligible name allows Public

- **WHEN** the player types "NL, Utrecht, Wilhelminapark" and checks Public
- **THEN** the Public checkbox is checked

### Requirement: Area is drawn on the area screen

The Playfield Area action SHALL open the area screen for the new playfield with no points, centered on the device's location when it is known. Saving on the area screen SHALL return the points to the new-playfield form, which SHALL show the point count. Cancelling on the area screen SHALL leave the form's area unchanged.

#### Scenario: Draw an area

- **WHEN** the player opens Playfield Area, taps four points on the map and saves the area
- **THEN** the new-playfield form is shown again and reports 4 points

#### Scenario: Cancel the area

- **WHEN** the player opens Playfield Area, taps three points and cancels
- **THEN** the new-playfield form still reports 0 points

### Requirement: Save needs a name and an area

Save SHALL be enabled only when the trimmed name is not blank and the area has at least 3 points. While the area has fewer than 3 points, the form SHALL show a hint that an area is needed before the playfield can be saved.

#### Scenario: Name without area

- **WHEN** the player has typed a name but not set an area
- **THEN** Save is disabled and the hint says an area is needed

#### Scenario: Area without name

- **WHEN** the player has set an area of 4 points but the name is blank
- **THEN** Save is disabled

#### Scenario: Name and area

- **WHEN** the player has typed a name and set an area of 4 points
- **THEN** Save is enabled and the area hint is not shown

### Requirement: Saving creates the playfield

Save SHALL create the playfield on the server with its trimmed name, its visibility (public only for an eligible name) and its area points, owned by the signed-in player. While the request runs, the form SHALL show progress and SHALL NOT accept input. On success the screen SHALL close and the Private tab SHALL list the new playfield.

#### Scenario: Successful create

- **WHEN** the player saves a new playfield named "NL, Utrecht, Wilhelminapark", Public checked, with a 4-point area, and the server accepts it
- **THEN** the screen closes, the Private tab is shown, and it lists "NL, Utrecht, Wilhelminapark" with a Public badge

#### Scenario: Busy while creating

- **WHEN** the create request is running
- **THEN** a progress indicator is shown and Save, Cancel, the fields and Playfield Area cannot be used

#### Scenario: Save sent once

- **WHEN** the player taps Save twice in quick succession
- **THEN** only one playfield is created

### Requirement: Failed create keeps the player's input

When the create fails because of a network error, a validation error or a server error, the form SHALL stay open with the name, visibility and area intact, SHALL show a message that the playfield was not created, and SHALL allow Save again.

#### Scenario: Offline

- **WHEN** the player taps Save while the device is offline
- **THEN** the form stays open with the entered name, visibility and area, a message says the playfield was not created, and Save is enabled again

### Requirement: Cancel discards the new playfield

Cancel, the top-bar back arrow and the system Back gesture SHALL close the new-playfield form without sending anything to the server and return to the Private tab. The entered name, visibility and area SHALL be discarded, so the next create starts empty.

#### Scenario: Cancel after entering details

- **WHEN** the player types a name, draws an area and taps Cancel
- **THEN** the Private tab is shown, no playfield was created, and tapping the create button again opens an empty form

### Requirement: New playfield survives configuration changes

The unsaved name, visibility and area of a new playfield SHALL be kept across a rotation and across the app process being recreated while the form or the area screen is open.

#### Scenario: Rotation

- **WHEN** the player has typed a name and drawn an area, then rotates the device
- **THEN** the form still shows the name, the visibility and the point count

### Requirement: New-playfield texts in English and Dutch

Every user-facing text of the new-playfield form, including the title, the area hint, the not-created message and the accessibility descriptions, SHALL be available in English and Dutch and follow the device language.

#### Scenario: Dutch device

- **WHEN** the device language is Dutch and the new-playfield form is open without an area
- **THEN** the title reads "Nieuw speelveld" and the area hint is in Dutch
