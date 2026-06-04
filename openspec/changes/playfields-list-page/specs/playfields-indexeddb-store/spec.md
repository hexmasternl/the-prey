## ADDED Requirements

### Requirement: Playfields object store in IndexedDB
The app SHALL maintain a `playfields` object store within the existing IndexedDB database. Each record SHALL store the full playfield data plus a client-managed `isSynced` boolean flag. The store SHALL use the playfield's `id` (UUID string) as the primary key.

#### Scenario: Store is created on DB version upgrade
- **WHEN** the app opens IndexedDB and the `playfields` store does not exist
- **THEN** the store is created during the `onupgradeneeded` event with `id` as the key path

#### Scenario: Retrieve all playfields for a user
- **WHEN** the service queries the `playfields` store
- **THEN** it returns all records matching the current user's `ownerId`

---

### Requirement: Upsert a playfield with server-wins conflict resolution
The `PlayfieldsDbService.upsert(playfield)` method SHALL insert or update a playfield record. If a record with the same `id` already exists in IndexedDB, it SHALL only be overwritten if the incoming `lastUpdatedOn` timestamp is strictly newer than the stored value.

#### Scenario: Insert new record
- **WHEN** `upsert` is called with a playfield `id` not present in IndexedDB
- **THEN** the record is inserted with `isSynced = true`

#### Scenario: Update when incoming is newer
- **WHEN** `upsert` is called and the incoming `lastUpdatedOn` is strictly greater than the stored `lastUpdatedOn`
- **THEN** the existing record is replaced with the new data and `isSynced = true`

#### Scenario: Skip when incoming is not newer
- **WHEN** `upsert` is called and the incoming `lastUpdatedOn` is less than or equal to the stored `lastUpdatedOn`
- **THEN** the existing record is NOT modified

---

### Requirement: Query unsynced local playfields
The `PlayfieldsDbService.getUnsynced()` method SHALL return all playfield records where `isSynced = false`.

#### Scenario: Returns only unsynced records
- **WHEN** IndexedDB contains a mix of synced and unsynced playfields
- **THEN** `getUnsynced()` returns only those with `isSynced = false`

#### Scenario: Returns empty array when all synced
- **WHEN** all IndexedDB records have `isSynced = true`
- **THEN** `getUnsynced()` returns an empty array

---

### Requirement: Mark a playfield as synced
The `PlayfieldsDbService.markSynced(id)` method SHALL set `isSynced = true` for the record with the given `id`.

#### Scenario: Mark existing record synced
- **WHEN** `markSynced` is called with a valid playfield `id`
- **THEN** the record's `isSynced` field is updated to `true` without modifying other fields

---

### Requirement: Delete a playfield from IndexedDB
The `PlayfieldsDbService.delete(id)` method SHALL remove the record with the given `id` from IndexedDB.

#### Scenario: Delete existing record
- **WHEN** `delete` is called with a valid playfield `id`
- **THEN** the record is removed from IndexedDB and is no longer returned by subsequent queries

#### Scenario: Delete non-existent record is a no-op
- **WHEN** `delete` is called with an `id` that does not exist in IndexedDB
- **THEN** no error is thrown and the store is unchanged
