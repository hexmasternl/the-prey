## 1. Tour state service

- [ ] 1.1 Add `TourService` (`providedIn: 'root'`) in `src/ThePrey/src/app/games/` (or `shared/`) wrapping `@capacitor/preferences` with keys `tour.seen.hunter` and `tour.seen.prey`.
- [ ] 1.2 Implement `hasSeen(role: 'hunter' | 'prey'): Promise<boolean>` (read failure → `false`) and `markSeen(role): Promise<void>` (swallow write errors so play is never blocked).
- [ ] 1.3 Add a unit spec for `TourService` (returns false when unset, true after markSeen, hunter/prey independent, read error → false).

## 2. Tour overlay component

- [ ] 2.1 Add a standalone `GameTourComponent` in `src/ThePrey/src/app/games/` modeled on `hunter-delay-overlay.component` (`position:absolute; inset:0` layer, phosphor-green design tokens).
- [ ] 2.2 Accept an ordered `steps` input where each step carries a target `ElementRef` (or null) plus translated `titleKey`/`bodyKey`; emit a `completed` output when the last step is passed or the tour is skipped.
- [ ] 2.3 For the active step, measure the target via `getBoundingClientRect()` and render a padded highlight ring around it plus a tooltip card with the translated title/body and **Next** / **Done** / **Skip** controls; recompute on `resize`/`orientationchange`.
- [ ] 2.4 If a step's target ref is missing, skip that step rather than render a detached highlight; if no steps remain, emit `completed`.

## 3. Prey view wiring

- [ ] 3.1 In `game-prey.page.html`, add a template ref to the collapsed time/HUD bar element and render `<app-game-tour>` when the prey tour is active.
- [ ] 3.2 In `game-prey.page.ts`, query the HUD-bar `ElementRef` (signal `viewChild`) and add state to trigger the tour once: in-progress + surroundings warning dismissed + `!hasSeen('prey')`.
- [ ] 3.3 Build the single prey step (time bar → `GAME_TOUR.TIME_BAR_*`); on `completed`, call `tourService.markSeen('prey')` and hide the overlay.

## 4. Hunter view wiring

- [ ] 4.1 In `game-hunter.page.html`, add template refs to the collapsed time/HUD bar and the `.tag-fab` button, and render `<app-game-tour>` when the hunter tour is active.
- [ ] 4.2 In `game-hunter.page.ts`, query both `ElementRef`s and trigger the tour once: in-progress + surroundings warning dismissed + head-start delay overlay cleared + `!hasSeen('hunter')`.
- [ ] 4.3 Build the two hunter steps (time bar → `GAME_TOUR.TIME_BAR_*`, then tag button → `GAME_TOUR.TAG_*`); on `completed`, call `tourService.markSeen('hunter')` and hide the overlay.

## 5. i18n

- [ ] 5.1 Add a `GAME_TOUR` section to `src/assets/i18n/en.json` and `nl.json`: `TIME_BAR_TITLE`, `TIME_BAR_BODY` (tap to expand/collapse), `TAG_TITLE`, `TAG_BODY` (tag a nearby prey), `NEXT`, `DONE`, `SKIP`.

## 6. Tests & verification

- [ ] 6.1 Add a `GameTourComponent` spec: advances steps in order, emits `completed` on the last step and on skip, skips a step whose target is missing.
- [ ] 6.2 Extend/add prey page spec: tour shows on first in-progress entry, not shown when `hasSeen('prey')` is true, `markSeen('prey')` called on completion.
- [ ] 6.3 Extend/add hunter page spec: two-step tour shows on first entry (after delay cleared), not shown when `hasSeen('hunter')` is true, `markSeen('hunter')` called on completion; prey flag does not suppress the hunter tour.
- [ ] 6.4 Run `npx ng lint` and `npx ng test --watch=false --browsers=ChromeHeadless` for `src/ThePrey`; confirm no new failures.
- [ ] 6.5 Manual check: start a game as prey (tour: time bar only) and as hunter (tour: time bar then tag button); confirm neither reappears after completion and that the two flags are independent.
