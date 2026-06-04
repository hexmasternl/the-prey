## ADDED Requirements

### Requirement: Public playfield search UI

The Public tab on the Playfields list page SHALL display an `ion-searchbar` above the results. When the search input is empty or contains fewer than 3 characters, the list area SHALL display a prompt instructing the user to type at least 3 characters. The searchbar input SHALL be debounced (≥ 300 ms) before triggering a search request. When the input reaches 3 or more characters and the debounce interval elapses, the client SHALL call `GET /playfields/public?q=<text>` and render the returned results. If a new search supersedes an in-flight request, the earlier request SHALL be cancelled.

#### Scenario: Search box displayed on Public tab

- **WHEN** the user selects the Public segment tab
- **THEN** an `ion-searchbar` is visible at the top of the content area

#### Scenario: Fewer than 3 characters shows prompt

- **WHEN** the search input contains 0–2 characters
- **THEN** no results list is shown and a hint message is displayed

#### Scenario: Search fires after debounce with 3+ characters

- **WHEN** the user types a query of 3 or more characters and 300 ms pass without further input
- **THEN** the client sends a request to `GET /playfields/public?q=<text>` and renders the returned list

#### Scenario: In-flight request cancelled on new input

- **WHEN** the user types a new query before the previous search response arrives
- **THEN** the earlier request is cancelled and only the latest response is rendered

#### Scenario: Results list displayed

- **WHEN** the server returns one or more matching public playfields
- **THEN** each result is displayed as a tappable `ion-item` showing the playfield name

#### Scenario: Empty results state

- **WHEN** the server returns an empty list for a valid search query
- **THEN** the page displays an "No public playfields found" message

#### Scenario: Tapping a result navigates to detail

- **WHEN** the user taps a result item
- **THEN** the app navigates to `/playfields/:id` for that playfield
