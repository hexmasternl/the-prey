## MODIFIED Requirements

### Requirement: Display playfield metadata

The client SHALL present a dedicated detail page for a single playfield. The page SHALL display the playfield's name. When the authenticated user is the owner of the playfield, the page SHALL include a private/public visibility toggle reflecting the playfield's current visibility state. When the user changes the toggle, the client SHALL persist the new visibility state to the server immediately and SHALL roll back the toggle to its previous state if the server returns an error. When the authenticated user is NOT the owner of the playfield, the visibility toggle SHALL NOT be displayed.

#### Scenario: Page loads with playfield data

- **WHEN** an authenticated user navigates to `/playfields/:id` for a playfield that exists
- **THEN** the page displays the playfield name

#### Scenario: Owner sees visibility toggle

- **WHEN** the authenticated user is the owner of the playfield
- **THEN** the visibility toggle is visible and set to the current visibility state

#### Scenario: Non-owner does not see visibility toggle

- **WHEN** the authenticated user is NOT the owner of the playfield
- **THEN** the visibility toggle is not present in the page

#### Scenario: Toggling visibility commits the change

- **WHEN** the owner flips the visibility toggle
- **THEN** the client sends a request to update the visibility and the toggle remains in the new position on success

#### Scenario: Failed visibility update rolls back the toggle

- **WHEN** the owner flips the visibility toggle and the server responds with an error
- **THEN** the toggle reverts to its previous state and an error message is shown

#### Scenario: Playfield not found

- **WHEN** an authenticated user navigates to `/playfields/:id` for an identifier that does not exist
- **THEN** the page shows a "not found" message and offers navigation back

### Requirement: Set Area entry point

The page SHALL display a **Set Area** button only when the authenticated user is the owner of the playfield. Tapping the button SHALL navigate the user to the area-drawing flow for the playfield.

#### Scenario: Owner sees Set Area button

- **WHEN** the authenticated user is the owner of the playfield
- **THEN** the Set Area button is visible on the page

#### Scenario: Non-owner does not see Set Area button

- **WHEN** the authenticated user is NOT the owner of the playfield
- **THEN** the Set Area button is not present in the page

#### Scenario: Tapping Set Area navigates to the drawing flow

- **WHEN** the owner taps the Set Area button
- **THEN** the app navigates to the area-drawing route for that playfield
