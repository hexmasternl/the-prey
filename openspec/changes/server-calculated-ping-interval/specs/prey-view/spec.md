## ADDED Requirements

### Requirement: NEXT UPDATE progress bar driven by server-supplied ping timing

The prey view's "NEXT UPDATE" HUD progress bar SHALL render its fill from server-supplied values rather than client-derived dates. On every status snapshot (fetched or pushed), the view SHALL seed the per-second countdown from the response's `nextPingDuration` and SHALL use the response's `currentPingInterval` as the bar's full capacity (denominator). The bar fill percentage SHALL be `countdown / currentPingInterval × 100`, clamped to the 0–100 range. The countdown SHALL tick down once per second client-side until the next snapshot re-seeds it. The bar capacity SHALL NOT be derived from the UI poll cadence. When `currentPingInterval` is 0 or missing, the view SHALL fall back to a 30-second capacity to avoid divide-by-zero.

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
