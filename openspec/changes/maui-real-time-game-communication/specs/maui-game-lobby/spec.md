## MODIFIED Requirements

### Requirement: Live lobby updates

While the lobby page is visible it SHALL subscribe to the shared game-state service and replace its displayed state from each broadcast full game snapshot, so joins, ready changes, hunter designation, settings edits, and game start made by any player are reflected without a manual refresh. When the lobby resolves the active game it SHALL ensure the shared connection is started for that game. It SHALL unsubscribe its handler when the page is no longer visible, but SHALL NOT close the shared connection — the connection outlives the lobby so the play page it hands off to keeps receiving updates without reconnecting.

#### Scenario: Another player's ready change appears

- **WHEN** the lobby is visible and another player readies up
- **THEN** the broadcast snapshot updates that player's row to Ready and, if it makes the game startable, enables the owner's START OPERATION action

#### Scenario: Game started by the owner reaches other players

- **WHEN** the lobby is visible on a non-owner's device and the owner starts the game
- **THEN** the broadcast started snapshot causes that device to hand off to the gameplay screen via the navigation seam

#### Scenario: Handler unsubscribed but connection kept alive on leaving

- **WHEN** the lobby page is no longer visible
- **THEN** the lobby's subscriber handler is removed but the shared connection stays open for the play page
