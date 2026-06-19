## Why

When a game starts, players are dropped straight into the live hunter/prey view with no
explanation of the HUD. Two things are non-obvious: the collapsible time/HUD bar can be tapped
to expand and collapse, and (for hunters) the tag button is how you capture a nearby prey. A
one-time guided tour on first play teaches these without cluttering the experience for returning
players.

## What Changes

- On the first time a player reaches the **in-progress prey view**, show a short guided tour
  (coachmark-style tooltip) that highlights the **time/HUD bar** and explains it can be tapped
  to expand and collapse.
- On the first time a player reaches the **in-progress hunter view**, show the same time-bar
  step **plus** a second step highlighting the **tag button** and explaining it is used to tag a
  nearby prey.
- The tour is shown **once per role**: it never reappears once completed/dismissed for that role.
- The **hunter** tour and the **prey** tour are tracked by **separate** persisted flags, so a
  player who has seen one still sees the other the first time they take that role.

> **Note on view mapping** — the request named the hunter view twice. The tag button exists only
> on the hunter view, and the prey view has just the time bar. So this proposal maps the
> time-bar-only tour to the **prey** view and the time-bar + tag-button tour to the **hunter**
> view, which also matches "maintain the hunter view tour and the prey tour as separate
> settings." Flag if a different mapping was intended.

## Capabilities

### New Capabilities
- `in-game-tour`: A one-time, per-role guided tour shown on entering the live hunter/prey view,
  highlighting the time/HUD bar (both roles) and the tag button (hunter only), with separate
  persisted "seen" flags per role.

### Modified Capabilities
<!-- None — no existing spec's requirements change. -->

## Impact

- **Client only** — `src/ThePrey`. No backend changes.
- New reusable tour overlay component + a small tour-state service (persists "seen" flags via
  `@capacitor/preferences`, two keys).
- `game-hunter.page.*` and `game-prey.page.*` — trigger the tour on first entry, expose anchor
  elements (time bar, tag button) for the overlay to highlight.
- i18n resource files (`en.json`, `nl.json`) — new tour copy.
- No new third-party dependency; the overlay reuses the existing in-page overlay pattern
  (`hunter-delay-overlay.component`) and the phosphor-green tactical design system.
