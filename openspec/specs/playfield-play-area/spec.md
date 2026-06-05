# playfield-play-area Specification

## Purpose
TBD - created by archiving change playfield-play-area. Update Purpose after archive.
## Requirements
### Requirement: Full-screen interactive map

The page SHALL render a full-screen map tile layer using OpenStreetMap. The map SHALL support pinch-to-zoom and tap-drag panning as standard Leaflet gestures. The map SHALL disable the Leaflet legacy tap handler (`tap: false`) to avoid conflicts with Ionic's native gesture system.

#### Scenario: Page opens with map filling the screen

- **WHEN** the user navigates to `/playfields/:id/area`
- **THEN** a full-screen interactive map is displayed occupying the entire page content area

#### Scenario: Map centres on existing polygon centroid

- **WHEN** the playfield already has at least one GPS coordinate vertex
- **THEN** the map centres on the centroid of the existing coordinates, existing points are drawn as green circle markers, and the polygon is drawn if there are at least 3 vertices

#### Scenario: Map centres on device location when no polygon exists

- **WHEN** the playfield has no GPS coordinate vertices and the device grants location access
- **THEN** the map centres on the device's current GPS position at a default street-level zoom with no markers drawn

#### Scenario: Map falls back to world zoom when location is unavailable

- **WHEN** the playfield has no GPS coordinate vertices and the device denies or is unable to provide location access
- **THEN** the map renders centred at latitude 0, longitude 0 at a world-level zoom with no markers drawn

### Requirement: Add GPS points by tapping the map

The page SHALL add a green GPS point marker at the tapped map coordinate each time the user taps the map. Tapping SHALL be additive — each tap appends a new point to the ordered list without removing previous points. Each point SHALL be rendered as a green circle marker.

#### Scenario: First tap adds a single marker

- **WHEN** the user taps a location on the map
- **THEN** a green circle marker appears at that GPS coordinate and is added to the internal point list

#### Scenario: Subsequent taps add additional markers

- **WHEN** the user taps additional locations on the map
- **THEN** each new tap adds another green circle marker at the tapped coordinate without removing previous markers

### Requirement: Live polygon drawn from points

When the internal point list contains at least 3 GPS coordinates the page SHALL draw a green semi-transparent polygon connecting all points in order. The polygon SHALL update immediately each time a new point is added. When fewer than 3 points exist no polygon SHALL be rendered.

#### Scenario: Polygon appears after third point is added

- **WHEN** the user has tapped exactly 3 points on the map
- **THEN** a green semi-transparent polygon is drawn connecting all three points

#### Scenario: Polygon updates as more points are added

- **WHEN** the user taps additional points after the polygon is already visible
- **THEN** the polygon immediately redraws to include all points including the newly added one

#### Scenario: No polygon shown with fewer than 3 points

- **WHEN** the user has added 0, 1, or 2 points to the map
- **THEN** no polygon is drawn; only individual circle markers are visible

### Requirement: Save the polygon area

The page SHALL display a **Save** button. The button SHALL be disabled when fewer than 3 points have been added. When tapped, the client SHALL send the ordered GPS coordinate list to the server via `PATCH /playfields/:id` and then close the page, returning the user to the Playfield Details page. If the server returns an error, the page SHALL display an error toast and remain open.

#### Scenario: Save button is disabled with fewer than 3 points

- **WHEN** the user has added 0, 1, or 2 points to the map
- **THEN** the Save button is visible but disabled

#### Scenario: Save button is enabled with 3 or more points

- **WHEN** the user has added at least 3 points to the map
- **THEN** the Save button is enabled and tappable

#### Scenario: Successful save closes the page

- **WHEN** the user taps Save and the server responds with a success status
- **THEN** the page closes and the user is returned to the Playfield Details page with the updated area

#### Scenario: Failed save shows error and keeps the page open

- **WHEN** the user taps Save and the server responds with an error
- **THEN** an error toast is displayed and the page remains open with all markers and the polygon intact

### Requirement: Cancel without saving

The page SHALL display a **Cancel** button. Tapping Cancel SHALL close the page and return the user to the Playfield Details page without sending any data to the server. The playfield's GPS coordinates SHALL remain unchanged.

#### Scenario: Tapping Cancel discards all changes

- **WHEN** the user taps the Cancel button regardless of how many points have been added
- **THEN** the page closes, the user is returned to the Playfield Details page, and the playfield's area coordinates are unchanged on the server

