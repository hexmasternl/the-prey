# Spec Delta

## Purpose

Lets the Games module hand every participant's outcome statistics to other modules once a running game completes, without sharing raw location data.

## ADDED Requirements

### Requirement: Publish game-completed once per completed game
When a game in progress completes, Games SHALL publish exactly one `game-completed` integration event for that game after the completed game is saved. A game cancelled from the lobby SHALL NOT produce the event.

#### Scenario: Duration elapses
- **WHEN** the sweep completes an in-progress game because its duration elapsed
- **THEN** one `game-completed` event is published with outcome `PreysWin`

#### Scenario: Last prey tagged
- **WHEN** the hunter tags the last Active or Passive prey
- **THEN** one `game-completed` event is published with outcome `HuntersWin`
- **AND** a later sweep of that game publishes no second event

#### Scenario: Owner cancels the lobby
- **WHEN** the owner leaves a game in `Lobby` or `Ready`
- **THEN** no `game-completed` event is published

### Requirement: Event carries one entry per participant
The event SHALL carry the game id, start and completion times, the outcome, the configured duration and one entry for every participant at completion, including prey that went Out. Entries SHALL contain no coordinates.

#### Scenario: Prey timed out
- **WHEN** a game completes with one prey `Out` and one prey `Tagged`
- **THEN** the event has an entry for the hunter and for both prey

#### Scenario: Player left the lobby
- **WHEN** a player left the lobby before the game started
- **THEN** the event has no entry for that player

### Requirement: Role and side result
Each entry SHALL state the player's role at completion and a result for their side: `won`, `lost` or `undecided`. On `HuntersWin` the hunter won and every prey lost; on `PreysWin` every prey won and the hunter lost; on `Undecided` everyone is undecided.

#### Scenario: Tagged prey on the winning side
- **WHEN** the game ends `PreysWin` and a prey was tagged earlier
- **THEN** that prey's result is `won` and its `survivedToEnd` is false

### Requirement: Survival time for prey
Each prey entry SHALL give survival time in seconds from game start until the prey was caught, or until completion when it was not, clamped between zero and the game duration. A tag ends survival at the tag time; going Out ends it at the prey's last location report.

#### Scenario: Prey survives
- **WHEN** a prey is Active or Passive at completion
- **THEN** its survival time is the time from game start to completion and `survivedToEnd` is true

#### Scenario: Prey goes silent
- **WHEN** a prey's last report is 12 minutes after game start and it times out to Out
- **THEN** its survival time is 720 seconds

#### Scenario: Hunter entry
- **WHEN** an entry's role is Hunter
- **THEN** it has no survival time

### Requirement: Tags credited to the tagger
Each entry SHALL count the prey this player tagged, even when the Hunter role moved to another player later in the game.

#### Scenario: Hunter hands over the role
- **WHEN** player A tags one prey, hands the Hunter role to player B, and B tags two prey
- **THEN** A's entry has 1 tag made and B's entry has 2 tags made

### Requirement: Distance covered with GPS filtering
Each entry SHALL give the distance in whole metres along the player's readings from game start until caught or completion, ignoring segments that imply more than 12 m/s and movement within 10 m of the last counted point.

#### Scenario: Single GPS jump
- **WHEN** a player walks 500 m and one reading in between lies 2 km away
- **THEN** the reported distance is about 500 m

#### Scenario: Standing still
- **WHEN** a player's readings wander within a 5 m radius for the whole game
- **THEN** the reported distance is 0

### Requirement: Penalties received
Each entry SHALL give the number of penalties the player received during the game, counting boundary and head-start penalties.

#### Scenario: Hunter moved during head start and left the playfield
- **WHEN** the hunter received the head-start penalty and one boundary penalty
- **THEN** the hunter's entry has 2 penalties received
