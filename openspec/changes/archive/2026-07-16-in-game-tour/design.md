## Context

The live game runs in two standalone Angular pages in `src/ThePrey`: `game-hunter.page` and
`game-prey.page`. Both render a collapsible HUD inside `ion-content`:
- collapsed → a tappable `.hud-bar` (`(click)="toggleHud()"`) showing the mission time;
- expanded → a `.hud-grid` with a `.hud-toggle` to collapse again.
State is a `hudExpanded` signal toggled by `toggleHud()`.

The **hunter** page additionally renders a `.tag-fab` button (`(click)="openTagModal()"`) used to
tag a nearby prey. The **prey** page has no tag button.

Both pages open with a "surroundings warning" the player must acknowledge, and the hunter page
shows a head-start delay overlay (`hunter-delay-overlay.component`) before the hunter may move.
That component establishes the in-page overlay pattern: a `position:absolute; inset:0` layer
inside `ion-content`, themed with the phosphor-green tactical tokens (`--tp-signal`, `--tp-bg-void`,
`--tp-body`/`--tp-head` fonts).

Local, per-device preferences are persisted with `@capacitor/preferences` (see
`language.service.ts`). There is no existing tour/coachmark mechanism.

## Goals / Non-Goals

**Goals:**
- A one-time guided tour per role, shown on first entry to the live view.
- Prey tour: one step on the time/HUD bar (tap to expand/collapse).
- Hunter tour: the time-bar step plus a second step on the tag button.
- Separate persisted "seen" flags for hunter vs prey, so each role's tour shows independently.
- Reuse the existing overlay/design-system conventions; no new third-party dependency.

**Non-Goals:**
- No tour for any other screen (lobby, home, settings).
- No replay/reset UI in this change (flags can be cleared by reinstall; a settings "replay tour"
  toggle is out of scope).
- No server-side persistence — "seen" is per device, like the language preference.
- No change to HUD/tag behavior itself; the tour only points at existing controls.

## Decisions

### 1. A reusable tour overlay component driven by steps
Add a standalone `GameTourComponent` that renders a full-screen (`position:absolute; inset:0`)
coachmark layer inside the page's `ion-content`, matching `hunter-delay-overlay`. It takes:
- `steps`: an ordered list of `{ targetRect, titleKey, bodyKey }` (or a target `ElementRef`/selector
  resolved to a rect), and
- emits `completed` when the user advances past the last step or dismisses.

Each step renders a highlight ring around the target element's bounding rect and a tooltip card
(translated title/body) with a **Next** button (→ **Got it** on the final step) and a **Skip**
affordance. The component measures the target with `getBoundingClientRect()` on show and on
`resize`/`orientationchange`.
- *Why a custom component over a library (e.g. shepherd.js, intro.js)?* The app avoids extra
  dependencies, the design system is bespoke (phosphor-green), and the need is two short tours.
  A ~1-step/2-step coachmark is small and keeps full control of the tactical styling.

### 2. Page-owned anchors, page triggers the tour
The pages own the anchored elements, so each page passes the relevant element references to the
overlay:
- prey: the HUD bar element;
- hunter: the HUD bar element + the `.tag-fab` element.
Use template reference variables + `viewChild`/`viewChildren` (signal queries) to get
`ElementRef`s, rather than the overlay reaching into the DOM by global selector, so the tour stays
decoupled from page markup details.
- *Anchoring to the collapsed bar*: the time-bar step points at the collapsed `.hud-bar`. The tour
  shows with the HUD in its default (collapsed) state so the "tap to expand/collapse" hint matches
  what the user sees.

### 3. Trigger timing: after the blocking start overlays clear
Show the tour only once the live view is actually interactive and the anchored controls are
visible and unobscured — i.e. **after** the surroundings warning is acknowledged and (hunter) the
head-start delay overlay has cleared. Concretely, gate the tour on: game status is in-progress,
the warning/delay overlays are not showing, and the role's "seen" flag is false.
- *Why defer?* If shown under the warning/delay overlays, the highlight would point at covered or
  not-yet-rendered controls. Deferring guarantees the tag button and HUD bar exist and are tappable.

### 4. Tour-state service with two preference keys
Add a `TourService` (`providedIn: 'root'`) wrapping `@capacitor/preferences`:
- `hasSeen(role: 'hunter' | 'prey'): Promise<boolean>`
- `markSeen(role: 'hunter' | 'prey'): Promise<void>`

Backed by two keys, e.g. `tour.seen.hunter` and `tour.seen.prey`. Separate keys satisfy
"maintain the hunter view tour and the prey tour as separate settings" and let each role's tour
fire independently the first time that role is played.
- `markSeen` is called when the tour completes **or** is skipped — either way the player has been
  offered it, so it should not reappear.

### 5. Tour content (i18n)
New `GAME_TOUR` strings in `en.json`/`nl.json`:
- `TIME_BAR_TITLE` / `TIME_BAR_BODY` — "tap the bar to expand and collapse the HUD".
- `TAG_TITLE` / `TAG_BODY` — "when a prey is in range, use this to tag them" (hunter only).
- `NEXT`, `DONE`, `SKIP` — controls.

## Risks / Trade-offs

- **Anchor rect is wrong if the element moves/resizes** (rotation, HUD expanded, keyboard) →
  Mitigation: measure on show and recompute on `resize`/`orientationchange`; show with HUD in its
  default collapsed state; the highlight is forgiving (a padded ring), not pixel-exact.
- **Tour fires before controls exist** (status flips to in-progress while overlays still up) →
  Mitigation: Decision 3 gates on overlays cleared; if a target ref is still missing when the tour
  would show, skip that step rather than render a detached highlight.
- **Preferences write fails / web has no native storage** → `@capacitor/preferences` has a web
  fallback; if a read throws, treat as "not seen" (fail toward showing the tour once) and if a
  write throws the worst case is the tour shows again next session — acceptable, non-blocking.
- **Player switches role mid-game (hunter reassignment)** → each role's flag is independent, so a
  prey promoted to hunter will see the hunter tour on first entry to the hunter view; this is the
  intended behavior.

## Migration Plan

1. Ship the component + service + page wiring. Existing players who already know the UI will see
   each tour once on their next first-entry per role (flags start unset); after that it is silent.
2. **Rollback**: the feature is additive and self-contained; removing the page trigger disables it.
   No data migration — the two preference keys are orphaned harmlessly if the feature is removed.

## Open Questions

- Confirm the view mapping (prey = time bar only; hunter = time bar + tag button), per the
  proposal note.
- Should a "replay tutorial" control be added to Settings later? (Out of scope here; the flags are
  designed to support it.)
