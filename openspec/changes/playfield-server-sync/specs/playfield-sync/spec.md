## ADDED Requirements

### Requirement: Playfields carry a last-updated timestamp
Every playfield SHALL carry a `LastUpdatedOn` (`DateTimeOffset`) on both the server and in the local cache. Creating or modifying a playfield in the app SHALL set `LastUpdatedOn` to the current UTC time.

#### Scenario: Timestamp set on create
- **WHEN** the user saves a new playfield
- **THEN** the stored playfield's `LastUpdatedOn` equals the moment of saving (UTC)

#### Scenario: Timestamp refreshed on edit
- **WHEN** the user edits and saves an existing playfield
- **THEN** the playfield's `LastUpdatedOn` is replaced with the current UTC time

### Requirement: Local sync state tracking
The local cache SHALL store an `IsSynchronized` flag per playfield. Any local create or edit SHALL set the flag to `false`. The flag SHALL be set to `true` only after the server has confirmed it holds a copy at least as new as the local one. The flag SHALL NOT be sent to the server.

#### Scenario: Flag cleared on local mutation
- **WHEN** a playfield is created or edited locally
- **THEN** its `IsSynchronized` flag is `false` in the cache

#### Scenario: Flag set after successful upload
- **WHEN** an upload of an unsynchronised playfield succeeds
- **THEN** its `IsSynchronized` flag is `true` in the cache

### Requirement: Immediate upload after save
When the user saves a playfield and the device is online, the app SHALL write the playfield to the local cache first and then upload it to the server. On upload success the playfield SHALL be marked synchronised; on failure the local copy SHALL be retained unsynchronised and retried during a later sync pass, with the user informed that the playfield was saved locally.

#### Scenario: Online save succeeds
- **WHEN** the user saves a playfield while online and the server accepts the upload
- **THEN** the playfield is in the local cache with `IsSynchronized == true` and the app navigates back to the list

#### Scenario: Online save with server failure
- **WHEN** the user saves a playfield while online but the upload fails
- **THEN** the playfield remains in the local cache with `IsSynchronized == false` and the user is informed the playfield is saved locally and will be uploaded later

#### Scenario: Offline save
- **WHEN** the user saves a playfield while the device is offline
- **THEN** no upload is attempted, the playfield is cached with `IsSynchronized == false`, and the user is informed the playfield is saved locally

### Requirement: Sync pass on opening the playfields list
When the playfields list is opened and the device is online, the app SHALL first upload every cached playfield whose `IsSynchronized` flag is `false`, and then download the server list. When the device is offline the list SHALL be served from cache without any sync attempt.

#### Scenario: Pending playfields pushed on list open
- **WHEN** the user opens the playfields list while online and the cache contains unsynchronised playfields
- **THEN** each unsynchronised playfield is uploaded before the list is displayed, and successfully uploaded ones are marked synchronised

#### Scenario: Failed push retried on next open
- **WHEN** an upload during the sync pass fails for a playfield
- **THEN** that playfield keeps `IsSynchronized == false` and is retried the next time the list opens

### Requirement: Timestamp-guarded download merge
When playfields are downloaded from the server, a server copy SHALL replace the local copy only when the server `LastUpdatedOn` is newer than the local `LastUpdatedOn`. Server playfields not present locally SHALL be added. Local playfields that are newer than the server copy SHALL be kept unchanged.

#### Scenario: Server copy is newer
- **WHEN** the sync pass downloads a playfield whose `LastUpdatedOn` is later than the cached copy's
- **THEN** the cached copy is replaced by the server copy

#### Scenario: Local copy is newer
- **WHEN** the sync pass downloads a playfield whose `LastUpdatedOn` is earlier than or equal to the cached copy's
- **THEN** the cached copy is left unchanged

#### Scenario: New server playfield
- **WHEN** the sync pass downloads a playfield that does not exist in the cache
- **THEN** it is added to the cache marked synchronised

### Requirement: Timestamp-guarded server update
The server SHALL expose an upsert operation (`PUT /playfields/{id}`) that creates the playfield when the id is unknown, replaces it when the incoming `LastUpdatedOn` is newer than the stored value, and rejects the write with a conflict response containing the current server copy when the incoming `LastUpdatedOn` is older than or equal to the stored value. Only the owner SHALL be able to update a playfield.

#### Scenario: Upsert creates unknown playfield
- **WHEN** a PUT arrives for an id the server does not know
- **THEN** the playfield is created with the client-supplied id and `LastUpdatedOn`

#### Scenario: Upsert replaces older server copy
- **WHEN** a PUT arrives with `LastUpdatedOn` newer than the stored playfield's
- **THEN** the server replaces the stored playfield

#### Scenario: Stale write rejected
- **WHEN** a PUT arrives with `LastUpdatedOn` older than or equal to the stored playfield's
- **THEN** the server responds 409 Conflict with the current server copy in the body, and the app adopts that copy locally and marks it synchronised

#### Scenario: Non-owner update rejected
- **WHEN** a PUT arrives for a playfield owned by a different user
- **THEN** the server rejects the request and the stored playfield is unchanged

### Requirement: Center coordinates maintained on the playfield
Every playfield with at least one coordinate SHALL carry `CenterCoordinates` — the arithmetic centroid of its area coordinates — on both the server and in the local cache. The value SHALL be recomputed whenever the coordinate set changes.

#### Scenario: Center computed on save
- **WHEN** the user saves a playfield with a configured area
- **THEN** the stored `CenterCoordinates` equals the mean latitude/longitude of the area coordinates

#### Scenario: Center recomputed after area change
- **WHEN** the user changes the area of an existing playfield and saves
- **THEN** `CenterCoordinates` reflects the new coordinate set

### Requirement: Maps center on the playfield center
The details mini-map SHALL center on `CenterCoordinates` when the playfield has coordinates. The area editor SHALL center on `CenterCoordinates` when coordinates exist, and on the device's current location when no coordinates exist.

#### Scenario: Details mini-map centered
- **WHEN** the details page opens for a playfield with coordinates
- **THEN** the mini-map view is centered on the playfield's `CenterCoordinates`

#### Scenario: Area editor centered on existing area
- **WHEN** the area editor opens with existing coordinates
- **THEN** the map view is centered on the playfield's `CenterCoordinates`

#### Scenario: Area editor centered on device for new area
- **WHEN** the area editor opens with no coordinates
- **THEN** the map view is centered on the device's current location
