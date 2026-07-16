## ADDED Requirements

### Requirement: Create-playfield entry from the Private tab

The Private tab of the playfields list SHALL present a `+` action that opens the Create Playfield page.

#### Scenario: Plus action opens the create page

- **WHEN** the user is on the Private tab of the playfields list and taps the `+` action
- **THEN** the Create Playfield page is opened

#### Scenario: Plus action is available even when the list is empty

- **WHEN** the Private tab shows its empty state (the user owns no playfields)
- **THEN** the `+` action is still available and opens the Create Playfield page

### Requirement: Create Playfield page layout

The Create Playfield page SHALL provide a name input, a Public/Private toggle, a Define Area button, and Cancel and Save actions, styled per the app's single-source-of-truth styling (no inline visual literals).

#### Scenario: Initial state

- **WHEN** the Create Playfield page opens
- **THEN** the name is empty, the Public/Private toggle is disabled and set to Private, no area is defined, and Save is disabled

### Requirement: Name-pattern gating of the Public/Private toggle

The Public/Private toggle SHALL be enabled only while the name matches the pattern `<country>, <city>, <free name>`: exactly three comma-separated non-empty parts where the first trimmed part is a country code of 2 or 3 uppercase letters (`^[A-Z]{2,3}$`). While the name does not match, the toggle SHALL be disabled and the visibility SHALL be Private.

#### Scenario: Valid name enables the toggle

- **WHEN** the name is `NL, Amsterdam, City park`
- **THEN** the Public/Private toggle is enabled and the user may switch it to Public

#### Scenario: Three-letter country code is accepted

- **WHEN** the name is `FRA, Paris, The Mall`
- **THEN** the Public/Private toggle is enabled

#### Scenario: Invalid name disables the toggle and forces Private

- **WHEN** the name is `Amsterdam park` (fewer than three comma-separated parts) or the first part is not 2–3 uppercase letters (e.g. `Nl, Amsterdam, Park`)
- **THEN** the Public/Private toggle is disabled and the visibility is Private

#### Scenario: Making a valid name invalid resets to Private

- **WHEN** the user had set the toggle to Public with a valid name and then edits the name so it no longer matches the pattern
- **THEN** the toggle is disabled and the visibility reverts to Private

### Requirement: Define Area hand-off

The Define Area button SHALL open the area editor, passing any already-defined polygon, and SHALL receive the resulting polygon when the editor is saved.

#### Scenario: Opening the area editor

- **WHEN** the user taps Define Area
- **THEN** the area editor opens centred for drawing, pre-populated with the current polygon if one was already defined

#### Scenario: Returning a saved area

- **WHEN** the area editor is saved with a polygon of at least 3 points
- **THEN** the Create Playfield page holds that polygon and indicates that an area has been defined

#### Scenario: Cancelling the area editor keeps the prior polygon

- **WHEN** the user cancels the area editor
- **THEN** the Create Playfield page's held polygon is unchanged

### Requirement: Save enablement

Save SHALL be enabled only when the name is non-empty and a polygon of at least 3 points has been defined.

#### Scenario: Save disabled without an area

- **WHEN** the name is non-empty but no area (or an area of fewer than 3 points) has been defined
- **THEN** Save is disabled

#### Scenario: Save disabled without a name

- **WHEN** a polygon of at least 3 points is defined but the name is empty
- **THEN** Save is disabled

#### Scenario: Save enabled

- **WHEN** the name is non-empty and a polygon of at least 3 points is defined
- **THEN** Save is enabled

### Requirement: Creating the playfield

On Save the app SHALL send an authenticated `POST /playfields` request with the name, the chosen visibility (`IsPublic`), and the polygon points; on success it SHALL close the page and append the created playfield to the Private list.

#### Scenario: Successful creation

- **WHEN** Save is tapped with a valid name and a polygon of at least 3 points and the backend responds `201 Created`
- **THEN** the Create Playfield page closes and the created playfield (its name and visibility badge) appears in the Private list

#### Scenario: Validation rejection

- **WHEN** the backend responds `400` to the create request
- **THEN** the page remains open and a validation error is shown without losing the entered name or defined area

#### Scenario: Unauthorized session

- **WHEN** the create request responds `401`
- **THEN** the cached access token is invalidated and an error state is shown without crashing

#### Scenario: Transient failure

- **WHEN** the create request fails to complete (network or timeout) or returns an unexpected status
- **THEN** an error state is shown and the user may retry Save

### Requirement: Cancelling creation

Cancel SHALL close the Create Playfield page without creating anything and without altering the list.

#### Scenario: Cancel discards the draft

- **WHEN** the user taps Cancel
- **THEN** the page closes, no create request is sent, and the Private list is unchanged
