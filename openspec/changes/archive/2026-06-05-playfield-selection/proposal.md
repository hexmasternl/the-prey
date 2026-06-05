## Why

Game sessions require a playfield to be selected before starting — currently there is no dedicated UI component for this. Players need a discoverable way to browse their own playfields, find public ones, and confirm a selection before proceeding.

## What Changes

- New `PlayfieldSelectionPage` component for the Ionic/Angular client that presents a searchable, selectable list of playfields.
- Local-first: initial list is loaded from IndexedDB so the page is usable offline.
- Server search: typing 3+ characters triggers a live search against the backend API, merging results with the local list.
- Each list item displays the playfield name, a public/private badge, and an owner badge (mine / external).
- Single-select interaction: tapping a row selects it; a "Select" button at the bottom becomes enabled and confirms the choice.

## Capabilities

### New Capabilities

- `playfield-selection`: A page component that lets the player browse (local-first) and search (server-side) playfields, pick one, and confirm the selection via a bottom action button.

### Modified Capabilities

<!-- No existing spec-level requirements are changing. -->

## Impact

- **Client** (`src/ThePrey`): new Ionic/Angular page + component; IndexedDB service reads; HTTP search call to the existing `/playfields/public?q=` endpoint.
- **API**: no changes — the existing `SearchPublicPlayFields` endpoint (`GET /playfields/public?q=`) is sufficient.
- **State**: selected playfield is returned as an output/event from the page; no new persistent state.
