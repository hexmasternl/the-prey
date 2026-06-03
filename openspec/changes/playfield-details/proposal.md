## Why

The `list-playfields` change gives players a screen to browse their playfields, but tapping "Create new" or an existing playfield leads nowhere. This change delivers the create/edit form so players can actually define and maintain playfields — the core content-management loop of the game.

## What Changes

- New `PlayfieldDetailsPage` that serves as both create and edit screens (distinguished by whether a playfield id is passed in navigation)
- Name field with a minimum of 5 characters
- Public/Private toggle
- Read-only mini-map preview showing the current coordinate shape (or the user's location centered if no coordinates exist)
- "Set Area" button that opens a coordinate editing flow (area editor; the editor itself is a separate future change — this change wires the button and the round-trip back)
- Save button, enabled only when the name is valid (≥ 5 chars) and at least 3 coordinates have been set
- On save: persist locally and POST (create) or PUT (edit) to the server

## Capabilities

### New Capabilities

- `playfield-details`: Create and edit a playfield — name input, public/private toggle, coordinate shape preview on a mini-map, "Set Area" navigation, validation, and save (local + server)

### Modified Capabilities

- `playfield-list`: The "Create new" button and playfield-tap handler now navigate to `playfield-details` (navigation target changes from placeholder to real route). No spec-level requirement changes — the list behaviour itself is unchanged.

## Impact

- **App layer**: New `PlayfieldsDetailsPage.xaml` + `.xaml.cs`; `IPlayfieldService` gains `CreatePlayfieldAsync` and `UpdatePlayfieldAsync`; `PlayfieldCacheService` gains upsert support
- **Map**: Requires a map control capable of displaying a polygon and centering on a coordinate — `Microsoft.Maui.Controls.Maps` or a lightweight alternative
- **Location**: `Geolocation` API used to center empty map on current position
- **Navigation**: `AppShell` registers `"playfield-details"` route; `list-playfields` change's create and tap handlers updated to pass id (or none for new)
- **API**: `POST /playfields` (create), `PUT /playfields/{id}` (edit)
