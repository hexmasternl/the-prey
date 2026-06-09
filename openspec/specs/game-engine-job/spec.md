# game-engine-job Specification

## Purpose

Defines the Azure Container Apps Job that runs the game engine. The Job is triggered by messages on an Azure Storage Queue, loads the full game state from PostgreSQL, manages the engine lifetime for the duration of the game, and sends a final broadcast before exiting.

## Requirements

### Requirement: Queue-triggered engine startup

The system SHALL start a game engine instance by placing a message containing the `gameId` (a non-empty string) onto an Azure Storage Queue. An Azure Container Apps Job SHALL be configured to trigger on messages from this queue, starting exactly one Job execution per message. On startup the Job SHALL load the full game record — including all participants, their location histories, and their penalties — from PostgreSQL. The Job SHALL reject (dead-letter) the message if the `gameId` is absent, empty, or does not correspond to a known game in an InProgress state.

#### Scenario: Valid gameId starts the engine

- **WHEN** a message containing a valid `gameId` of an InProgress game is placed on the queue
- **THEN** the Container Apps Job starts, loads the game from PostgreSQL, and begins scheduling the GameLocationChecker

#### Scenario: Missing or empty gameId is rejected

- **WHEN** a message with an absent or empty `gameId` arrives on the queue
- **THEN** the Job dead-letters the message and exits without starting the engine

#### Scenario: Unknown gameId is rejected

- **WHEN** a message contains a `gameId` that does not match any persisted game
- **THEN** the Job dead-letters the message and exits without starting the engine

#### Scenario: GameId for a non-InProgress game is rejected

- **WHEN** a message contains a `gameId` for a game that is not in the InProgress state
- **THEN** the Job dead-letters the message and exits without starting the engine

### Requirement: Engine lifetime tied to game duration

The game engine SHALL remain running from startup until the game is determined to have ended. The engine SHALL determine the game's scheduled end time as `StartTime + GameDuration` (in minutes). The engine SHALL poll the game record in PostgreSQL at the start of each broadcast cycle to detect whether the game has transitioned to the Completed state ahead of schedule (e.g. ended early by the owner).

#### Scenario: Engine exits after scheduled game end

- **WHEN** the current time reaches or passes `StartTime + GameDuration`
- **THEN** the engine performs a final broadcast of all player locations and then exits cleanly

#### Scenario: Engine exits when game is marked Completed early

- **WHEN** the engine detects on a broadcast cycle that the game's status is Completed
- **THEN** the engine performs a final broadcast of all player locations and then exits cleanly

### Requirement: Final broadcast before exit

Before the Container Apps Job exits — whether due to scheduled game end or early completion — the engine SHALL update `GameParticipant.Location` for every participant with their last known position from their location history, then call `POST /game-engine/{gameId}/location-update` with the full participant list. This ensures all clients receive a definitive final state.

#### Scenario: All players broadcasted on exit

- **WHEN** the engine determines the game has ended
- **THEN** it reads the last known location for every participant from their location history, updates each participant's `Location` property in PostgreSQL, and calls the location-update endpoint with all participants before exiting

### Requirement: Startup enqueue by the Games API

When the `StartGame` command handler successfully persists the game in the InProgress state, it SHALL enqueue a message containing the `gameId` onto the Azure Storage Queue that triggers the game engine. This enqueue SHALL happen within the same logical operation as the game state transition, and if enqueuing fails, the start operation SHALL be considered failed.

#### Scenario: StartGame enqueues the engine trigger

- **WHEN** the owner successfully starts a game
- **THEN** a message containing the `gameId` is placed on the Azure Storage Queue before the HTTP response is returned to the caller

#### Scenario: Enqueue failure rolls back the start

- **WHEN** placing the message on the queue fails during the StartGame command
- **THEN** the game is not transitioned to InProgress and the caller receives an error response
