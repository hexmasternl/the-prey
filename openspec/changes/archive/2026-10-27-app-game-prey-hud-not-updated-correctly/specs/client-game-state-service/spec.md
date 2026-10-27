# Spec Delta

## ADDED Requirements

### Requirement: Report broadcasts of the current user's location

The Game State Service SHALL let its subscribers tell when an applied location batch included the current user's own location, separately from batches that named only other players. Snapshots SHALL NOT count as such a broadcast.

#### Scenario: Batch includes the current user

- **WHEN** a `locations-updated` message is applied that contains an entry for the current user
- **THEN** subscribers can see that the current user's location was broadcast at that moment

#### Scenario: Batch names only other players

- **WHEN** a `locations-updated` message is applied that contains no entry for the current user
- **THEN** subscribers see a location batch but no broadcast of the current user's location

#### Scenario: Snapshot is not a broadcast

- **WHEN** a full snapshot replaces the current state
- **THEN** no broadcast of the current user's location is reported
