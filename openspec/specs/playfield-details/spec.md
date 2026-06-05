# playfield-details Specification

## Purpose
TBD - created by archiving change playfield-details-page. Update Purpose after archive.
## Requirements
### Requirement: Display playfield metadata

The client SHALL present a dedicated detail page for a single playfield. The page SHALL display the playfield's name. The page SHALL include a private/public visibility toggle reflecting the playfield's current visibility state. When the user changes the toggle, the client SHALL persist the new visibility state to the server immediately and SHALL roll back the toggle to its previous state if the server returns an error.

#### Scenario: Page loads with playfield data

- **WHEN** an authenticated user navigates to `/playfields/:id` for a playfield that exists
- **THEN** the page displays the playfield name and a toggle set to the current visibility state

#### Scenario: Toggling visibility commits the change

- **WHEN** the user flips the visibility toggle
- **THEN** the client sends a PATCH request to update the visibility and the toggle remains in the new position on success

#### Scenario: Failed visibility update rolls back the toggle

- **WHEN** the user flips the visibility toggle and the server responds with an error
- **THEN** the toggle reverts to its previous state and an error message is shown

#### Scenario: Playfield not found

- **WHEN** an authenticated user navigates to `/playfields/:id` for an identifier that does not exist
- **THEN** the page shows a "not found" message and offers navigation back

### Requirement: Display playfield area map

The page SHALL display a non-interactive preview map showing the playfield's polygon. When the playfield has one or more GPS coordinate vertices, the map SHALL render the polygon with a semi-transparent fill, centre the viewport on the polygon's centroid, and zoom to a level where the entire polygon is visible within the map tile area. When the playfield has no GPS coordinate vertices, the map SHALL centre on the device's current GPS position at a default street-level zoom; if the device position is unavailable, the map SHALL render at a world-level zoom centred at `[0, 0]`.

#### Scenario: Map shows the polygon centred and fitted

- **WHEN** the playfield has at least three GPS coordinate vertices
- **THEN** the map displays the polygon with a semi-transparent fill and adjusts the zoom level so the entire polygon fits within the visible map area

#### Scenario: Map centres on device location when no polygon exists

- **WHEN** the playfield has no GPS coordinate vertices and the device grants location access
- **THEN** the map centres on the device's current GPS position at a default street-level zoom with no polygon drawn

#### Scenario: Map renders at world zoom when location is unavailable

- **WHEN** the playfield has no GPS coordinate vertices and the device denies or is unable to provide location access
- **THEN** the map renders centred at latitude 0, longitude 0 at a world-level zoom with no polygon drawn

### Requirement: Set Area entry point

The page SHALL display a **Set Area** button. Tapping the button SHALL navigate the user to the area-drawing flow for the playfield. The button SHALL be present regardless of whether the playfield already has a defined polygon area.

#### Scenario: Set Area button is always visible

- **WHEN** a user views the detail page of any playfield
- **THEN** the Set Area button is visible on the page

#### Scenario: Tapping Set Area navigates to the drawing flow

- **WHEN** the user taps the Set Area button
- **THEN** the app navigates to the area-drawing route for that playfield

