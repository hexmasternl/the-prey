## ADDED Requirements

### Requirement: Playfields list page is accessible from home
The app SHALL navigate to `/playfields` when the user taps the "PLAYFIELDS" button on the home page. The route SHALL be protected by `authGuardFn` and lazy-load the `PlayfieldsListPage` component.

#### Scenario: Navigate to playfields from home
- **WHEN** the authenticated user taps the "PLAYFIELDS" button on the home page
- **THEN** the app navigates to the `/playfields` route and renders `PlayfieldsListPage`

#### Scenario: Unauthenticated access is blocked
- **WHEN** an unauthenticated user attempts to navigate to `/playfields`
- **THEN** the auth guard redirects them to `/login`

---

### Requirement: Two-tab layout for private and public playfields
The `PlayfieldsListPage` SHALL display two tabs labelled "Private" and "Public" using `ion-segment`. The "Private" tab SHALL be active by default on page load.

#### Scenario: Default tab on load
- **WHEN** the user navigates to the playfields page
- **THEN** the "Private" tab is active and the private playfields list is displayed

#### Scenario: Switch to public tab
- **WHEN** the user taps the "Public" segment button
- **THEN** the public playfields section is displayed

#### Scenario: Public tab placeholder
- **WHEN** the "Public" tab is active and public data has not been loaded
- **THEN** a "Coming soon" placeholder message is shown

---

### Requirement: Private playfields list syncs with the server on page load
On entering the page, the service SHALL execute the sync sequence: (1) download playfields from `GET /playfields`, merge into IndexedDB with server-wins conflict resolution on `LastUpdatedOn`, (2) push locally unsynced records (`IsSynced = false`) to `POST /playfields`.

#### Scenario: Download and update newer records
- **WHEN** the page loads and the server returns a playfield whose `LastUpdatedOn` is newer than the local IndexedDB copy
- **THEN** the local record is overwritten with the server version and `IsSynced` is set to `true`

#### Scenario: Skip older server records
- **WHEN** the server returns a playfield whose `LastUpdatedOn` is older than or equal to the local IndexedDB copy
- **THEN** the local record is NOT overwritten

#### Scenario: Insert missing records
- **WHEN** the server returns a playfield that does not exist in IndexedDB
- **THEN** the record is inserted into IndexedDB with `IsSynced = true`

#### Scenario: Upload unsynced local playfields after download
- **WHEN** the sync-down phase completes and IndexedDB contains records where `IsSynced = false`
- **THEN** each such record is posted to `POST /playfields`

#### Scenario: Mark records synced after upload
- **WHEN** `POST /playfields` returns a success response for a local record
- **THEN** that record's `IsSynced` flag is set to `true` in IndexedDB

#### Scenario: Sync error handling
- **WHEN** the network request fails during sync
- **THEN** an error state is shown and the user can retry; previously cached data remains visible

---

### Requirement: Private playfields displayed as sliding list items
Each playfield in the private list SHALL be rendered as an `ion-item-sliding`. The label SHALL show the playfield name. A badge tag SHALL indicate whether the playfield is "Private" or "Public".

#### Scenario: Render list with badge
- **WHEN** the private tab is active and playfields are loaded from IndexedDB
- **THEN** each playfield is shown as an `ion-item-sliding` with name and a "Private"/"Public" badge tag

#### Scenario: Empty private list
- **WHEN** no private playfields exist in IndexedDB and sync has completed
- **THEN** a "No playfields yet" empty-state message is shown

---

### Requirement: Tap a playfield item to view details
Tapping an `ion-item` SHALL navigate to the playfield detail page at `/playfields/:id`.

#### Scenario: Navigate to detail on tap
- **WHEN** the user taps a playfield list item
- **THEN** the app navigates to `/playfields/:id` passing the playfield ID

---

### Requirement: Swipe left on a playfield item to reveal delete action
`ion-item-sliding` SHALL expose a red delete `ion-item-option` on left-swipe. Confirming delete removes the record from IndexedDB and updates the displayed list.

#### Scenario: Reveal delete option on swipe
- **WHEN** the user swipes a playfield item to the left
- **THEN** a red "Delete" option is revealed

#### Scenario: Delete removes item from list
- **WHEN** the user taps the revealed "Delete" option
- **THEN** the playfield is removed from the local IndexedDB store and disappears from the list

#### Scenario: Delete does not call server
- **WHEN** the user deletes a playfield locally
- **THEN** no DELETE request is sent to the server (server-side delete is a future feature)
