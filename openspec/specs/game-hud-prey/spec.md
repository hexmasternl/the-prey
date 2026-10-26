# game-hud-prey Specification

## Purpose
The prey's in-game heads-up display in the Android app: an overlay on the game screen that shows the player's role and penalty status, how close the nearest hunter is, the remaining game time and the countdown to the prey's next location broadcast.

## Requirements

### Requirement: Prey HUD shown to an in-progress prey

The Android app SHALL show the prey HUD on the game screen when the signed-in player is a prey participant in a game whose status is `InProgress`. The app SHALL NOT show the prey HUD to a hunter, or for a game in any other status.

#### Scenario: Prey in an in-progress game sees the prey HUD

- **WHEN** the signed-in player opens the game screen of an `InProgress` game in which they are a prey
- **THEN** the prey HUD is shown over the game screen

#### Scenario: Hunter does not see the prey HUD

- **WHEN** the signed-in player opens the game screen of an `InProgress` game in which they are the hunter
- **THEN** the prey HUD is not shown

#### Scenario: Prey HUD hidden outside InProgress

- **WHEN** the game's status is `Started` or `Completed`
- **THEN** the prey HUD is not shown

### Requirement: Role pill shows the prey role

The prey HUD SHALL show a role pill in the top-left corner of the screen. While the player has no active penalty, the pill SHALL be green and read "Prey".

#### Scenario: Role pill without a penalty

- **WHEN** the prey HUD is shown and the player has no active penalty
- **THEN** the top-left pill is green and reads "Prey"

### Requirement: Role pill signals an active penalty

While the player has an active penalty, the role pill SHALL be red and SHALL switch its text between "Prey" and "Penalty" every 1000 ms, starting with "Penalty". When the penalty ends, the pill SHALL return to green "Prey" straight away. The switching SHALL continue when the device has animations turned off, because it carries state rather than decoration.

#### Scenario: Penalty starts

- **WHEN** the player receives a penalty
- **THEN** the role pill turns red and reads "Penalty"

#### Scenario: Penalty text alternates every second

- **WHEN** the player's penalty has been active for 1000 ms
- **THEN** the role pill reads "Prey", and after another 1000 ms it reads "Penalty" again

#### Scenario: Penalty ends

- **WHEN** the player's penalty is cleared or its end time passes
- **THEN** the role pill turns green and reads "Prey", and stops alternating

### Requirement: Proximity pill shows the hunter distance band

The prey HUD SHALL show a proximity pill in the top-right corner, based on the last known distance to the closest hunter in whole meters: 250 m or more SHALL show a green "Safe" pill; from 100 m up to but not including 250 m SHALL show a yellow "Close" pill; under 100 m SHALL show an orange "Warning" pill.

#### Scenario: Hunter far away

- **WHEN** the last known distance to the closest hunter is 250 m
- **THEN** the proximity pill is green and reads "Safe"

#### Scenario: Hunter closing in

- **WHEN** the last known distance to the closest hunter is 249 m
- **THEN** the proximity pill is yellow and reads "Close"

#### Scenario: Hunter at the warning boundary

- **WHEN** the last known distance to the closest hunter is 100 m
- **THEN** the proximity pill is yellow and reads "Close"

#### Scenario: Hunter very close

- **WHEN** the last known distance to the closest hunter is 99 m
- **THEN** the proximity pill is orange and reads "Warning"

### Requirement: Proximity pill when no hunter location is known

When no hunter location is known yet, the proximity pill SHALL be a neutral grey pill that reads "Unknown". It SHALL NOT show "Safe", because the app cannot know whether the prey is safe.

#### Scenario: Hunter has not reported a location yet

- **WHEN** the game state carries no hunter distance and no hunter location
- **THEN** the proximity pill is grey and reads "Unknown"

### Requirement: Closest hunter decides the distance

The hunter distance the HUD shows SHALL be the distance from the prey's position to the closest hunter's last known location. When the game has more than one hunter, the HUD SHALL use the smallest of their distances.

#### Scenario: Two hunters at different distances

- **WHEN** one hunter's last known location is 300 m away and another's is 120 m away
- **THEN** the HUD uses 120 m and the proximity pill reads "Close"

### Requirement: Hunter distance follows live updates

The HUD SHALL take the hunter distance from the game state snapshot, and SHALL recompute it when a realtime update brings a new hunter location or the prey's own position changes, so the pill and the distance tile change without waiting for the next snapshot.

#### Scenario: Hunter location update moves the band

- **WHEN** the HUD shows "Safe" and a location update places the hunter 80 m from the prey
- **THEN** the proximity pill changes to orange "Warning" and the distance tile shows 80 m

### Requirement: Bottom panel expands and collapses

The prey HUD SHALL show a panel at the bottom of the screen with an expanded and a collapsed state. The panel SHALL start expanded. A full-width row at the bottom of the panel SHALL show a downward arrow when expanded and an upward arrow when collapsed; tapping the row SHALL switch the state.

#### Scenario: Collapse the panel

- **WHEN** the panel is expanded and the player taps the arrow row
- **THEN** the panel collapses and the arrow points up

#### Scenario: Expand the panel

- **WHEN** the panel is collapsed and the player taps the arrow row
- **THEN** the panel expands and the arrow points down

#### Scenario: Arrow row is accessible

- **WHEN** a screen reader focuses the arrow row
- **THEN** it announces whether tapping will expand or collapse the panel

### Requirement: Expanded panel layout

In the expanded state the panel SHALL show, from top to bottom: a row of two tiles of equal width and height, with the remaining game time in the left tile and the last known distance to the closest hunter in the right tile; then a full-width next-broadcast tile; then the full-width arrow row.

#### Scenario: Expanded panel contents

- **WHEN** the panel is expanded
- **THEN** the game-time tile and the hunter-distance tile are shown side by side at equal size, above the next-broadcast tile and the arrow row

### Requirement: Collapsed panel layout

In the collapsed state the panel SHALL hide the game-time tile and the hunter-distance tile, and SHALL show only the next-broadcast tile and the arrow row.

#### Scenario: Collapsed panel contents

- **WHEN** the panel is collapsed
- **THEN** only the next-broadcast tile and the arrow row are shown

### Requirement: Remaining game time tile

The game-time tile SHALL show the time left until the game ends, as minutes and seconds (`mm:ss`, or `h:mm:ss` from one hour), ticking down once per second between snapshots. It SHALL be seeded from the server's remaining game duration on every snapshot and SHALL NOT go below `00:00`.

#### Scenario: Time ticks down

- **WHEN** the snapshot reports 754 seconds left
- **THEN** the tile shows `12:34`, and one second later `12:33`

#### Scenario: Time does not go negative

- **WHEN** the countdown reaches zero before the game-ended message arrives
- **THEN** the tile shows `00:00`

### Requirement: Hunter distance tile

The hunter-distance tile SHALL show the last known distance to the closest hunter in meters, in the same color as the proximity pill's current band. When no hunter location is known it SHALL show a dash in the neutral grey.

#### Scenario: Distance shown in band color

- **WHEN** the last known distance to the closest hunter is 180 m
- **THEN** the tile shows `180 m` in yellow

#### Scenario: No distance known

- **WHEN** no hunter location is known
- **THEN** the tile shows a dash in grey

### Requirement: Next-broadcast countdown

The next-broadcast tile SHALL show a progress bar and a number of seconds, both giving the time left until the prey's location is next broadcast to the hunter. On every snapshot the countdown SHALL be seeded from the server's seconds-until-next-broadcast and the bar's full length SHALL be the server's current broadcast interval. The bar's fill SHALL be the seconds left divided by the interval, clamped to 0–100%. The countdown SHALL tick down once per second and SHALL NOT go below zero. When the interval is 0 or missing, the bar SHALL use a 30-second full length.

#### Scenario: Bar seeded from a snapshot

- **WHEN** a snapshot reports 18 seconds until the next broadcast and a 30-second interval
- **THEN** the tile shows `18s` and the bar is 60% full

#### Scenario: Countdown ticks down

- **WHEN** one second passes after the bar shows `18s`
- **THEN** the tile shows `17s` and the bar shrinks accordingly

#### Scenario: Countdown waits at zero

- **WHEN** the countdown reaches zero before the next snapshot arrives
- **THEN** the tile shows `0s` and an empty bar until the next snapshot re-seeds it

#### Scenario: Missing interval falls back to 30 seconds

- **WHEN** a snapshot reports an interval of 0 and 15 seconds until the next broadcast
- **THEN** the bar is 50% full

### Requirement: HUD text in English and Dutch

All text and accessibility descriptions in the prey HUD SHALL be available in English and Dutch and SHALL follow the device language.

#### Scenario: Dutch device

- **WHEN** the device language is Dutch
- **THEN** the HUD's pills, tile labels and arrow description are shown in Dutch
