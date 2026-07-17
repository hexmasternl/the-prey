# tag-player-action Specification

## Purpose
Defines the server-side endpoint and client-side UX for the hunter to tag a prey participant, marking them as permanently out of play.
## Requirements
### Requirement: POST /games/{gameId}/participants/{participantId}/tag marks prey as Tagged

The system SHALL expose `POST /games/{gameId}/participants/{participantId}/tag`. The endpoint SHALL require authentication. On success the endpoint SHALL return HTTP 204 No Content.

The system SHALL validate:
1. The authenticated caller is the hunter of the specified game.
2. The game is in `InProgress` state.
3. The current time is at or after the game's `HunterMayMoveAt` (`StartedAt + HunterDelayTime`).
4. The target participant exists in the game, has role `Prey`, and has `PlayerState` of `Active` or `Passive`.

On success the system SHALL set the target participant's `PlayerState` to `Tagged` (irreversible) and publish a `participant-status-changed` event to the game's Azure Web PubSub group.

#### Scenario: Hunter successfully tags an Active prey

- **WHEN** the authenticated hunter calls POST /games/{gameId}/participants/{participantId}/tag and the target prey is Active
- **THEN** the system returns HTTP 204, sets the prey's PlayerState to Tagged, and publishes a participant-status-changed event

#### Scenario: Hunter successfully tags a Passive prey

- **WHEN** the authenticated hunter calls POST /games/{gameId}/participants/{participantId}/tag and the target prey is Passive
- **THEN** the system returns HTTP 204, sets the prey's PlayerState to Tagged, and publishes a participant-status-changed event

#### Scenario: Tagging before HunterMayMoveAt is rejected with 409

- **WHEN** the hunter calls the tag endpoint at a time before the game's `HunterMayMoveAt`
- **THEN** the system returns HTTP 409 Conflict and no participant state changes

#### Scenario: Non-hunter caller is rejected with 403

- **WHEN** an authenticated user who is not the hunter of the game calls the tag endpoint
- **THEN** the system returns HTTP 403 Forbidden

#### Scenario: Tagging an Out prey is rejected with 409

- **WHEN** the hunter calls the tag endpoint for a prey whose PlayerState is Out
- **THEN** the system returns HTTP 409 Conflict

#### Scenario: Tagging an already-Tagged prey is rejected with 409

- **WHEN** the hunter calls the tag endpoint for a prey whose PlayerState is already Tagged
- **THEN** the system returns HTTP 409 Conflict

#### Scenario: Target participant not in game returns 404

- **WHEN** the hunter calls the tag endpoint with a participantId that does not exist in the game
- **THEN** the system returns HTTP 404 Not Found

#### Scenario: Game not InProgress returns 409

- **WHEN** the hunter calls the tag endpoint for a game that is not in InProgress state
- **THEN** the system returns HTTP 409 Conflict

### Requirement: Hunter HUD displays Tag Player button

The hunter view SHALL display a "Tag Player" button in the HUD. The button SHALL be disabled while the current time is before the game's `hunterMayMoveAt` and SHALL become available once `hunterMayMoveAt` has passed, without requiring a new status poll. When tapped, the application SHALL request `GET /games/{gameId}/status` to retrieve the current participant list, then present a modal or action sheet listing only prey participants whose `State` is `Active` or `Passive`. After the hunter selects a prey and confirms, the application SHALL call `POST /games/{gameId}/participants/{participantId}/tag`. The button SHALL be disabled while a tagging request is in flight.

#### Scenario: Tag Player button visible in hunter HUD

- **WHEN** the hunter view is active and the game is InProgress
- **THEN** a "Tag Player" button is visible in the HUD panel

#### Scenario: Tag Player button disabled during the hunter delay

- **WHEN** the hunter view is active and the current time is before `hunterMayMoveAt`
- **THEN** the "Tag Player" button is disabled

#### Scenario: Tag Player button becomes available when the delay expires

- **WHEN** the local countdown to `hunterMayMoveAt` reaches zero
- **THEN** the "Tag Player" button becomes enabled without waiting for the next status poll

#### Scenario: Tag Player list shows only Active and Passive preys

- **WHEN** the hunter taps "Tag Player"
- **THEN** the presented list contains only preys with State Active or Passive; Tagged and Out preys are excluded

#### Scenario: Confirming selection calls the tag endpoint

- **WHEN** the hunter selects a prey from the list and confirms
- **THEN** the application calls POST /games/{gameId}/participants/{participantId}/tag for the selected participantId

#### Scenario: Button disabled during in-flight request

- **WHEN** a tagging request is in flight
- **THEN** the "Tag Player" button is disabled and cannot be tapped again

#### Scenario: Successful tag dismisses the modal and updates the HUD

- **WHEN** the server returns 204 for the tag request
- **THEN** the modal is dismissed and the HUD preys-remaining count is updated

