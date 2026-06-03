## ADDED Requirements

### Requirement: Open with existing coordinates loaded
When the area editor is opened, it SHALL read coordinates from `PlayfieldEditingContext.CurrentCoordinates` and display any existing points on the map. If coordinates exist, the map SHALL be fitted to their bounds. If no coordinates exist, the map SHALL be centered on the user's current location (or a default position if location is unavailable).

#### Scenario: Opens with existing coordinates
- **WHEN** the area editor page opens and `PlayfieldEditingContext.CurrentCoordinates` contains at least one coordinate
- **THEN** the map displays the existing points and fits the viewport to their bounds

#### Scenario: Opens with no coordinates, location available
- **WHEN** the area editor page opens, `PlayfieldEditingContext.CurrentCoordinates` is empty, and geolocation permission is granted
- **THEN** the map is centered on the user's current location with a reasonable default zoom level

#### Scenario: Opens with no coordinates, location unavailable
- **WHEN** the area editor page opens, `PlayfieldEditingContext.CurrentCoordinates` is empty, and geolocation is unavailable or denied
- **THEN** the map opens at a default position and zoom level

### Requirement: Cancel closes without saving
The Cancel button SHALL always be enabled. Tapping Cancel SHALL close the area editor and navigate back without modifying `PlayfieldEditingContext.CurrentCoordinates`.

#### Scenario: Cancel with no changes
- **WHEN** the user taps Cancel without having added or removed any points
- **THEN** the page closes and `PlayfieldEditingContext.CurrentCoordinates` is unchanged

#### Scenario: Cancel after adding points
- **WHEN** the user has added one or more points and then taps Cancel
- **THEN** the page closes and `PlayfieldEditingContext.CurrentCoordinates` is unchanged (the added points are discarded)

### Requirement: OK button disabled until three points are placed
The OK button SHALL be disabled when fewer than 3 coordinate points are present on the map. It SHALL become enabled as soon as a third point is placed, and disabled again if points are removed below 3.

#### Scenario: Fewer than 3 points — OK disabled
- **WHEN** the number of points on the map is 0, 1, or 2
- **THEN** the OK button is disabled

#### Scenario: Third point placed — OK enables
- **WHEN** the user places the third point on the map
- **THEN** the OK button becomes enabled

#### Scenario: Point removed below threshold — OK disables
- **WHEN** the map has 3 points and the user removes one
- **THEN** the OK button becomes disabled again

### Requirement: OK saves coordinates and closes
Tapping the OK button (when enabled) SHALL write the current set of map points to `PlayfieldEditingContext.CurrentCoordinates` and navigate back to the playfield details page.

#### Scenario: OK confirms coordinate set
- **WHEN** the user has placed at least 3 points and taps OK
- **THEN** `PlayfieldEditingContext.CurrentCoordinates` is updated with the current points (in placement order) and the page closes

### Requirement: Single tap on map places a coordinate point
A single tap on an empty area of the map SHALL place a visible marker at the GPS coordinate corresponding to the tap position.

#### Scenario: Tap on empty map area
- **WHEN** the user taps on an area of the map that does not contain an existing point
- **THEN** a new coordinate point marker is placed at that GPS location and added to the working point list

### Requirement: Tap on existing point removes it
A single tap on an existing coordinate point marker SHALL remove that point from the working list.

#### Scenario: Tap on existing point
- **WHEN** the user taps on an existing point marker
- **THEN** that point is removed from the working list and its marker is removed from the map

### Requirement: Progressive polygon shape drawing
The map SHALL visualise the current set of points as a polygon shape that updates after every add or remove:
- 1 point: single marker, no lines
- 2 points: open line segment between the two markers
- 3+ points: closed polygon where the last point connects back to the first point

#### Scenario: One point — no line drawn
- **WHEN** exactly one point is on the map
- **THEN** only a single marker is shown; no line is drawn

#### Scenario: Two points — open line drawn
- **WHEN** exactly two points are on the map
- **THEN** a line segment is drawn between the two markers

#### Scenario: Three or more points — closed polygon drawn
- **WHEN** three or more points are on the map
- **THEN** a closed polygon is drawn connecting all points in placement order, with the last point connected back to the first

#### Scenario: New point added to existing polygon
- **WHEN** the user adds a point to a map that already has 3 or more points
- **THEN** the polygon is redrawn to include the new point, with the closing edge now connecting the new last point back to the first point

#### Scenario: Point removed from polygon
- **WHEN** the user removes a point from a map that has 3 or more points
- **THEN** the polygon (or line, if reduced to 2 points) is redrawn without the removed point

### Requirement: Map supports pinch-to-zoom and pan
The map SHALL support standard touch interactions: pinch gesture to zoom in and out, and hold-drag (pan) to move the map viewport. These interactions SHALL NOT place or remove coordinate points.

#### Scenario: Pinch-to-zoom
- **WHEN** the user performs a pinch gesture on the map
- **THEN** the map zooms in or out accordingly and no point is placed or removed

#### Scenario: Pan by hold-drag
- **WHEN** the user holds and drags on the map
- **THEN** the map pans in the drag direction and no point is placed or removed
