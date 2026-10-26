## Purpose

The hunter's in-game heads-up display in the Android app: an overlay on the game screen that shows the hunter's role, the remaining game time and penalty state, a summary of the hunt, and the Tag action that lets the hunter eliminate a prey within range.

## ADDED Requirements

### Requirement: Hunter HUD shown to an in-progress hunter

The Android app SHALL show the hunter HUD on the game screen when the signed-in player is the hunter of a game whose status is `InProgress`. The app SHALL NOT show the hunter HUD to a prey, or for a game in any other status.

#### Scenario: Hunter in an in-progress game sees the hunter HUD

- **WHEN** the signed-in player opens the game screen of an `InProgress` game in which they are the hunter
- **THEN** the hunter HUD is shown over the game screen

#### Scenario: Prey does not see the hunter HUD

- **WHEN** the signed-in player opens the game screen of an `InProgress` game in which they are a prey
- **THEN** the hunter HUD is not shown

#### Scenario: Hunter HUD hidden outside InProgress

- **WHEN** the game's status is `Started` or `Completed`
- **THEN** the hunter HUD is not shown

### Requirement: Role pill shows the hunter role

The hunter HUD SHALL show a red role pill in the top-left corner of the screen that reads "Hunter". The pill SHALL NOT change with the hunter's penalty state.

#### Scenario: Role pill for the hunter

- **WHEN** the hunter HUD is shown
- **THEN** the top-left pill is red and reads "Hunter"

### Requirement: Game-clock pill shows live game time

The hunter HUD SHALL show a game-clock pill in the top-right corner that reads "Live" followed by the remaining game time, updated every 1000 ms. The time SHALL be shown as `mm:ss`, or `h:mm:ss` from one hour, and SHALL never go below `00:00`.

#### Scenario: Clock pill counts down

- **WHEN** the game has 12 minutes and 34 seconds left
- **THEN** the pill reads "Live 12:34", and 1000 ms later it reads "Live 12:33"

#### Scenario: Clock pill at game end

- **WHEN** the remaining game time reaches zero before the game-ended update arrives
- **THEN** the pill reads "Live 00:00" and does not go negative

### Requirement: Game-clock pill colour follows the time left

Without an active hunter penalty, the game-clock pill SHALL be green while more than 10 minutes are left, orange while 10 minutes or less but more than 5 minutes are left, and red while 5 minutes or less are left.

#### Scenario: Plenty of time

- **WHEN** 10 minutes and 1 second are left
- **THEN** the pill is green

#### Scenario: Ten-minute boundary

- **WHEN** exactly 10 minutes are left
- **THEN** the pill is orange

#### Scenario: Five-minute boundary

- **WHEN** exactly 5 minutes are left
- **THEN** the pill is red

### Requirement: Game-clock pill signals a hunter penalty

While the hunter has an active penalty, the game-clock pill SHALL be red and read "Penalty" followed by the remaining game time, whatever the time left. When the penalty ends, the pill SHALL return to "Live" and the colour of its time band straight away.

#### Scenario: Hunter receives a penalty

- **WHEN** the hunter has an active penalty and 30 minutes are left
- **THEN** the pill is red and reads "Penalty 30:00"

#### Scenario: Hunter penalty ends

- **WHEN** the hunter's penalty is cleared or its end time passes with 30 minutes left
- **THEN** the pill is green and reads "Live 30:00"

### Requirement: Summary grid of four equal tiles

The hunter HUD SHALL show four tiles at the bottom of the screen, in two rows of two, all of equal width and height: next location ping (top left), preys in play (top right), closest prey distance (bottom left) and penalised preys (bottom right). Each tile SHALL carry a label as well as its value.

#### Scenario: Grid layout

- **WHEN** the hunter HUD is shown
- **THEN** four tiles of equal size are shown at the bottom in two rows and two columns, in the order above

### Requirement: Next location ping tile counts down

The top-left tile SHALL show the seconds until the next scheduled location ping, updated every 1000 ms and never below 0. It SHALL restart from the server's values on each game-state snapshot and each location broadcast.

#### Scenario: Ping countdown ticks

- **WHEN** the next ping is 18 seconds away
- **THEN** the tile shows 18 s, and 1000 ms later 17 s

#### Scenario: Ping countdown restarts on a broadcast

- **WHEN** the countdown shows 0 s and a location broadcast arrives
- **THEN** the tile restarts from the server's next ping value

### Requirement: Preys-in-play tile shows survivors over total

The top-right tile SHALL show the number of prey participants whose state is `Active` or `Passive`, a slash, and the total number of prey participants. It SHALL update when a prey is tagged or goes out.

#### Scenario: Some preys caught

- **WHEN** the game has 6 preys of which 1 is `Tagged` and 1 is `Out`
- **THEN** the tile reads `4/6`

#### Scenario: A prey is tagged

- **WHEN** the tile reads `4/6` and a prey is tagged
- **THEN** the tile reads `3/6`

### Requirement: Closest-prey tile shows the distance to the nearest last known location

The bottom-left tile SHALL show the distance in whole meters from the hunter's own position to the closest last known location of a prey whose state is `Active` or `Passive`. When the hunter's position or every such prey location is unknown, the tile SHALL show a dash instead of a number.

#### Scenario: Two preys located

- **WHEN** one prey's last known location is 320 m away and another's is 85 m away
- **THEN** the tile shows 85 m

#### Scenario: Tagged preys are ignored

- **WHEN** the nearest last known location belongs to a `Tagged` prey at 20 m and the next to an `Active` prey at 140 m
- **THEN** the tile shows 140 m

#### Scenario: No prey location known

- **WHEN** no prey in play has a last known location
- **THEN** the tile shows a dash

### Requirement: Penalised-preys tile counts preys under penalty

The bottom-right tile SHALL show the number of prey participants that have an active penalty, and SHALL update when a prey is penalised or its penalty is cleared.

#### Scenario: Two preys penalised

- **WHEN** two preys have an active penalty
- **THEN** the tile shows 2

#### Scenario: A penalty clears

- **WHEN** the tile shows 2 and one of those preys' penalties is cleared
- **THEN** the tile shows 1

### Requirement: Tag button above the grid

The hunter HUD SHALL show a red floating action button with a target icon and the text "Tag", placed just above the summary grid on the right-hand side. Tapping the enabled button SHALL open the tag sheet.

#### Scenario: Tag button shown

- **WHEN** the hunter HUD is shown
- **THEN** a red "Tag" button with a target icon is shown above the grid on the right

#### Scenario: Tapping Tag opens the sheet

- **WHEN** the hunter taps the enabled Tag button
- **THEN** the tag sheet opens

### Requirement: Tag button locked during the hunter start delay

The Tag button SHALL be disabled while the current server-corrected time is before the game's `hunterMayMoveAt`, and SHALL become enabled when that moment passes, without waiting for a new snapshot. While disabled, its accessibility description SHALL state how long until tagging is allowed.

#### Scenario: Delay still running

- **WHEN** `hunterMayMoveAt` is 2 minutes away
- **THEN** the Tag button is disabled and tapping it does nothing

#### Scenario: Delay ends

- **WHEN** the local countdown to `hunterMayMoveAt` reaches zero
- **THEN** the Tag button becomes enabled

#### Scenario: Delay already over

- **WHEN** the hunter HUD is shown after `hunterMayMoveAt`
- **THEN** the Tag button is enabled

### Requirement: Tag sheet lists the preys in tagging range

When the tag sheet opens, the app SHALL request `GET /games/{id}/tag-candidates` and list each returned prey with its callsign and distance in whole meters. While the request runs the sheet SHALL show a loading state. The app SHALL NOT offer any prey that the server did not return as a candidate.

#### Scenario: Candidates in range

- **WHEN** the server returns two candidates at 12 m and 41 m
- **THEN** the sheet lists both preys with their callsigns and distances

#### Scenario: No prey in range

- **WHEN** the server returns no candidates
- **THEN** the sheet says no prey is within tagging range, shows the range in meters, and offers no tag action

#### Scenario: Candidates request fails

- **WHEN** the candidates request fails or times out
- **THEN** the sheet shows an error with a retry action

### Requirement: Hunter tags a selected prey

The hunter SHALL select one listed prey and confirm to call `POST /games/{id}/participants/{participantId}/tag`. The confirm action SHALL be disabled until a prey is selected and while the tag request is in flight. On success the sheet SHALL close and the HUD SHALL confirm the tag and reflect the prey as tagged.

#### Scenario: Successful tag

- **WHEN** the hunter selects a prey, confirms, and the server returns 204
- **THEN** the sheet closes, a confirmation names the tagged prey, and the preys-in-play tile drops by one

#### Scenario: Confirm needs a selection

- **WHEN** the sheet lists candidates and none is selected
- **THEN** the confirm action is disabled

#### Scenario: No double tag

- **WHEN** a tag request is in flight
- **THEN** the confirm action is disabled and cannot send a second request

### Requirement: Refused tag keeps the sheet honest

When the server refuses the tag with 409 (for example because the prey moved out of range or was already tagged), the sheet SHALL tell the hunter the prey can no longer be tagged and SHALL reload the candidate list. Any other failure SHALL show an error and keep the selection so the hunter can retry.

#### Scenario: Prey moved out of range

- **WHEN** the hunter confirms a tag and the server returns 409
- **THEN** the sheet says the prey can no longer be tagged and shows a freshly loaded candidate list

#### Scenario: Network failure

- **WHEN** the tag request fails without a server response
- **THEN** the sheet shows an error, keeps the prey selected, and the confirm action is enabled again

### Requirement: Hunter HUD text is available in English and Dutch

All text in the hunter HUD, the Tag button and the tag sheet, including accessibility descriptions, SHALL be available in English and Dutch, following the device language.

#### Scenario: Dutch device

- **WHEN** the device language is Dutch
- **THEN** the role pill, clock pill, tile labels, Tag button and tag sheet show Dutch text
