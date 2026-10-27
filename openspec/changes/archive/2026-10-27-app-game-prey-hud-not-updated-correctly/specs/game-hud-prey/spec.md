# Spec Delta

## MODIFIED Requirements

### Requirement: Next-broadcast countdown

The next-broadcast tile SHALL show a progress bar and a number of seconds, both giving the time left until the prey's location is next broadcast to the hunter. On every snapshot the countdown SHALL be seeded from the server's seconds-until-next-broadcast and the bar's full length SHALL be the server's current broadcast interval. The bar's fill SHALL be the seconds left divided by the interval, clamped to 0–100%. The countdown SHALL tick down once per second and SHALL NOT go below zero. When the interval is 0 or missing, the bar SHALL use a 30-second full length.

#### Scenario: Bar seeded from a snapshot

- **WHEN** a snapshot reports 18 seconds until the next broadcast and a 30-second interval
- **THEN** the tile shows `18s` and the bar is 60% full

#### Scenario: Countdown ticks down

- **WHEN** one second passes after the bar shows `18s`
- **THEN** the tile shows `17s` and the bar shrinks accordingly

#### Scenario: Countdown waits at zero

- **WHEN** the countdown reaches zero before a broadcast of the prey's location or a new snapshot arrives
- **THEN** the tile shows `0s` and an empty bar until that broadcast or snapshot restarts it

#### Scenario: Missing interval falls back to 30 seconds

- **WHEN** a snapshot reports an interval of 0 and 15 seconds until the next broadcast
- **THEN** the bar is 50% full

## ADDED Requirements

### Requirement: Next-broadcast countdown restarts on the prey's own broadcast

When a live location broadcast includes the prey's own location, the next-broadcast countdown SHALL start over at the full broadcast interval from the moment the broadcast arrived, without waiting for a snapshot. A broadcast that names only other players SHALL NOT restart it. A later snapshot SHALL seed the countdown again from the server's values.

#### Scenario: Own broadcast restarts the countdown

- **WHEN** the tile shows `0s` with a 180-second interval and a location broadcast arrives that includes the prey's location
- **THEN** the tile shows `180s` and a full bar, and keeps ticking down from there

#### Scenario: Broadcast arrives before the countdown ends

- **WHEN** the tile shows `4s` with a 60-second interval and a location broadcast arrives that includes the prey's location
- **THEN** the tile shows `60s` and a full bar

#### Scenario: Broadcast of another player only

- **WHEN** the tile shows `90s` and a location broadcast arrives that names only a penalised other prey
- **THEN** the tile keeps counting down from `90s` and does not restart

#### Scenario: Snapshot after a broadcast

- **WHEN** the countdown was restarted by a broadcast and then a snapshot arrives reporting 120 seconds until the next broadcast
- **THEN** the tile shows `120s`

### Requirement: Penalised prey restarts on the sweep cadence

While the prey has an active penalty, a restart caused by its own broadcast SHALL use 30 seconds, the cadence at which a penalised player is broadcast, and the bar's full length SHALL be 30 seconds. When the penalty ends, restarts SHALL use the broadcast interval again.

#### Scenario: Penalised prey is broadcast

- **WHEN** the prey has an active penalty and a location broadcast includes the prey's location
- **THEN** the tile shows `30s` and a full bar

#### Scenario: Penalty ended

- **WHEN** the prey's penalty has ended, the broadcast interval is 180 seconds and a location broadcast includes the prey's location
- **THEN** the tile shows `180s` and a full bar
