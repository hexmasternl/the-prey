## ADDED Requirements

### Requirement: Open in create mode when no playfield id is provided
When the details page is opened without a playfield id, the app SHALL initialise an empty form with a blank name, the visibility defaulting to private, and no coordinates. The page title SHALL indicate "New Playfield".

#### Scenario: Create mode opens with empty form
- **WHEN** the user navigates to the details page without a playfield id
- **THEN** the name field is empty, the visibility toggle is set to private, no coordinates are loaded, and the page title reads "New Playfield"

### Requirement: Open in edit mode when a playfield id is provided
When the details page is opened with a playfield id, the app SHALL load the playfield from local cache and populate the name, visibility, and coordinates. The page title SHALL indicate "Edit Playfield".

#### Scenario: Edit mode loads existing playfield
- **WHEN** the user navigates to the details page with a valid playfield id
- **THEN** the name field, visibility toggle, and coordinate set are pre-populated from the cached playfield data and the page title reads "Edit Playfield"

#### Scenario: Edit mode — playfield not found in cache
- **WHEN** the user navigates to the details page with an id that does not exist in the local cache
- **THEN** the app displays an error message and navigates back to the playfields list

### Requirement: Name validation
The name field SHALL enforce a minimum of 5 characters. The Save button SHALL remain disabled while the name length is below this threshold. An inline validation message SHALL be shown when the field loses focus with insufficient content.

#### Scenario: Name too short
- **WHEN** the user enters fewer than 5 characters in the name field and the field loses focus
- **THEN** an inline validation message is displayed and the Save button remains disabled

#### Scenario: Name meets minimum length
- **WHEN** the name field contains at least 5 characters
- **THEN** the name validation message is hidden (assuming coordinate validation also passes)

### Requirement: Public/Private visibility toggle
The details form SHALL include a toggle (switch or checkbox) that controls whether the playfield is public or private. The default for a new playfield SHALL be private.

#### Scenario: Toggle visibility
- **WHEN** the user changes the visibility toggle
- **THEN** the playfield's visibility state is updated in the form and reflected on save

### Requirement: Mini-map preview with coordinate shape
When at least one coordinate exists, the mini-map SHALL display a polygon drawn from the current coordinate set, scaled to fit the bounds of the shape.

#### Scenario: Coordinates exist — polygon shown
- **WHEN** the details page loads or coordinates are updated and at least one coordinate is set
- **THEN** the mini-map renders a polygon connecting all coordinates, fit to the map bounds

### Requirement: Mini-map centered on user location when no coordinates exist
When no coordinates are set, the mini-map SHALL request the device's current location and center the map there. If location permission is denied or unavailable, the map SHALL display a default view and show a notice that the location could not be determined.

#### Scenario: No coordinates, location available
- **WHEN** the details page opens in create mode with no coordinates and location permission is granted
- **THEN** the mini-map is centered on the user's current location

#### Scenario: No coordinates, location permission denied
- **WHEN** the details page opens in create mode with no coordinates and location permission is denied
- **THEN** the mini-map shows a default view and a notice is displayed explaining location could not be determined

### Requirement: Navigate to area editor via Set Area button
Tapping the "Set Area" button SHALL pass the current coordinates to the shared editing context and navigate to the area editor page. On return, the updated coordinates SHALL be read from the shared context and the mini-map SHALL refresh.

#### Scenario: User taps Set Area and returns with updated coordinates
- **WHEN** the user taps "Set Area", modifies coordinates in the area editor, and navigates back
- **THEN** the mini-map updates to show the new shape and the coordinate count is updated for Save validation

#### Scenario: User taps Set Area and returns without changes
- **WHEN** the user taps "Set Area" and navigates back without modifying coordinates
- **THEN** the mini-map and coordinate state remain unchanged

### Requirement: Save button enabled only when form is valid
The Save button SHALL be enabled only when the name contains at least 5 characters AND at least 3 coordinates have been set.

#### Scenario: Form not valid — Save disabled
- **WHEN** the name is fewer than 5 characters or fewer than 3 coordinates are set
- **THEN** the Save button is disabled

#### Scenario: Form valid — Save enabled
- **WHEN** the name contains at least 5 characters and at least 3 coordinates are set
- **THEN** the Save button is enabled

### Requirement: Save persists locally and to server
When the user taps Save, the app SHALL write the playfield to local cache and then POST (create) or PUT (edit) to the server. On server success, the app SHALL navigate back to the playfields list. On server failure, the app SHALL display an error alert and remain on the details page; the local cache write is retained.

#### Scenario: Save succeeds (create)
- **WHEN** the user taps Save on a new playfield and the server returns success
- **THEN** the playfield is written to local cache, POSTed to the server, and the app navigates back to the playfields list

#### Scenario: Save succeeds (edit)
- **WHEN** the user taps Save on an existing playfield and the server returns success
- **THEN** the playfield is updated in local cache, PUT to the server, and the app navigates back to the playfields list

#### Scenario: Save fails — server error
- **WHEN** the user taps Save and the server returns a non-success response
- **THEN** the playfield is retained in local cache, an error alert is shown, and the user remains on the details page

#### Scenario: Save fails — 401 Unauthorized
- **WHEN** the user taps Save and the server returns 401
- **THEN** the app navigates the user to the login page
