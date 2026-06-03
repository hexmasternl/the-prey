## ADDED Requirements

### Requirement: Playfields page has Private and Public tabs
The playfields page SHALL display two tabs: "Private" and "Public". The Private tab SHALL be active by default when the page is opened. Switching tabs SHALL not reset the state of the other tab.

#### Scenario: Page opens on Private tab by default
- **WHEN** the user navigates to the playfields page
- **THEN** the Private tab is active and the user's private playfields are displayed

#### Scenario: User switches to Public tab
- **WHEN** the user taps the Public tab
- **THEN** the Public tab becomes active and the Public tab content (search box) is shown; the Private list is hidden but its state is preserved

#### Scenario: User switches back to Private tab
- **WHEN** the user is on the Public tab and taps the Private tab
- **THEN** the Private tab becomes active with its previously loaded list intact; no new server fetch is triggered

### Requirement: Public tab shows a search box
The Public tab SHALL display a text input (search box) and a prompt message instructing the user to type at least 3 characters to search. No results SHALL be shown until a qualifying search has been executed.

#### Scenario: Public tab initial state
- **WHEN** the user first opens the Public tab
- **THEN** a search box is shown and a prompt message instructs the user to type at least 3 characters; no playfield results are displayed

### Requirement: Search requires at least 3 characters
The app SHALL NOT send a search request to the server while the search box contains fewer than 3 characters. If the user clears the search box or reduces the query below 3 characters after a search, the results list SHALL be cleared and the prompt message shown again.

#### Scenario: Fewer than 3 characters typed
- **WHEN** the user types 1 or 2 characters into the search box
- **THEN** no server request is made and no results are shown

#### Scenario: Query cleared after results shown
- **WHEN** the user deletes characters from the search box until fewer than 3 remain
- **THEN** the results list is cleared and the prompt message is shown again

### Requirement: Search is debounced
The app SHALL wait at least 400 milliseconds after the user stops typing before sending a search request. If the user types additional characters within that window, the timer SHALL reset and the previous pending request (if any) SHALL be cancelled.

#### Scenario: User pauses after typing
- **WHEN** the user types at least 3 characters and stops typing for 400 milliseconds
- **THEN** a single search request is sent to the server

#### Scenario: User continues typing within debounce window
- **WHEN** the user types characters in rapid succession
- **THEN** only one search request is sent, using the text present after the final pause; intermediate requests are not sent

### Requirement: Display search results
When the server returns results, the app SHALL display the list of matching public playfields. Each item SHALL show at minimum the playfield name and owner. A loading indicator SHALL be shown while the request is in progress.

#### Scenario: Search returns results
- **WHEN** the server responds with a non-empty list of public playfields
- **THEN** the results are displayed in a list and the loading indicator is hidden

#### Scenario: Search in progress
- **WHEN** a search request has been sent and the response has not yet arrived
- **THEN** a loading indicator is shown

### Requirement: Empty and error states for search
When a search returns no results, the app SHALL display an appropriate empty-state message. When the search request fails, an error message SHALL be shown. Neither result set SHALL be cached locally.

#### Scenario: Search returns no results
- **WHEN** the server returns an empty list for the search query
- **THEN** an empty-state message is displayed (e.g. "No public playfields found")

#### Scenario: Search request fails
- **WHEN** the search request fails due to a network error or server error
- **THEN** an error message is displayed and no partial results are shown

### Requirement: Navigate to read-only detail for a public playfield
Tapping a public playfield in the results list SHALL navigate to the playfield detail page in read-only mode. In read-only mode, no edit controls are active and no save action is available.

#### Scenario: User taps a public playfield
- **WHEN** the user taps a public playfield in the search results
- **THEN** the app navigates to the detail page for that playfield in read-only mode

#### Scenario: Read-only detail — edit controls disabled
- **WHEN** the detail page is opened in read-only mode
- **THEN** the name field, visibility toggle, and "Set Area" button are all disabled; the Save button is not shown; the page title reads "View Playfield"

### Requirement: Public playfields are not stored locally
Public playfield search results SHALL NOT be written to local storage. Navigating away from the Public tab or the page SHALL discard the results.

#### Scenario: Results discarded on navigation
- **WHEN** the user navigates away from the playfields page and returns
- **THEN** the Public tab shows the initial empty search state; no previous results are pre-loaded
