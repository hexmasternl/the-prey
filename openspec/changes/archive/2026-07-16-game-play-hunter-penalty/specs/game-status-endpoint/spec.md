# game-status-endpoint Specification (delta)

## ADDED Requirements

### Requirement: GameStatusDto carries HunterMayMoveAt

The `GameStatusDto` returned by `GET /games/{id}/status` SHALL include a `HunterMayMoveAt` field carrying the absolute date/time (`StartedAt + HunterDelayTime`) at which the hunter is allowed to move. For InProgress games the field SHALL always be populated; the value is returned to all participants (hunter and preys alike) so both views can render the head-start countdown.

#### Scenario: HunterMayMoveAt present for an InProgress game

- **WHEN** a participant calls GET /games/{id}/status for an InProgress game with a 5-minute `HunterDelayTime` started at 12:00:00
- **THEN** the response includes `HunterMayMoveAt` equal to 12:05:00

#### Scenario: HunterMayMoveAt returned to both hunter and prey callers

- **WHEN** the hunter and a prey each call GET /games/{id}/status for the same InProgress game
- **THEN** both responses carry the same `HunterMayMoveAt` value
