# maui-playfield-edit Specification

## Purpose
TBD - created by archiving change maui-playfield-update-existing. Update Purpose after archive.
## Requirements
### Requirement: Edit-playfield entry from the Private tab

Tapping a playfield item on the Private tab of the playfields list SHALL open the Edit Playfield page for that playfield, passing its id. The edit affordance SHALL apply only to the Private list (the user's own playfields), not the Public search results.

#### Scenario: Tapping a private item opens the edit page

- **WHEN** the user is on the Private tab and taps one of their playfield items
- **THEN** the Edit Playfield page opens for that playfield

#### Scenario: Public results are not editable

- **WHEN** the Public tab's search results are shown
- **THEN** tapping a result does not open the Edit Playfield page

### Requirement: Loading the playfield to edit

On opening, the Edit Playfield page SHALL fetch the full playfield by id — including its ordered polygon points and its `LastUpdatedOn` concurrency stamp — using an authenticated `GET /playfields/{id}` request, because the list summary does not carry those. Until the fetch completes the page SHALL show a loading state, and it SHALL handle failure without crashing.

#### Scenario: Playfield loaded

- **WHEN** the page opens and the backend returns the playfield with `200 OK`
- **THEN** the name field shows the current name, the toggle reflects the current visibility, the area is set to the loaded polygon, and the loaded `LastUpdatedOn` is retained for the later update

#### Scenario: Playfield no longer exists

- **WHEN** the load request returns `404 Not Found`
- **THEN** the page indicates the playfield no longer exists and does not crash, and no edit can be saved

#### Scenario: Unauthorized load

- **WHEN** the load request returns `401 Unauthorized`
- **THEN** the cached access token is invalidated and an error state is shown without crashing

#### Scenario: Load cannot complete

- **WHEN** the load request fails with a network error, times out, or returns an unexpected status
- **THEN** an error state is shown and the user may retry loading, without crashing

### Requirement: Edit Playfield page layout

The Edit Playfield page SHALL provide a name input, a Public/Private toggle, a Set Area button, and Cancel and Save actions, styled per the app's single-source-of-truth styling (no inline visual literals).

#### Scenario: Initial state after load

- **WHEN** the playfield has loaded successfully
- **THEN** the name, visibility toggle, and defined area reflect the loaded playfield and Save is disabled

### Requirement: Name-pattern gating of the Public/Private toggle

The Public/Private toggle SHALL be enabled only while the name matches the pattern `<country>, <city>, <free name>`: exactly three comma-separated non-empty parts where the first trimmed part is a country code of 2 or 3 uppercase letters (`^[A-Z]{2,3}$`). While the name does not match, the toggle SHALL be disabled and the visibility SHALL be Private.

#### Scenario: Valid name enables the toggle

- **WHEN** the name is `NL, Amsterdam, City park`
- **THEN** the Public/Private toggle is enabled and the user may switch it to Public

#### Scenario: Invalid name disables the toggle and forces Private

- **WHEN** the name is edited to a value that is not three comma-separated parts with a 2–3 uppercase-letter first part (e.g. `Amsterdam park` or `Nl, Amsterdam, Park`)
- **THEN** the Public/Private toggle is disabled and the visibility is Private

#### Scenario: Making a valid name invalid resets to Private

- **WHEN** the visibility was Public with a valid name and the user edits the name so it no longer matches the pattern
- **THEN** the toggle is disabled and the visibility reverts to Private

### Requirement: Set Area hand-off with the existing polygon

The Set Area button SHALL open the area editor pre-populated with the playfield's current polygon, with the map centred on the polygon's centroid, and SHALL receive the edited polygon when the editor is saved.

#### Scenario: Opening the area editor on the existing polygon

- **WHEN** the user taps Set Area
- **THEN** the area editor opens showing the current polygon, with the map centred on the centroid of that polygon

#### Scenario: Returning an edited area

- **WHEN** the area editor is saved with a polygon of at least 3 points
- **THEN** the Edit Playfield page holds that polygon as the new area

#### Scenario: Cancelling the area editor keeps the prior polygon

- **WHEN** the user cancels the area editor
- **THEN** the Edit Playfield page's held polygon is unchanged

### Requirement: Clearing the area in the editor

While editing the area the user SHALL be able to clear all points in a single action; after clearing, editing continues under the normal rules (a polygon reappears once at least 3 points exist again).

#### Scenario: Clear removes all points

- **WHEN** the user taps Clear in the area editor
- **THEN** all vertices are removed, any selection is cleared, and no polygon is drawn

#### Scenario: Save disabled after clearing until three points exist

- **WHEN** the area has been cleared and fewer than 3 vertices have been placed
- **THEN** the editor's Save action is disabled until at least 3 vertices exist again

### Requirement: Save enablement by change detection

Save SHALL be disabled by default and SHALL become enabled only when the current name, visibility, or polygon differs from the loaded playfield AND the current state is itself valid (name non-empty and a polygon of at least 3 points). Reverting all values to the loaded playfield SHALL disable Save again.

#### Scenario: No changes keeps Save disabled

- **WHEN** the playfield has loaded and the user has changed nothing
- **THEN** Save is disabled

#### Scenario: Changing the name enables Save

- **WHEN** the user edits the name to a non-empty value different from the loaded name while the area still has at least 3 points
- **THEN** Save is enabled

#### Scenario: Toggling visibility enables Save

- **WHEN** the user changes the Public/Private visibility from the loaded value while the state remains valid
- **THEN** Save is enabled

#### Scenario: Changing the area enables Save

- **WHEN** the user returns from the area editor with a polygon different from the loaded one (still at least 3 points)
- **THEN** Save is enabled

#### Scenario: Reverting changes disables Save

- **WHEN** the user changes a value and then restores every value to match the loaded playfield
- **THEN** Save is disabled again

#### Scenario: Dirty but invalid keeps Save disabled

- **WHEN** the user changes a value but the current state is invalid (name empty, or the polygon has fewer than 3 points)
- **THEN** Save remains disabled

### Requirement: Updating the playfield

On Save the app SHALL send an authenticated `PUT /playfields/{id}` request with the name, the chosen visibility (`IsPublic`), the polygon points, and the `LastUpdatedOn` the page loaded with; on success it SHALL close the page and update the corresponding item in the Private list in place.

#### Scenario: Successful update

- **WHEN** Save is tapped on a valid, changed playfield and the backend responds `200 OK`
- **THEN** the Edit Playfield page closes and the Private list item for that playfield reflects the updated name and visibility, in place, without a full reload

#### Scenario: Stale-write conflict

- **WHEN** the update responds `409 Conflict` because the playfield changed elsewhere since it was loaded
- **THEN** the page remains open, a stale-write error is shown, the local edits are not lost silently, and the user is offered to reload the current state rather than overwriting it

#### Scenario: Validation rejection

- **WHEN** the update responds `400`
- **THEN** the page remains open and a validation error is shown without losing the entered name or defined area

#### Scenario: Unauthorized session

- **WHEN** the update responds `401`
- **THEN** the cached access token is invalidated and an error state is shown without crashing

#### Scenario: Forbidden

- **WHEN** the update responds `403` because the caller is not the owner
- **THEN** an error state is shown and the playfield is not changed, without crashing

#### Scenario: Transient failure

- **WHEN** the update fails to complete (network or timeout) or returns an unexpected status
- **THEN** an error state is shown and the user may retry Save

### Requirement: Cancelling the edit

Cancel SHALL close the Edit Playfield page without sending an update and without altering the Private list.

#### Scenario: Cancel discards edits

- **WHEN** the user taps Cancel
- **THEN** the page closes, no update request is sent, and the Private list is unchanged

### Requirement: Client get-and-update API maps backend status codes

The client playfields API SHALL provide a get-by-id operation over `GET /playfields/{id}` returning the full playfield (id, name, visibility, ordered points, and `LastUpdatedOn`) and an update operation over `PUT /playfields/{id}` carrying `Name`, `IsPublic`, `Points`, and `LastUpdatedOn`. Both SHALL attach the access token as a bearer credential and map backend responses to results the view model can act on, without throwing: get maps `200`→success, `404`→not-found, `401`→unauthorized, other/failure→error; update maps `200`→updated, `409`→conflict (carrying the current playfield from the response body), `400`→validation, `401`→unauthorized, `403`→forbidden, `404`→not-found, and network/timeout/unexpected→error.

#### Scenario: Get status mapping

- **WHEN** the get operation receives `200`, `404`, `401`, or a network/timeout/unexpected response
- **THEN** it returns success (with the full playfield), not-found, unauthorized, or error respectively
- **AND** it does not throw

#### Scenario: Update status mapping

- **WHEN** the update operation receives `200`, `409`, `400`, `401`, `403`, `404`, or a network/timeout/unexpected response
- **THEN** it returns updated, conflict (with the current playfield), validation, unauthorized, forbidden, not-found, or error respectively
- **AND** it does not throw

