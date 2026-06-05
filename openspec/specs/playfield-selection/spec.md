# playfield-selection Specification

## Purpose
TBD - created by archiving change playfield-selection. Update Purpose after archive.
## Requirements
### Requirement: Display local playfields on open
The page SHALL load all playfields from IndexedDB and display them as the initial list when opened, before any search is performed.

#### Scenario: IndexedDB has records
- **WHEN** the modal opens and IndexedDB contains playfield records
- **THEN** all records are shown in the list immediately

#### Scenario: IndexedDB is empty
- **WHEN** the modal opens and IndexedDB contains no records
- **THEN** an empty-state message is shown

### Requirement: Each list item shows name, visibility badge, and ownership badge
Each row in the list SHALL display the playfield name, a badge indicating whether it is public or private, and a badge indicating whether it is owned by the authenticated user or external.

#### Scenario: User-owned playfield
- **WHEN** a playfield's `ownerId` matches the authenticated user's `userId`
- **THEN** the row shows a "Mine" ownership badge

#### Scenario: Externally-owned playfield
- **WHEN** a playfield's `ownerId` does not match the authenticated user's `userId`
- **THEN** the row shows an "External" ownership badge

#### Scenario: Public playfield
- **WHEN** a playfield's `isPublic` is `true`
- **THEN** the row shows a "Public" visibility badge

#### Scenario: Private playfield
- **WHEN** a playfield's `isPublic` is `false`
- **THEN** the row shows a "Private" visibility badge

### Requirement: Search activates at 3 or more characters
The page SHALL send a search query to the server when the user types 3 or more characters in the search box, debounced at 400 ms. While fewer than 3 characters are entered, the local list is shown.

#### Scenario: Query reaches 3 characters
- **WHEN** the user types 3 or more characters in the search box
- **THEN** a request is sent to `GET /playfields/public?q=<query>` after 400 ms debounce
- **THEN** the server results replace the local list

#### Scenario: Query is fewer than 3 characters
- **WHEN** the search box contains fewer than 3 characters (including empty)
- **THEN** no server request is sent
- **THEN** the local IndexedDB list is displayed

#### Scenario: Search is cleared
- **WHEN** the user clears the search box
- **THEN** the local IndexedDB list is restored

### Requirement: Single-row selection
The player SHALL be able to tap a row to select it. Only one row can be selected at a time; tapping another row deselects the previous one.

#### Scenario: Tap unselected row
- **WHEN** the player taps a row that is not currently selected
- **THEN** that row becomes selected and is visually highlighted
- **THEN** any previously selected row is deselected

#### Scenario: Tap already-selected row
- **WHEN** the player taps the currently selected row
- **THEN** the row is deselected

### Requirement: Confirm button enabled only when a playfield is selected
A "Select" button at the bottom of the page SHALL be disabled when no row is selected and enabled when exactly one row is selected.

#### Scenario: No selection
- **WHEN** no row is selected
- **THEN** the "Select" button is disabled

#### Scenario: Row selected
- **WHEN** a row is selected
- **THEN** the "Select" button is enabled

### Requirement: Confirm dismisses modal with selected playfield
Pressing the enabled "Select" button SHALL dismiss the modal and return the selected `PlayFieldRecord` to the caller.

#### Scenario: Player confirms selection
- **WHEN** the player presses the enabled "Select" button
- **THEN** the modal dismisses with `data: { playfield: <selected PlayFieldRecord> }`

### Requirement: Cancel dismisses modal with no data
The page SHALL provide a way to cancel, dismissing the modal without returning a playfield.

#### Scenario: Player cancels
- **WHEN** the player dismisses the modal via the cancel button or swipe-down gesture
- **THEN** the modal dismisses with no data payload

