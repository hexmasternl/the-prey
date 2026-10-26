# playfield-play-area Specification

## Purpose
TBD - created by archiving change playfield-play-area. Update Purpose after archive.

## Requirements

### Requirement: Full-screen interactive map

The playfield area screen SHALL show a map that fills the whole screen, built from free and open map data (OpenStreetMap) without a paid API key. The player SHALL be able to drag to pan and pinch to zoom; the map SHALL stay north-up and flat. The map data attribution SHALL remain visible. The screen SHALL centre on the playfield's existing area when it has points, otherwise on the device's current position when location permission is granted, otherwise on a world view.

#### Scenario: Page opens with map filling the screen

- **WHEN** the player taps "Playfield Area" on the details screen
- **THEN** a map fills the screen edge to edge, with the action buttons floating at the bottom above the system navigation area

#### Scenario: Pan and zoom

- **WHEN** the player drags one finger across the map, or pinches two fingers apart or together
- **THEN** the map pans with the finger, or zooms in or out

#### Scenario: Map centres on existing polygon centroid

- **WHEN** the playfield already has at least one point
- **THEN** the map shows all existing points as green dots and, with 3 or more points, the polygon, zoomed so the whole area is visible

#### Scenario: Map centres on device location when no polygon exists

- **WHEN** the playfield has no points and location permission is granted and the device provides a position
- **THEN** the map centres on the device's current position at a street-level zoom with no points drawn

#### Scenario: Map falls back to world zoom when location is unavailable

- **WHEN** the playfield has no points and location permission is not granted or no position is available within a few seconds
- **THEN** the map shows a world view with no points drawn, and the player can still pan, zoom and place points

#### Scenario: Location permission is not requested here

- **WHEN** location permission has not been granted
- **THEN** the area screen does not show a permission prompt

#### Scenario: Attribution visible

- **WHEN** the map is shown
- **THEN** the map data attribution (OpenStreetMap contributors) is visible and not covered by the buttons

### Requirement: Add GPS points by tapping the map

On an editable playfield, a single tap on the map where there is no point SHALL add a GPS point at the tapped position, drawn as a green dot. Each new point SHALL be appended after the existing points, so the area's outline follows the order in which points were placed. When a point is selected, a tap on empty map SHALL only deselect it and SHALL NOT add a point.

#### Scenario: First tap adds a single marker

- **WHEN** the player taps an empty spot on the map and no point is selected
- **THEN** a green dot appears at that position

#### Scenario: Subsequent taps add additional markers

- **WHEN** the player taps further empty spots on the map
- **THEN** each tap adds another green dot without removing earlier ones, and the area outline connects them in the order they were placed

#### Scenario: Tap while a point is selected deselects it

- **WHEN** a point is selected and the player taps an empty spot on the map
- **THEN** the point is no longer selected and no new point is added

#### Scenario: Panning does not add points

- **WHEN** the player drags or pinches the map
- **THEN** no point is added

### Requirement: Live polygon drawn from points

When the area has at least 3 points, the screen SHALL draw a green polygon connecting all points in order, with the last connected back to the first, filled at 50 % opacity so the map stays visible underneath. The polygon SHALL update immediately when a point is added, moved or erased. With fewer than 3 points no polygon SHALL be drawn.

#### Scenario: Polygon appears after third point is added

- **WHEN** the player has placed exactly 3 points
- **THEN** a green, half-transparent polygon connecting the three points is drawn and the streets beneath it remain readable

#### Scenario: Polygon updates as more points are added

- **WHEN** the polygon is visible and the player adds a point
- **THEN** the polygon immediately redraws to include all points including the newly added one

#### Scenario: Polygon follows a moved point

- **WHEN** the polygon is visible and the player drags a point to a new position
- **THEN** the polygon immediately redraws through the moved point

#### Scenario: No polygon shown with fewer than 3 points

- **WHEN** the area has 0, 1 or 2 points
- **THEN** no polygon is drawn; only the green dots are visible

### Requirement: Cancel without saving

The screen SHALL show a Cancel button, styled as the default (neutral) action. Cancel, the system Back gesture and the back arrow SHALL close the area screen and return to the details screen without changing its area, whatever was placed, moved or erased on the map.

#### Scenario: Tapping Cancel discards all changes

- **WHEN** the player adds, moves or erases points and taps Cancel
- **THEN** the details screen is shown with the area it had before the area screen was opened

#### Scenario: System Back behaves as Cancel

- **WHEN** the player uses the system Back gesture on the area screen
- **THEN** the area changes made on the map are discarded

### Requirement: Save the area to the playfield details

The screen SHALL show a green Save button. Save SHALL be disabled while the area has fewer than 3 points. Tapping Save SHALL close the area screen and return to the details screen with the new area as an unsaved change. Nothing SHALL be sent to the server until the player saves on the details screen.

#### Scenario: Save button is disabled with fewer than 3 points

- **WHEN** the area has 0, 1 or 2 points
- **THEN** the Save button is visible but disabled

#### Scenario: Save button is enabled with 3 or more points

- **WHEN** the area has at least 3 points
- **THEN** the Save button is enabled

#### Scenario: Save returns the area to the details screen

- **WHEN** the player taps Save with 4 points placed
- **THEN** the details screen is shown, no request has been sent to the server, and the details screen's Save action is enabled because the area changed

### Requirement: Select and drag a GPS point

On an editable playfield, tapping an existing point SHALL select it, drawn as a green dot with a red border. At most one point SHALL be selected at a time. Dragging a selected point SHALL move it with the finger without panning the map, and its new position SHALL be kept when the finger lifts. Dragging from anywhere else SHALL pan the map. Points SHALL be easy to hit with a finger (a touch target of at least 48 dp).

#### Scenario: Select a point

- **WHEN** the player taps an existing green dot
- **THEN** that dot gets a red border, and any previously selected dot loses its red border

#### Scenario: Drag the selected point

- **WHEN** a point is selected and the player presses on it and drags
- **THEN** the dot follows the finger, the map does not pan, and the polygon (if shown) follows the dot

#### Scenario: Drag ends

- **WHEN** the player lifts the finger after dragging the selected point
- **THEN** the point stays at its new position and remains selected

#### Scenario: Dragging an unselected point pans the map

- **WHEN** the player presses on a dot that is not selected and drags
- **THEN** the map pans and the dot does not move

#### Scenario: Tapping a point does not add a point

- **WHEN** the player taps an existing dot
- **THEN** no new point is added

### Requirement: Erase all points

The screen SHALL show a red Erase All button that removes every point from the map, along with any selection and the polygon. Erase All SHALL be disabled when there are no points. Erasing SHALL only affect the area being edited on the map; Cancel SHALL still return to the area the details screen had.

#### Scenario: Erase all

- **WHEN** the area has 5 points and the player taps Erase All
- **THEN** all dots and the polygon disappear and Save becomes disabled

#### Scenario: Erase All disabled when empty

- **WHEN** the area has no points
- **THEN** the Erase All button is visible but disabled

#### Scenario: Erase then cancel

- **WHEN** the player taps Erase All and then Cancel
- **THEN** the details screen still has the original area

### Requirement: Action buttons layout

The Cancel, Erase All and Save buttons SHALL be shown side by side at the bottom of the screen, in that order from left to right, above the system navigation area and readable over the map. Cancel SHALL be styled as the default (neutral) action, Erase All as a destructive (red) action and Save as the confirming (green) action. On a Dutch device they SHALL read "Annuleren", "Alles wissen" and "Opslaan".

#### Scenario: Button order and styles

- **WHEN** the area screen is open for an editable playfield
- **THEN** Cancel, Erase All and Save are shown left to right at the bottom, in neutral, red and green styles

#### Scenario: Dutch labels

- **WHEN** the device language is Dutch
- **THEN** the buttons read "Annuleren", "Alles wissen" and "Opslaan"

### Requirement: Read-only area view

For a playfield opened read-only, the area screen SHALL show the map, the points and the polygon, and SHALL support panning and zooming. Tapping SHALL NOT add, select or move points. Instead of Cancel, Erase All and Save, a single Close button SHALL be shown that returns to the details screen.

#### Scenario: Read-only map

- **WHEN** the player opens the area of a playfield from the Public tab
- **THEN** the area is shown, and tapping the map or a dot changes nothing

#### Scenario: Close

- **WHEN** the player taps Close on a read-only area screen
- **THEN** the details screen is shown

### Requirement: Area edits survive configuration changes

Points placed, moved or erased on the area screen and the current selection SHALL be kept when the device rotates or the app is briefly sent to the background.

#### Scenario: Rotation keeps the points

- **WHEN** the player has placed 4 points and rotates the device
- **THEN** the same 4 points and polygon are shown

### Requirement: Delete the selected point

On an editable playfield, the area screen SHALL show a floating delete button with a trash icon near the bottom end of the map, above the action buttons and without covering the map attribution. The button SHALL be greyed out and do nothing while no point is selected, and SHALL be red and enabled while a point is selected. Tapping it SHALL remove only the selected point and leave no point selected.

#### Scenario: Delete button greyed out without a selection

- **WHEN** the area screen is open for an editable playfield and no point is selected
- **THEN** the delete button with the trash icon is visible, greyed out, and tapping it changes nothing

#### Scenario: Selecting a point enables the delete button

- **WHEN** the player taps an existing dot so it gets a red border
- **THEN** the delete button turns red and is enabled

#### Scenario: Deselecting disables the delete button again

- **WHEN** a point is selected and the player taps an empty spot on the map
- **THEN** the point is deselected, no point is added, and the delete button is greyed out again

#### Scenario: Delete the selected point

- **WHEN** the area has 5 points, the third point is selected, and the player taps the delete button
- **THEN** the third dot disappears, the other 4 dots stay where they were, the polygon connects the remaining points in their original order, no point is selected, and the delete button is greyed out

#### Scenario: Deleting below 3 points removes the polygon

- **WHEN** the area has 3 points, one is selected, and the player taps the delete button
- **THEN** 2 dots remain, no polygon is drawn, and Save is disabled

#### Scenario: Deleting the last point

- **WHEN** the area has 1 point, it is selected, and the player taps the delete button
- **THEN** no dots remain and Erase All is disabled

#### Scenario: Delete then cancel

- **WHEN** the player deletes a point and then taps Cancel
- **THEN** the details screen still has the area it had before the area screen was opened

#### Scenario: Deleted point stays deleted after rotation

- **WHEN** the player deletes a point and rotates the device
- **THEN** the deleted point is still gone and no point is selected

#### Scenario: Accessible label

- **WHEN** a screen reader focuses the delete button
- **THEN** it reads "Delete selected point", or "Geselecteerd punt verwijderen" on a Dutch device, and reports the button as disabled while no point is selected

### Requirement: No delete button on the read-only area view

For a playfield opened read-only, the area screen SHALL NOT show the delete button.

#### Scenario: Read-only area has no delete button

- **WHEN** the player opens the area of a playfield from the Public tab
- **THEN** no delete button is shown
