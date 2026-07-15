# maui-playfield-area-editor Specification

## Purpose
Provide an interactive map-based area editor in the MAUI app that lets a user draw, edit, and save a GPS polygon of vertices, returning the resulting area to the Create Playfield page.

## Requirements
### Requirement: Map centred on the current location

The area editor SHALL display an interactive map centred on the user's current location when the editor opens.

#### Scenario: Centre on current location

- **WHEN** the area editor opens and the current location is available
- **THEN** the map is centred on the user's current location at a usable zoom level for drawing

#### Scenario: Location unavailable

- **WHEN** the current location cannot be determined (permission denied or no fix)
- **THEN** the map opens at a sensible default centre and the user can still pan and zoom to their area, without any crash

### Requirement: Map navigation gestures

The map SHALL support pinch-to-zoom and touch-drag-to-pan.

#### Scenario: Pinch to zoom

- **WHEN** the user performs a pinch gesture on the map
- **THEN** the map zooms in or out accordingly

#### Scenario: Drag to pan

- **WHEN** the user touches and drags an empty area of the map
- **THEN** the map pans to follow the drag

### Requirement: Adding vertices

A single tap on the map SHALL add a green dot vertex at the tapped location, up to a maximum of 100 vertices.

#### Scenario: First tap adds a vertex

- **WHEN** the user single-taps an empty location on the map
- **THEN** a green dot marker is added at that location

#### Scenario: Maximum of 100 vertices

- **WHEN** the polygon already has 100 vertices and the user taps an empty location
- **THEN** no additional vertex is added

### Requirement: Polygon rendering

When 3 or more vertices exist the editor SHALL draw a green, transparent polygon connecting them, updating as vertices are added, moved, or removed.

#### Scenario: Polygon appears at three points

- **WHEN** a third vertex is added
- **THEN** a green transparent polygon is drawn connecting all the vertices

#### Scenario: Fewer than three points shows no polygon

- **WHEN** only 1 or 2 vertices exist
- **THEN** no polygon is drawn (the dots are shown but not filled)

#### Scenario: Polygon updates on change

- **WHEN** a vertex is moved or removed while at least 3 vertices remain
- **THEN** the polygon is redrawn to reflect the new vertex set

### Requirement: Selecting a vertex

Tapping an existing vertex SHALL select it, indicated by a red border, and reveal a Trash action to remove it.

#### Scenario: Select a vertex

- **WHEN** the user taps an existing vertex
- **THEN** that vertex is shown with a red border and a Trash action becomes available

#### Scenario: Only one vertex selected at a time

- **WHEN** a vertex is selected and the user taps a different existing vertex
- **THEN** the newly tapped vertex becomes the selected one and the previous vertex is deselected

### Requirement: Moving a selected vertex

A selected vertex SHALL be draggable to a new location, updating the polygon.

#### Scenario: Drag a selected vertex

- **WHEN** a vertex is selected and the user drags it
- **THEN** the vertex moves to the new location and the polygon is redrawn to match

### Requirement: Deleting a selected vertex

The Trash action SHALL remove the currently selected vertex and update the polygon.

#### Scenario: Delete a vertex

- **WHEN** a vertex is selected and the user taps the Trash action
- **THEN** the vertex is removed, the selection is cleared, and the polygon is redrawn (or removed if fewer than 3 vertices remain)

### Requirement: Saving the area

Save SHALL be enabled only when at least 3 vertices exist; saving SHALL return the polygon to the Create Playfield page and close the editor.

#### Scenario: Save disabled below three points

- **WHEN** fewer than 3 vertices exist
- **THEN** the Save action is disabled

#### Scenario: Save returns the polygon

- **WHEN** at least 3 vertices exist and the user taps Save
- **THEN** the editor closes and the polygon (its ordered points) is returned to the Create Playfield page

### Requirement: Cancelling the area editor

Cancel SHALL close the editor and discard any changes made in the editor.

#### Scenario: Cancel discards edits

- **WHEN** the user taps Cancel
- **THEN** the editor closes and no polygon changes are returned to the Create Playfield page
