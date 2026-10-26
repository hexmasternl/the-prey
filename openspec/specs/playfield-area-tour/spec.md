# playfield-area-tour Specification

## Purpose
A one-time walkthrough of the Android app's playfield area editor, shown when a player first draws the area of a new playfield, so they know how to place, move and remove points and how the area is saved.

## Requirements

### Requirement: Area tour starts on a new playfield's area screen

When the player opens the area screen of a new playfield (create mode) and the playfield area tour is not seen, the app SHALL start the playfield area tour as soon as the screen is shown. The tour SHALL NOT start on the area screen of an existing playfield, editable or read-only.

#### Scenario: First new playfield

- **WHEN** the player taps create on the playfields list, opens the area from the new playfield's details, and the area tour is not seen
- **THEN** the area tour starts on the area screen

#### Scenario: Editing an existing playfield

- **WHEN** the player opens the area of one of their existing playfields and the area tour is not seen
- **THEN** no tour is shown

#### Scenario: Read-only playfield

- **WHEN** the player opens the area of a public playfield
- **THEN** no tour is shown

#### Scenario: Already seen

- **WHEN** the area tour is seen and the player creates another playfield
- **THEN** no tour is shown on the area screen

### Requirement: Area tour steps

The area tour SHALL have six steps, in this order: (1) what the play area is, as a centred card; (2) tap the map to add points, which are joined in order and need at least 3 to form the area; (3) tap a point to select it (red border) and drag it to move it, or tap empty map to deselect; (4) the delete button removes the selected point, spotlighting that button; (5) Erase All removes every point, spotlighting it; (6) Save, spotlighting it.

#### Scenario: Step order

- **WHEN** the player advances through the whole area tour
- **THEN** the steps appear in the order: play area, add points, select and move, delete point, erase all, save

#### Scenario: Delete step spotlights the delete button

- **WHEN** the delete-point step is shown
- **THEN** the delete button is spotlighted, even though it is greyed out because no point is selected

### Requirement: Save step explains how editing the playfield works

The Save step SHALL explain that Save returns the area to the playfield details, that the playfield is only stored once it is saved there, and that the area can be changed later by opening it again from the playfield's details. It SHALL also mention that Cancel leaves without changing the area.

#### Scenario: Save step text

- **WHEN** the Save step is shown on an English device
- **THEN** the text says Save returns the area to the details, the playfield is stored when saved there, the area can be edited again later from the details, and Cancel leaves without changes

### Requirement: Area tour leaves the editor unchanged

Running, completing or skipping the area tour SHALL NOT add, select, move or remove any point, and the editor SHALL be fully usable as soon as the tour closes.

#### Scenario: After the tour

- **WHEN** the player completes the area tour on a new playfield
- **THEN** the map has no points, no point is selected, and the next tap on the map adds the first point
