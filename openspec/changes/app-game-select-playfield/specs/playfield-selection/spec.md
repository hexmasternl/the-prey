# Spec Delta

## ADDED Requirements

### Requirement: Selection screen has a Private and a Public tab
The playfield selection screen SHALL show the same two tabs as the playfields screen, "Private" and "Public". The Private tab SHALL be selected every time the selection screen is opened. The selected tab SHALL survive a configuration change such as a rotation.

#### Scenario: Private tab is selected on open
- **WHEN** a flow opens the playfield selection screen
- **THEN** the screen opens with the Private tab selected and its content shown

#### Scenario: Tab survives rotation
- **WHEN** the Public tab is selected and the device is rotated
- **THEN** the Public tab is still selected and the search text is kept

#### Scenario: Tab labels follow the device language
- **WHEN** the device language is Dutch
- **THEN** the tabs read "Privé" and "Openbaar"

### Requirement: Private tab lists the player's own playfields for selection
The Private tab SHALL download the playfields owned by the signed-in player from the server and list them. While downloading for the first time it SHALL show a loading indicator. When the player owns no playfields it SHALL say so and point to Playfields in the main menu to create one. When the download fails it SHALL show an error message and a retry action that downloads the list again.

#### Scenario: Own playfields are shown
- **WHEN** the Private tab is shown and the server returns two playfields owned by the player
- **THEN** both playfields are listed

#### Scenario: Loading indicator while downloading
- **WHEN** the Private tab is shown and the download has not completed yet
- **THEN** a loading indicator is shown instead of the list

#### Scenario: No playfields yet
- **WHEN** the server returns an empty list
- **THEN** the Private tab shows a message that the player has no playfields yet and can create one from Playfields in the main menu
- **THEN** no create button is shown

#### Scenario: Download fails and is retried
- **WHEN** the download fails
- **THEN** an error message and a retry action are shown
- **WHEN** the player taps retry
- **THEN** the list is downloaded again

### Requirement: Public tab searches public playfields for selection
The Public tab SHALL show a search field that receives focus, opening the keyboard, when the tab is selected. Once the trimmed text has 3 or more characters, the screen SHALL search the server's public playfields 366 ms after the last change; a newer search SHALL supersede one still in flight. With fewer than 3 characters it SHALL send nothing and show a hint. It SHALL show a no-results message and a search error message when applicable.

#### Scenario: Search after the debounce
- **WHEN** the player types "von" on the Public tab and stops typing
- **THEN** after 366 ms `GET /playfields/public?q=von` is sent and the matching public playfields are listed

#### Scenario: Short query shows the hint
- **WHEN** the search field contains fewer than 3 characters after trimming
- **THEN** no request is sent and a hint to type at least 3 characters is shown

#### Scenario: Stale results are ignored
- **WHEN** a search for "vond" is still running and the player changes the text to "vondel"
- **THEN** only the results for "vondel" are shown

#### Scenario: No public playfield matches
- **WHEN** the server returns no playfields for the query
- **THEN** a message says no public playfields match the search

### Requirement: Rows show playfield details and are read-only
Each row on both tabs SHALL show the playfield's name, a Private or Public visibility badge, the date it was last updated and, when known, its centre coordinates. Rows on the selection screen SHALL NOT offer swipe, delete, create or opening the playfield's details; a tap only changes the selection.

#### Scenario: Row details are shown
- **WHEN** a playfield named "Vondelpark", public, last updated 20 October 2026, is listed
- **THEN** its row shows the name, a Public badge and the updated date in the device's locale

#### Scenario: No delete on the selection screen
- **WHEN** the player swipes a row on the Private tab to the left
- **THEN** no Delete button is revealed and the playfield is not deleted

#### Scenario: Tapping does not open the details
- **WHEN** the player taps a row
- **THEN** the selection screen stays open and the playfield's details are not opened

### Requirement: Selection is kept across tabs and rotation
One playfield selection SHALL span both tabs. Switching tabs, changing the search text or rotating the device SHALL NOT clear the selection, and the selected playfield's name SHALL stay visible in the bottom bar even when its row is not on screen.

#### Scenario: Selection survives a tab switch
- **WHEN** the player selects a playfield on the Private tab and switches to the Public tab
- **THEN** the Select button is still enabled and the bottom bar still shows that playfield's name

#### Scenario: Selection survives a new search
- **WHEN** the player selects a public search result and changes the search so it is no longer listed
- **THEN** the playfield stays selected

#### Scenario: Selection survives rotation
- **WHEN** a playfield is selected and the device is rotated
- **THEN** the same playfield is still selected and highlighted

### Requirement: Back acts as cancel
Leaving the selection screen with the system Back action SHALL behave exactly like the Cancel button.

#### Scenario: Player presses Back
- **WHEN** a playfield is selected and the player presses the system Back action
- **THEN** the selection screen closes and no playfield is returned to the caller

## MODIFIED Requirements

### Requirement: Single-row selection
The player SHALL be able to tap a row to select it. Only one row can be selected at a time; tapping another row deselects the previous one. The selected row SHALL be visually highlighted using the design-system's signal colour and SHALL be announced as selected by screen readers.

#### Scenario: Tap unselected row
- **WHEN** the player taps a row that is not currently selected
- **THEN** that row becomes selected and is visually highlighted
- **THEN** any previously selected row is deselected

#### Scenario: Tap already-selected row
- **WHEN** the player taps the currently selected row
- **THEN** the row is deselected

#### Scenario: Screen reader announces the selection
- **WHEN** a screen reader focuses the selected row
- **THEN** it announces the row as selected

### Requirement: Confirm button enabled only when a playfield is selected
A bottom bar on the selection screen SHALL show a "Cancel" and a "Select" button side by side. The "Select" button SHALL be disabled when no row is selected and enabled when exactly one row is selected. While a playfield is selected, the bar SHALL show its name.

#### Scenario: No selection
- **WHEN** no row is selected
- **THEN** the "Select" button is disabled and the "Cancel" button is enabled

#### Scenario: Row selected
- **WHEN** a row is selected
- **THEN** the "Select" button is enabled
- **THEN** the bottom bar shows the selected playfield's name

#### Scenario: Buttons follow the device language
- **WHEN** the device language is Dutch
- **THEN** the buttons read "Annuleren" and "Kiezen"

### Requirement: Confirm dismisses modal with selected playfield
Pressing the enabled "Select" button SHALL close the selection screen and return the selected playfield's id, name and visibility to the flow that opened it.

#### Scenario: Player confirms selection
- **WHEN** the player selects "Vondelpark" and presses the enabled "Select" button
- **THEN** the selection screen closes
- **THEN** the opening flow receives the id, the name "Vondelpark" and the visibility of that playfield

### Requirement: Cancel dismisses modal with no data
The selection screen SHALL provide a "Cancel" button that closes it without returning a playfield, whether or not a playfield is selected.

#### Scenario: Player cancels
- **WHEN** the player presses "Cancel"
- **THEN** the selection screen closes
- **THEN** the opening flow receives no playfield and keeps any playfield it had chosen before

## REMOVED Requirements

### Requirement: Display local playfields on open
**Reason**: Described the removed Ionic client's IndexedDB cache. The native app keeps no local playfield store; the Private tab downloads the player's own playfields instead.
**Migration**: See "Private tab lists the player's own playfields for selection".

### Requirement: Each list item shows name, visibility badge, and ownership badge
**Reason**: The ownership badge is redundant now that own playfields (Private tab) and public search results (Public tab) are on separate tabs, and the rows match the playfields screen.
**Migration**: See "Rows show playfield details and are read-only".

### Requirement: Search activates at 3 or more characters
**Reason**: Described the removed client's 400 ms debounce and fallback to the local list. The Public tab now behaves exactly like the playfields screen's (366 ms, hint below 3 characters).
**Migration**: See "Public tab searches public playfields for selection".
