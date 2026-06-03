## Why

Players need to manage their playfields from the mobile app — viewing, creating, and deleting them while connected, and still accessing them when offline. Without local storage and sync, the playfield management workflow is incomplete and unusable in the field.

## What Changes

- Playfields screen downloads the user's private playfields from the server on load
- Playfields are persisted locally (SQLite or file cache) for offline access
- A "Create new" button navigates to the playfield creation flow
- Tapping a playfield opens its detail view
- Swiping a playfield left reveals a delete button; confirming deletion removes it from both the server and local storage

## Capabilities

### New Capabilities

- `playfield-list`: Display, cache, and manage the list of the authenticated user's private playfields — including fetch-on-load, offline fallback, create navigation, detail navigation, and swipe-to-delete with server + local sync

### Modified Capabilities

<!-- None — no existing spec-level behavior is changing -->

## Impact

- **App layer**: `PlayfieldsPage` (new MAUI ContentPage), `PlayfieldsViewModel`, `IPlayfieldService` interface and implementation
- **Local storage**: New local cache (SQLite or JSON file) for playfield data
- **API**: Calls existing private playfields endpoint (GET, DELETE)
- **Navigation**: `AppShell` must register the new page; deep-link from main menu
- **Auth**: Requires authenticated session (Auth0 token forwarded in API calls)
