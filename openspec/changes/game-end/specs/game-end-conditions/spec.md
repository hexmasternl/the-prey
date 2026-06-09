## ADDED Requirements

### Requirement: Prey can be eliminated with a reason

The `Game` aggregate SHALL support eliminating a prey participant via `EliminatePrey(userId, reason)` where `reason` is one of `Tagged` or `Out`. Each `GameParticipant` SHALL carry a `ParticipantActiveStatus` (default `Active`) that tracks whether the participant is `Active`, `Tagged`, or `Out`. `EliminatePrey` SHALL only succeed when the game is `InProgress` and the target participant has role `Prey`. The operation SHALL be idempotent if called twice with the same reason; it SHALL reject a status change from `Tagged` to `Out` or vice versa.

#### Scenario: Active prey is eliminated as Tagged

- **WHEN** `EliminatePrey` is called for an `Active` prey with reason `Tagged` on an `InProgress` game
- **THEN** the prey's `ParticipantActiveStatus` is set to `Tagged`

#### Scenario: Active prey is eliminated as Out

- **WHEN** `EliminatePrey` is called for an `Active` prey with reason `Out` on an `InProgress` game
- **THEN** the prey's `ParticipantActiveStatus` is set to `Out`

#### Scenario: Eliminating a prey in a non-InProgress game is rejected

- **WHEN** `EliminatePrey` is called on a game that is not `InProgress`
- **THEN** the aggregate throws an `InvalidOperationException` and the participant status is unchanged

#### Scenario: Eliminating the hunter is rejected

- **WHEN** `EliminatePrey` is called with the hunter's user ID
- **THEN** the aggregate throws an `InvalidOperationException`

#### Scenario: Eliminating a non-participant is rejected

- **WHEN** `EliminatePrey` is called with a user ID that is not a participant of the game
- **THEN** the aggregate throws an `InvalidOperationException`

### Requirement: Game ends when all preys are eliminated

The system SHALL automatically transition an `InProgress` game to `Completed` when all preys have a `ParticipantActiveStatus` of `Tagged` or `Out`. When this condition is detected, the `Winner` SHALL be `Hunter` and `EndedAt` SHALL be set to the current UTC time. The system SHALL publish a `state-changed` SSE event carrying `newState: "Completed"` and `winner: "Hunter"`.

#### Scenario: Last prey is eliminated and game ends

- **WHEN** `EliminatePrey` is called and the updated state leaves no `Active` preys
- **THEN** the game transitions to `Completed`, `Winner` is set to `Hunter`, `EndedAt` is set to the current time, and a `state-changed` event is published

#### Scenario: At least one active prey remains after elimination

- **WHEN** `EliminatePrey` is called but at least one other prey is still `Active`
- **THEN** the game remains `InProgress` and no `state-changed` event is published

### Requirement: Game ends when scheduled duration expires

The system SHALL automatically detect when an `InProgress` game's `ScheduledEndAt` (start time plus `GameDuration`) has passed and transition it to `Completed`. If at least one prey is still `Active` at expiry, the `Winner` SHALL be `Preys`. If all preys are `Tagged` or `Out` at expiry, the `Winner` SHALL be `Hunter`. `EndedAt` SHALL be set to the current UTC time. The system SHALL publish a `state-changed` SSE event carrying `newState: "Completed"` and the determined `winner`.

#### Scenario: Time expires with at least one active prey

- **WHEN** the current time has passed `ScheduledEndAt` and at least one prey has `ParticipantActiveStatus` of `Active`
- **THEN** the game transitions to `Completed`, `Winner` is set to `Preys`, and a `state-changed` event with `winner: "Preys"` is published

#### Scenario: Time expires with all preys eliminated

- **WHEN** the current time has passed `ScheduledEndAt` and all preys have `ParticipantActiveStatus` of `Tagged` or `Out`
- **THEN** the game transitions to `Completed`, `Winner` is set to `Hunter`, and a `state-changed` event with `winner: "Hunter"` is published

#### Scenario: InProgress game before expiry is not ended

- **WHEN** the current time has not yet passed `ScheduledEndAt`
- **THEN** the game remains `InProgress` and no state transition or event is triggered

### Requirement: A background service drives time-based game end detection

The system SHALL provide a `GameEndHostedService` (`BackgroundService`) registered in the Games module. It SHALL poll the repository at a configurable interval (default 10 seconds) for all `InProgress` games. For each game whose `ScheduledEndAt` has passed, it SHALL dispatch `EndGameCommand`. The service SHALL be resilient to individual game failures — an exception processing one game SHALL log the error and continue processing the remaining games.

#### Scenario: Service ends an expired game

- **WHEN** the hosted service runs and finds an `InProgress` game whose `ScheduledEndAt` has passed
- **THEN** it dispatches `EndGameCommand` for that game

#### Scenario: Service skips non-expired InProgress games

- **WHEN** the hosted service runs and an `InProgress` game's `ScheduledEndAt` has not yet passed
- **THEN** it does not dispatch `EndGameCommand` for that game

#### Scenario: Service continues after a single-game failure

- **WHEN** the hosted service encounters an exception processing one game
- **THEN** it logs the error and continues to process the remaining games in the same poll cycle

### Requirement: EndGame command is idempotent

`EndGameCommand` SHALL be a no-op if the target game is already `Completed`. This prevents race conditions between time-based expiry and all-preys-eliminated paths firing simultaneously.

#### Scenario: EndGame on already-Completed game is a no-op

- **WHEN** `EndGameCommand` is dispatched for a game that is already in the `Completed` state
- **THEN** the handler returns successfully without modifying the game or publishing any event

### Requirement: Game aggregate tracks Winner and EndedAt

The `Game` aggregate SHALL carry a nullable `Winner` field (enum: `Hunter`, `Preys`) and a nullable `EndedAt` (`DateTimeOffset?`). Both SHALL be null until the game completes. `Game.Complete(winner, endedAt)` SHALL set both fields and transition status to `Completed`. Calling `Complete` on an already-`Completed` game SHALL throw `InvalidOperationException`.

#### Scenario: Winner and EndedAt set on completion

- **WHEN** `Game.Complete(winner, endedAt)` is called on an `InProgress` game
- **THEN** `Game.Status` is `Completed`, `Game.Winner` equals the supplied winner, `Game.EndedAt` equals the supplied timestamp

#### Scenario: Completing an already-Completed game throws

- **WHEN** `Game.Complete` is called on a game that is already `Completed`
- **THEN** the aggregate throws `InvalidOperationException`

### Requirement: state-changed SSE event carries winner

When a game transitions to `Completed`, the `state-changed` SSE event data SHALL include a `winner` field containing `"Hunter"` or `"Preys"`.

#### Scenario: state-changed event includes winner on game end

- **WHEN** the game transitions to `Completed` and a `state-changed` event is published via `IGameEventBus`
- **THEN** connected SSE clients receive a `state-changed` event with `newState: "Completed"` and `winner` set to the determined winner string
