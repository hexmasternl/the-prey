## ADDED Requirements

### Requirement: NEXT UPDATE progress bar driven by server-supplied ping timing

The hunter view's "NEXT UPDATE" HUD progress bar SHALL render its fill from server-supplied values rather than client-derived dates. On every status snapshot (fetched or pushed), the view SHALL seed the per-second countdown from the response's `nextPingDuration` and SHALL use the response's `currentPingInterval` as the bar's full capacity (denominator). The bar fill percentage SHALL be `countdown / currentPingInterval × 100`, clamped to the 0–100 range. The countdown SHALL tick down once per second client-side until the next snapshot re-seeds it. The bar capacity SHALL NOT be derived from the UI poll cadence. When `currentPingInterval` is 0 or missing, the view SHALL fall back to a 30-second capacity to avoid divide-by-zero.

#### Scenario: Bar capacity uses currentPingInterval

- **WHEN** a status snapshot arrives with `currentPingInterval: 30` and `nextPingDuration: 18`
- **THEN** the NEXT UPDATE bar is filled to 60% and the countdown shows 18s

#### Scenario: Countdown re-seeds on each snapshot

- **WHEN** a new status snapshot arrives with a fresh `nextPingDuration`
- **THEN** the per-second countdown is reset to that `nextPingDuration` and the bar fill recomputes against `currentPingInterval`

#### Scenario: Bar tightens when the interval shortens

- **WHEN** the game enters its final stage and a snapshot arrives with a smaller `currentPingInterval`
- **THEN** the bar's full capacity reflects the smaller interval on the next render

#### Scenario: Missing interval falls back safely

- **WHEN** a status snapshot has `currentPingInterval` of 0 or undefined
- **THEN** the bar uses a 30-second capacity and does not produce a divide-by-zero or NaN width

### Requirement: Waiting-for-start overlay while the game is Ready

When the hunter participant is routed into the hunter view for a game whose status is `Ready` (armed by the host but not yet committed by the server sweep), the view SHALL display a full-screen "waiting for game start" overlay styled like the existing hunter-delay overlay (same dark card treatment), conveying that the game will begin shortly. While the overlay is shown the gameplay HUD MAY be hidden or inert; no ping countdown is started because the game clock has not begun. When the server broadcasts the transition to `InProgress`, the view SHALL store the now-running game (including its `hunterMayMoveAt`), remove the waiting overlay, and proceed exactly as it does on a normal start — showing the hunter-delay countdown overlay and beginning status-driven gameplay. All subsequent ping, penalty, and broadcast timing SHALL be taken from server-supplied values; the client SHALL NOT derive these times from its own clock.

#### Scenario: Ready game shows the waiting overlay

- **WHEN** the hunter is routed into the hunter view while the game status is `Ready`
- **THEN** a "waiting for game start" overlay is displayed and no ping countdown is running

#### Scenario: InProgress broadcast replaces the waiting overlay with the hunter-delay countdown

- **WHEN** the view is showing the waiting overlay and a broadcast announces the game is now `InProgress`
- **THEN** the waiting overlay is removed, the hunter-delay countdown overlay is shown using the server-supplied `hunterMayMoveAt`, and the NEXT UPDATE bar begins driving from server-supplied ping timing

#### Scenario: Timing comes only from the server after start

- **WHEN** the game is `InProgress` and the hunter view renders its ping countdown, penalty indicator, and time-remaining
- **THEN** each value is taken from the latest server status snapshot or broadcast, not computed from the device clock
