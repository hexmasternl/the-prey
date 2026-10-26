# Spec Delta

## Purpose

Makes the Android app report the player's precise position to the server for the whole duration of an active game, so the server can broadcast it to the other players and enforce the playfield rules.

## ADDED Requirements

### Requirement: Tracking starts when the game screen shows an active game

When the app shows the game screen for a game in the `Started` or `InProgress` state and the player is still in play, the app SHALL start in-game location tracking for that game if it does not already run. This SHALL hold whether the player arrived from the lobby as the game started or reopened a running game from the menu, without restarting the app.

#### Scenario: Lobby moves to the game while the app is open

- **WHEN** the owner starts the game, every participant's app moves from the lobby to the game screen, and precise location is granted
- **THEN** each participant's app starts tracking for that game and posts its position to the server within the first reporting interval, without the app being restarted

#### Scenario: Resuming a running game from the menu

- **WHEN** the player opens a `Started` or `InProgress` game they are in play in through "Resume game" and no tracking runs
- **THEN** tracking starts for that game

#### Scenario: Tracking already runs

- **WHEN** the game screen opens for a game whose tracking already runs
- **THEN** the running tracking continues unchanged and no second tracker is created

#### Scenario: Precise location missing

- **WHEN** the game screen opens for an active game and precise location is not granted
- **THEN** the location consent flow is shown; tracking starts if the player grants it, and nothing starts if they decline, after which the player is told the server will see them as silent

#### Scenario: Player no longer in play

- **WHEN** the game screen opens for a game in which the player is `Tagged` or `Out`, or whose tracking already stopped
- **THEN** tracking does not start

### Requirement: Other participants appear on the map once they report

When participants of an active game are tracked, every participant's map SHALL show the other participants the viewer's role may see, from the locations the server broadcasts, without leaving and reopening the game screen.

#### Scenario: Hunter sees the preys

- **WHEN** the preys of an `InProgress` game are tracked and the server broadcasts their locations
- **THEN** the hunter's map shows a marker for every prey with a known location

#### Scenario: Prey sees the hunter and the other preys

- **WHEN** the hunter and the other preys of an `InProgress` game are tracked and the server broadcasts their locations
- **THEN** a prey's map shows the hunter and every other prey with a known location, with eliminated preys shown greyed out
