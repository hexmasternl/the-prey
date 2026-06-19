# tag-player-action Specification

## Purpose
Defines the server-side endpoint and client-side UX for the hunter to tag a prey participant, marking them as permanently out of play.

## Requirements

### Requirement: POST /games/{gameId}/participants/{participantId}/tag marks prey as Tagged

The system SHALL expose `POST /games/{gameId}/participants/{participantId}/tag`. The endpoint SHALL require authentication. On success the endpoint SHALL return HTTP 204 No Content.

The system SHALL validate:
1. The authenticated caller is the hunter of the specified game.
2. The game is in `InProgress` state.
3. The target participant exists in the game, has role `Prey`, and has `PlayerState` of `Active` or `Passive`.
4. The target prey is within tagging range: the distance between the hunter's most recent emitted location and the target prey's most recent emitted location SHALL be less than or equal to 50 meters. Distance SHALL be computed using the great-circle (Haversine) distance, using the latest reading by recorded timestamp from each participant's location history.

On success the system SHALL set the target participant's `PlayerState` to `Tagged` (irreversible) and publish a `participant-status-changed` event via `IGameEventBus`.

#### Scenario: Hunter successfully tags an Active prey

- **WHEN** the authenticated hunter calls POST /games/{gameId}/participants/{participantId}/tag and the target prey is Active and within 50 m
- **THEN** the system returns HTTP 204, sets the prey's PlayerState to Tagged, and publishes a participant-status-changed event

#### Scenario: Hunter successfully tags a Passive prey

- **WHEN** the authenticated hunter calls POST /games/{gameId}/participants/{participantId}/tag and the target prey is Passive and within 50 m
- **THEN** the system returns HTTP 204, sets the prey's PlayerState to Tagged, and publishes a participant-status-changed event

#### Scenario: Tagging a prey that is out of range is rejected with 409

- **WHEN** the hunter calls the tag endpoint for an Active or Passive prey whose most recent emitted location is more than 50 m from the hunter's most recent emitted location
- **THEN** the system returns HTTP 409 Conflict and the prey's state is unchanged

#### Scenario: Tagging when the hunter has no emitted location is rejected with 409

- **WHEN** the hunter calls the tag endpoint but the hunter's location history is empty
- **THEN** the system returns HTTP 409 Conflict

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

The hunter view SHALL display a "Tag Player" button in the HUD. When tapped, the application SHALL request `GET /games/{gameId}/tag-candidates` to retrieve the preys currently within tagging range, then present a modal or action sheet listing only the returned candidate preys. After the hunter selects a prey and confirms, the application SHALL call `POST /games/{gameId}/participants/{participantId}/tag`. The button SHALL be disabled while a tagging request is in flight.

#### Scenario: Tag Player button visible in hunter HUD

- **WHEN** the hunter view is active and the game is InProgress
- **THEN** a "Tag Player" button is visible in the HUD panel

#### Scenario: Tag Player list shows only preys returned by the tag-candidates endpoint

- **WHEN** the hunter taps "Tag Player"
- **THEN** the application fetches GET /games/{gameId}/tag-candidates and the presented list contains exactly the preys returned (those within 50 m and in state Active or Passive)

#### Scenario: No preys in range shows an empty-state message

- **WHEN** the hunter taps "Tag Player" and the tag-candidates endpoint returns no candidates
- **THEN** the presented list shows a "no preys in range" message and no prey is selectable

#### Scenario: Confirming selection calls the tag endpoint

- **WHEN** the hunter selects a prey from the list and confirms
- **THEN** the application calls POST /games/{gameId}/participants/{participantId}/tag for the selected participantId

#### Scenario: Button disabled during in-flight request

- **WHEN** a tagging request is in flight
- **THEN** the "Tag Player" button is disabled and cannot be tapped again

#### Scenario: Successful tag dismisses the modal and updates the HUD

- **WHEN** the server returns 204 for the tag request
- **THEN** the modal is dismissed and the HUD preys-remaining count is updated
