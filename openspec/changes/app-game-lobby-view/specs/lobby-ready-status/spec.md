# Spec Delta

## MODIFIED Requirements

### Requirement: Settings change resets all non-owner ready flags
When the game owner updates the game configuration via `PUT /games/{id}/config`, the server SHALL reset every non-owner lobby player's `IsReady` to `false`. It SHALL then broadcast a `participant-changed` message with the full participant for every player whose flag changed, in addition to `configuration-changed`, so connected clients show the reset without a resync.

#### Scenario: Settings updated — non-owner ready flags reset
- **WHEN** the game owner submits a valid settings update
- **THEN** all non-owner lobby players have their `IsReady` set to `false` and the updated game with reset flags is returned

#### Scenario: Owner's ready state is unaffected by settings change
- **WHEN** the game owner submits a settings update
- **THEN** the owner's effective ready state remains `true`

#### Scenario: Reset is broadcast per participant
- **WHEN** the game owner submits a valid settings update while two non-owner players are ready and one is waiting
- **THEN** the server publishes `configuration-changed` and exactly two `participant-changed` messages, one for each player who was ready, each with `isReady: false`

#### Scenario: No reset, no participant broadcast
- **WHEN** the game owner submits a valid settings update while no non-owner player is ready
- **THEN** the server publishes `configuration-changed` and no `participant-changed` message
