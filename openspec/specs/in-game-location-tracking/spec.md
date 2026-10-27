# in-game-location-tracking Specification

## Purpose
Makes the Android app report the player's precise position to the server every 5 seconds for the whole duration of an active game, even when the app is closed or the screen is off, so leaving the playfield cannot go unnoticed. Tracking runs only during an active game and always ends when the game, or the player's part in it, ends.

## Requirements

### Requirement: Tracking runs only for an active game the player is in

The app SHALL run in-game location tracking only while the signed-in player is a participant of a game in the `Started` or `InProgress` state and is still in play (the hunter, or a prey that is not `Tagged` or `Out`). The app SHALL NOT track location in the menus, in a lobby, or for a `Lobby`, `Ready` or `Completed` game.

#### Scenario: Tracking starts when the game starts

- **WHEN** the player's game enters `Started` or `InProgress` and location permission is granted
- **THEN** tracking starts and its notification is shown

#### Scenario: No tracking in the lobby

- **WHEN** the player is in a game lobby whose state is `Lobby` or `Ready`
- **THEN** no tracking runs and no location is posted

#### Scenario: No tracking for an ended game

- **WHEN** a start of tracking is requested for a game the server reports as `Completed`, or for which the player is not a participant
- **THEN** tracking does not start

#### Scenario: Only one game is tracked at a time

- **WHEN** tracking is already running for a game and a start for the same game is requested again
- **THEN** the running tracking continues unchanged and no second tracker is created

### Requirement: Position is posted every 5 seconds

While tracking runs, the app SHALL request high-accuracy location fixes and post the most recent fix to the server's location endpoint for that game every 5 seconds, including when the app is in the background, removed from recents, or the screen is off. Each post SHALL carry the latitude, longitude, the time the fix was measured, and its accuracy in meters.

#### Scenario: Regular cadence with the screen off

- **WHEN** tracking runs for an InProgress game and the screen is off for one minute
- **THEN** about 12 location posts reach the server during that minute

#### Scenario: App removed from recents

- **WHEN** the player swipes the app away from recents while tracking runs
- **THEN** tracking keeps running and posting

#### Scenario: Fix timestamp is the measurement time

- **WHEN** a location post is sent
- **THEN** its recorded-at time is the time the fix was measured, not the time it was sent

#### Scenario: No new fix available

- **WHEN** a 5-second tick passes and no fix newer than the last posted one is available
- **THEN** no duplicate of the previous fix is posted for that tick

### Requirement: Tracking learns the game's end time on start

When tracking starts, the app SHALL obtain the game's scheduled end time from the server and SHALL stop tracking no later than that time plus a 2-minute grace period. While the end time is not yet known (game still `Started`), the app SHALL use the tracking start time plus the configured game duration plus the grace period as the deadline, and SHALL replace it once the end time is known.

#### Scenario: Deadline stops tracking without a server signal

- **WHEN** tracking runs, the game's end time plus 2 minutes passes, and no end signal reached the device
- **THEN** tracking stops and its notification is removed

#### Scenario: End time not yet known during Started

- **WHEN** tracking starts for a game in `Started` without an end time
- **THEN** tracking runs with the upper-bound deadline and replaces it with the real end time once the game is `InProgress`

### Requirement: Tracking stops when the game or the player's part in it ends

Tracking SHALL stop, and its notification SHALL be removed, as soon as any of these is observed: the game is `Completed`; a location acknowledgement has `keepReporting` false; the server reports the game does not exist or the player is not a participant; the player signs out; the deadline passes. A stopped tracker SHALL NOT restart itself for the same game.

#### Scenario: Game ends by realtime event

- **WHEN** the app receives the `game-ended` event for the tracked game
- **THEN** tracking stops within 5 seconds

#### Scenario: Player tagged

- **WHEN** a location acknowledgement for the tracked game has `keepReporting` false
- **THEN** tracking stops and posts no further locations

#### Scenario: Server rejects posts for an ended game

- **WHEN** a location post is rejected and a follow-up read of the game shows it `Completed`, deleted, or without the player
- **THEN** tracking stops

#### Scenario: Sign-out stops tracking

- **WHEN** the player signs out while tracking runs
- **THEN** tracking stops and the stored tracking session is cleared

#### Scenario: Rejection during Started does not stop tracking

- **WHEN** a location post is rejected because the game is still `Started`
- **THEN** tracking keeps running and posts are accepted once the game is `InProgress`

### Requirement: Tracking resumes after the app restarts

When the app process starts with a signed-in player and no tracking runs, the app SHALL ask the server for the player's active game and start tracking if that game is `Started` or `InProgress` and the player is still in play. If the system restarts a killed tracker, it SHALL re-validate the stored game against the server before posting.

#### Scenario: App reopened during a game

- **WHEN** the player opens the app while a game they are in is `InProgress` and tracking is not running
- **THEN** tracking starts for that game

#### Scenario: Restarted after the game ended

- **WHEN** the system restarts the tracker after the stored game's deadline has passed
- **THEN** tracking does not resume and the stored session is cleared

### Requirement: Resume prompt after a reboot

The platform does not let a location tracker start without player interaction after a reboot or app update. On boot or update, the app SHALL therefore post a high-priority notification for a stored, unexpired tracking session asking the player to resume. Tapping it SHALL open the app, which validates the game and starts tracking.

#### Scenario: Reboot during a game

- **WHEN** the device reboots while the stored tracking session's deadline is in the future
- **THEN** a "game in progress, tap to resume" notification is shown

#### Scenario: Tap resumes tracking

- **WHEN** the player taps the resume notification and the server still reports the game active with the player in play
- **THEN** tracking starts and the resume notification is removed

#### Scenario: Reboot after the game ended

- **WHEN** the device reboots and the stored session's deadline has passed
- **THEN** no notification is shown and the stored session is cleared

### Requirement: Readings survive short network loss

When a location post fails because the network is unavailable or the server errs (5xx or timeout), the app SHALL keep the reading and send buffered readings oldest first once posts succeed again. The buffer SHALL hold at most 10 minutes of readings, dropping the oldest when full, and SHALL be discarded when tracking stops.

#### Scenario: Mobile data switched off

- **WHEN** posts fail for 2 minutes because there is no network, then the network returns
- **THEN** the readings from those 2 minutes are posted in measurement order before new readings

#### Scenario: Buffer bound

- **WHEN** the network is unavailable for longer than 10 minutes
- **THEN** only the most recent 10 minutes of readings are kept

### Requirement: Persistent tracking notification

While tracking runs, the app SHALL show an ongoing notification the player cannot swipe away. It SHALL say tracking is active, show a countdown to the game's end, and open the app when tapped. When location is off or permission is revoked during tracking, it SHALL warn that the server cannot see the player and that this can get them penalized or ruled out. All texts SHALL exist in English and Dutch.

#### Scenario: Notification while tracking

- **WHEN** tracking runs
- **THEN** an ongoing notification with a countdown to the game's end is visible and cannot be dismissed by swiping

#### Scenario: Location switched off mid-game

- **WHEN** the player turns device location off while tracking runs
- **THEN** the notification switches to the warning text, and tracking resumes posting once location is back on

#### Scenario: Dutch device

- **WHEN** the device language is Dutch
- **THEN** the tracking and resume notifications are shown in Dutch

### Requirement: Tracking requires the consented permissions

Tracking SHALL start only when fine location permission has been granted through the location tracking consent flow. When a game becomes active without that permission, the app SHALL show the consent flow before starting tracking and SHALL NOT start tracking if the player declines.

#### Scenario: Permission missing at game start

- **WHEN** the player's game becomes `Started` and fine location permission is not granted
- **THEN** the location tracking disclosure is shown before any system permission prompt

#### Scenario: Player declines

- **WHEN** the player declines the disclosure or the permission
- **THEN** tracking does not start and the app tells the player the server will see them as silent, which can get them ruled out

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
