## Why

The app lacks a playfields management screen — users have no way to view, sync, or delete their playfields from the mobile client. This page is the primary entry point for managing playfields and is required before game sessions can be started.

## What Changes

- Add a new `PlayfieldsListPage` in the Ionic/Angular client accessible from the home page playfield button
- Introduce a two-tab layout: "Private" (user-owned) and "Public" (discoverable playfields)
- Implement an IndexedDB-backed local store for offline-capable playfield data
- Add a sync mechanism: download playfields from the server, update local records only when `LastUpdatedOn` on the server is newer
- After sync-down, push unsynced local playfields (`IsSynced = false`) to the server via `POST /playfields`
- Display playfields as `ion-item-sliding` entries with a swipe-left delete action and a private/public tag badge
- Navigate to playfield detail page on item tap

## Capabilities

### New Capabilities

- `playfields-list-page`: Two-tab page listing private and public playfields with sync, offline storage, and slide-to-delete UX
- `playfields-indexeddb-store`: Local IndexedDB persistence for playfields with conflict-safe sync logic (server wins on newer `LastUpdatedOn`, client pushes when `IsSynced = false`)

### Modified Capabilities

<!-- No existing capability specs are changing -->

## Impact

- **Ionic/Angular client** (`src/ThePrey/src/app/`): new page, service, and IndexedDB utility
- **API**: `GET /playfields` (fetch user's private playfields), `POST /playfields` (create/update), `GET /playfields/public` (public tab) — server-side handlers must already exist or will be tracked separately
- **Routing**: home page playfields button must navigate to the new page
- **Dependencies**: `idb` npm package (or native IndexedDB API) for local storage
