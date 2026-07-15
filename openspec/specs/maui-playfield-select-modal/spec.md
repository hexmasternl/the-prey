# maui-playfield-select-modal Specification

## Purpose
Provide the MAUI playfield-selection modal — opening it over the current page and awaiting a result; the cache-first own-playfields default list; the 3-character-minimum, 300 ms-debounced search that merges the user's own (private + public) playfields with matching public playfields and de-duplicates by id; the per-row name + `PUBLIC`/`PRIVATE` badge; single-row selection with a `SELECT` button enabled only when a row is selected; dismissing with the selected playfield on confirm and with nothing on cancel; and the loading / empty / no-results / error states.

## Requirements
### Requirement: Open the modal and await a selection

A caller SHALL be able to open the playfield-selection modal over the current page and asynchronously receive the chosen playfield or an indication that the caller cancelled, without the caller taking any dependency on MAUI or Shell types.

#### Scenario: Caller opens the modal

- **WHEN** a caller invokes the playfield-select navigator
- **THEN** the modal is presented over the current page
- **AND** the call resolves later with the selected playfield (on confirm) or with nothing (on cancel)

#### Scenario: Modal returns the confirmed playfield

- **WHEN** the player confirms a selection in the modal
- **THEN** the modal is dismissed
- **AND** the caller's awaited result is the selected `PlayFieldSummary`

#### Scenario: Modal returns nothing on cancel

- **WHEN** the player cancels the modal (cancel action or system back/dismiss)
- **THEN** the modal is dismissed
- **AND** the caller's awaited result is empty, leaving the caller's prior selection unchanged

### Requirement: Show the user's own playfields by default

On open, the modal SHALL display the current user's own playfields as the initial list, served cache-first from the local playfield cache and then reconciled with the backend in the background, so the list is not blank while online.

#### Scenario: Cached own playfields exist

- **WHEN** the modal opens and the local playfield cache contains the user's playfields
- **THEN** those cached playfields are shown immediately as the default list
- **AND** the list is refreshed in the background from `GET /playfields` and reconciled

#### Scenario: No cached playfields and the refresh returns items

- **WHEN** the modal opens with an empty cache and the background refresh returns the user's playfields
- **THEN** a loading indication is shown until the refresh completes
- **AND** the returned playfields are then displayed

#### Scenario: User has no playfields

- **WHEN** the modal opens with an empty cache and the refresh returns no playfields
- **THEN** an empty-state message is shown

#### Scenario: Refresh fails but a cached list is present

- **WHEN** the background refresh fails and cached playfields are already shown
- **THEN** the cached list remains on screen and no error is surfaced

#### Scenario: Refresh fails with nothing cached

- **WHEN** the background refresh fails and there was nothing cached to fall back to
- **THEN** an error state is shown

### Requirement: Search merges private and public matches at three or more characters

The modal SHALL show the default own-playfields list while the trimmed search query is fewer than three characters, and at three or more characters SHALL retrieve matching playfields — the user's own (private and public) playfields plus matching public playfields from the backend — merged and de-duplicated by id.

#### Scenario: Query is fewer than three characters

- **WHEN** the trimmed search query contains fewer than three characters (including empty)
- **THEN** no search request is sent
- **AND** the default own-playfields list is shown

#### Scenario: Query reaches three characters

- **WHEN** the trimmed search query contains three or more characters
- **THEN** the user's own playfields are filtered locally (case-insensitive contains) for matches
- **AND** matching public playfields are retrieved from `GET /playfields/public?q=<query>`
- **AND** the two result sets are merged and de-duplicated by id, with own entries kept over public duplicates
- **AND** the merged results replace the displayed list

#### Scenario: Search returns no matches

- **WHEN** a search for three or more characters produces no matching playfields
- **THEN** a no-results message is shown

#### Scenario: Search is cleared

- **WHEN** the player clears the search box
- **THEN** the default own-playfields list is restored

#### Scenario: Search fails

- **WHEN** the public search request fails with a transient or unexpected error
- **THEN** an error state is shown for the search

### Requirement: Debounce the search at 300 ms with supersession

The modal SHALL debounce the search by 300 milliseconds and SHALL cancel any pending or in-flight search when a newer keystroke arrives, so that only the latest query's results are applied.

#### Scenario: Rapid typing sends a single request

- **WHEN** the player types several characters in quick succession
- **THEN** no request is sent while typing continues
- **AND** a single request for the final query is sent once typing pauses for 300 ms

#### Scenario: A newer query supersedes an in-flight search

- **WHEN** a new keystroke arrives while a previous search is pending or in flight
- **THEN** the previous search is cancelled
- **AND** only the latest query's results are applied

### Requirement: Each row shows name and visibility badge

Each row in the list SHALL display the playfield name and a badge indicating whether the playfield is `PUBLIC` or `PRIVATE`.

#### Scenario: Public playfield row

- **WHEN** a listed playfield's visibility is public
- **THEN** the row shows the playfield name and a `PUBLIC` badge

#### Scenario: Private playfield row

- **WHEN** a listed playfield's visibility is private
- **THEN** the row shows the playfield name and a `PRIVATE` badge

### Requirement: Single-row selection

The player SHALL be able to select exactly one row at a time; selecting another row moves the selection, and re-selecting the selected row clears it.

#### Scenario: Select an unselected row

- **WHEN** the player taps a row that is not currently selected
- **THEN** that row becomes selected and is visually highlighted
- **AND** any previously selected row is deselected

#### Scenario: Re-tap the selected row

- **WHEN** the player taps the currently selected row
- **THEN** the row is deselected and no row is selected

### Requirement: SELECT enabled only when a row is selected

A `SELECT` button SHALL be disabled when no row is selected and enabled when exactly one row is selected.

#### Scenario: No selection

- **WHEN** no row is selected
- **THEN** the `SELECT` button is disabled

#### Scenario: Row selected

- **WHEN** a row is selected
- **THEN** the `SELECT` button is enabled

#### Scenario: Selection cleared

- **WHEN** the player deselects the selected row so no row is selected
- **THEN** the `SELECT` button becomes disabled again

### Requirement: Confirm dismisses the modal with the selected playfield

Pressing the enabled `SELECT` button SHALL dismiss the modal and return the selected playfield to the caller.

#### Scenario: Player confirms a selection

- **WHEN** the player presses the enabled `SELECT` button with a row selected
- **THEN** the modal is dismissed
- **AND** the selected `PlayFieldSummary` is returned to the caller

### Requirement: Graceful handling of an expired or denied session

The modal SHALL surface an error state rather than crashing when the session is expired or the backend denies the request, and SHALL trigger a re-acquisition of the access token on the next request.

#### Scenario: Unauthorized during the default load or search

- **WHEN** a load or search request returns unauthorized
- **THEN** the cached access token is invalidated so the next request re-acquires one
- **AND** an error state is shown rather than crashing
