# Spec Delta

## ADDED Requirements

### Requirement: Penalty state survives a full snapshot

When the service adopts a full snapshot, it SHALL take each participant's penalty flag and penalty end time from the snapshot. A penalty SHALL count as active only until its end time, judged against the corrected server clock, so penalty indicators on the HUDs and the map appear after a refresh and disappear when the penalty ends, without waiting for a message or the next resync.

#### Scenario: Penalty shown after a refresh

- **WHEN** the player refreshes the game state while they have a penalty ending in 4 minutes
- **THEN** their HUD shows the penalty straight away

#### Scenario: Penalty ends on time after a snapshot

- **WHEN** a snapshot reported a participant's penalty ending at a moment that has now passed on the corrected server clock, and no further message has arrived
- **THEN** that participant is no longer shown as penalised

#### Scenario: Live penalty still applies between snapshots

- **WHEN** a `prey-updated` `penalized` message arrives after the last snapshot
- **THEN** the named participant is shown as penalised until the end time in that message
