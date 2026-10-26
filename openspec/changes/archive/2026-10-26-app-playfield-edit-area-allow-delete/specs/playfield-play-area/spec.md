# Spec Delta

## ADDED Requirements

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
