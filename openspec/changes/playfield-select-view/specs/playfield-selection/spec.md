## ADDED Requirements

### Requirement: Playfield select view entry

The app SHALL provide a playfield select view that a calling page can navigate to in order to obtain exactly one selected playfield. On opening, the view SHALL reset any previous selection state so a stale selection from an earlier flow can never be returned.

#### Scenario: View opens with a clean selection state

- **WHEN** the playfield select view is opened
- **THEN** no playfield is marked as selected, the Select button is disabled, and any previously stored selection result is cleared

### Requirement: Default list shows local synced playfields

While the search box contains fewer than 3 characters, the view SHALL display the locally cached playfields. Playfields whose sync state is `true` SHALL be shown as selectable. The default list SHALL be rendered from the local cache without requiring a network call.

#### Scenario: Synced local playfields are listed

- **WHEN** the view opens and the local cache contains playfields with sync state `true`
- **THEN** those playfields are displayed in the list and can be tapped to select

#### Scenario: Default list works offline

- **WHEN** the view opens while the device has no network connectivity
- **THEN** the locally cached playfields are still displayed

### Requirement: Unsynced playfields can never be selected

A locally cached playfield whose sync state is `false` SHALL be displayed in a visually disabled state with an indication that it is not synchronized, and SHALL NOT be selectable, because the local and remote copies may differ. This rule SHALL apply in both the default list and search results.

#### Scenario: Unsynced playfield is shown disabled

- **WHEN** the list contains a locally cached playfield whose sync state is `false`
- **THEN** the playfield is rendered in a disabled style with a not-synchronized indication

#### Scenario: Tapping an unsynced playfield does not select it

- **WHEN** the user taps a playfield whose sync state is `false`
- **THEN** no selection is made, the Select button remains disabled, and the user is informed the playfield must be synchronized before it can be used

### Requirement: Hybrid search at three or more characters

When the search box contains at least 3 characters, the view SHALL execute a debounced search and display a hybrid result list combining: (a) locally cached playfields owned by the current user whose name matches the search text case-insensitively, and (b) public playfields returned by the server search. Results appearing in both sets SHALL be de-duplicated by playfield identifier, preferring the local copy. While the search box contains fewer than 3 characters, no server query SHALL be executed and the view SHALL show the default local list.

#### Scenario: Search merges local private and server public results

- **WHEN** the user enters at least 3 characters and both a matching local synced playfield and matching public playfields exist
- **THEN** the list shows the local private matches together with the server's public matches as a single list

#### Scenario: Duplicate results are de-duplicated

- **WHEN** a playfield owned by the current user appears both in the local cache and in the server's public search results
- **THEN** the playfield appears exactly once in the list

#### Scenario: Fewer than three characters shows the local list

- **WHEN** the search text drops below 3 characters
- **THEN** any in-flight server search is cancelled and the view shows the default local synced list

#### Scenario: Continued typing cancels the previous search

- **WHEN** the user keeps typing while a server search is in flight
- **THEN** the previous search is cancelled and a new debounced search is started for the latest text

#### Scenario: Server search failure degrades gracefully

- **WHEN** the server search fails or the device is offline while at least 3 characters are entered
- **THEN** the matching local playfields are still displayed together with a non-blocking error indication

### Requirement: Selecting a playfield and confirming

Tapping a selectable playfield SHALL mark it as the current selection and visually highlight it; tapping a different selectable playfield SHALL move the selection. The Select button SHALL be enabled only while a selectable playfield is selected. Confirming with the Select button SHALL store the selected playfield in the selection context and navigate back to the calling page, which can read the result from the context.

#### Scenario: Tapping a playfield enables the Select button

- **WHEN** the user taps a selectable playfield
- **THEN** the playfield is highlighted as selected and the Select button becomes enabled

#### Scenario: Selection moves to the most recently tapped playfield

- **WHEN** a playfield is already selected and the user taps a different selectable playfield
- **THEN** the highlight and selection move to the newly tapped playfield

#### Scenario: Confirming returns the selection to the caller

- **WHEN** the user taps the enabled Select button
- **THEN** the selected playfield is stored in the selection context and the view navigates back, allowing the calling page to read the selection

#### Scenario: Leaving without confirming returns no selection

- **WHEN** the user navigates back without tapping the Select button
- **THEN** the selection context carries no completed selection and the calling page treats it as a cancelled pick

### Requirement: Localized user-visible texts

All user-visible texts on the playfield select view (title, search placeholder, prompts, not-synchronized hint, Select button, empty and error states) SHALL be provided in both English and Dutch through the app's localization resources.

#### Scenario: Texts resolve through localization resources

- **WHEN** the view is rendered with the device language set to Dutch
- **THEN** all user-visible texts on the view appear in Dutch
