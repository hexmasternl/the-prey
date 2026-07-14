## ADDED Requirements

### Requirement: Phone is the sole authenticated data leader

The phone app SHALL remain the only authenticated client that fetches game data (the existing
status poll and Web PubSub stream), and the watch SHALL NOT authenticate, hold Auth0 credentials,
or call the backend. All game data reaching the watch SHALL originate from the phone over the Wear
OS Data Layer.

#### Scenario: Watch never contacts the backend

- **WHEN** the companion is in use during a game
- **THEN** the watch obtains all game data from the phone via the Data Layer and makes no backend or Auth0 request of its own

#### Scenario: No login on the watch

- **WHEN** a player uses the watch companion
- **THEN** the watch presents no login flow and relies entirely on the phone's existing authenticated session

### Requirement: Push latest game data on update

Whenever the phone receives new game data (a status poll result or a stream event), it SHALL
publish the latest relevant game snapshot to the watch as a Data Layer data item, provided a watch
running the companion app is available.

#### Scenario: New status pushed

- **WHEN** the phone's status poll or stream produces updated game data and a companion watch is available
- **THEN** the phone publishes a snapshot data item carrying the latest relevant game data (timers seed, penalty state, role, and the nearest-threat and player last-known locations) with a capture timestamp

#### Scenario: Snapshot is the latest-known value

- **WHEN** the watch reads the snapshot data item after being asleep or reconnecting
- **THEN** it receives the most recently published snapshot (the data item is cached/synced), not a missed intermediate value

### Requirement: Forward discrete events promptly

The phone SHALL forward latency-sensitive discrete events — at least `player-penalized` and
`game-ended` — to the watch as Data Layer messages, in addition to the periodic snapshot.

#### Scenario: Penalty event forwarded

- **WHEN** the phone receives a `player-penalized` event for the current player and a watch is available
- **THEN** the phone sends a corresponding message to the watch carrying the penalty end time and reason

#### Scenario: Game-ended forwarded

- **WHEN** the phone receives a `game-ended` event and a watch is available
- **THEN** the phone sends a game-ended message so the watch can show its end-of-game state

### Requirement: Watch availability handling

The phone SHALL determine whether a watch running the companion app is connected before attempting
to push, and SHALL skip pushing without error when none is available, resuming automatically when a
watch becomes available.

#### Scenario: No watch connected

- **WHEN** no watch running the companion app is connected
- **THEN** the phone does not attempt to push, raises no user-visible error, and continues normal gameplay

#### Scenario: Watch becomes available mid-game

- **WHEN** a companion watch connects during an active game
- **THEN** the phone publishes the current snapshot so the newly available watch is brought up to date

### Requirement: No-active-game signal

When the player has no active game, the phone SHALL publish a "no active game" state (or clear the
snapshot) so the watch can present its idle screen rather than stale data.

#### Scenario: No active game

- **WHEN** there is no active game for the player
- **THEN** the phone publishes a state indicating no active game (or clears the snapshot data item)

#### Scenario: Game completes

- **WHEN** the active game completes
- **THEN** the phone publishes the completed/no-active-game state after the end-of-game event

### Requirement: Relay stays alive during a game

The phone SHALL keep the data connection and relay alive for the duration of an active game via a
foreground service, so snapshots continue to be pushed while the phone screen is off or the app is
backgrounded, and SHALL tear the service down when the game ends.

#### Scenario: Screen off during a game

- **WHEN** an active game is in progress and the phone screen turns off
- **THEN** the foreground service keeps the connection alive and the phone continues to push updated snapshots to the watch

#### Scenario: Service torn down after game

- **WHEN** the active game ends
- **THEN** the phone stops the foreground service and ceases relaying

### Requirement: Native bridge exposes the relay to the app

The phone SHALL implement the Data Layer relay as a native bridge (a Capacitor plugin) that the
Angular application drives, exposing at least publish-snapshot, send-event, clear, and
watch-availability operations, so the existing status/stream data can be relayed without changing
the backend.

#### Scenario: App publishes through the bridge

- **WHEN** the Angular relay service has new game data
- **THEN** it calls the native bridge to publish the snapshot, without the web layer needing direct access to the Data Layer APIs
