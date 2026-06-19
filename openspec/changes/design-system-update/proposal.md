## Why

The design system was upgraded to a mature **v1.0** (`designs/The Prey Design System.html`) — a complete tactical "night-vision" visual language (phosphor green on dark grey, Special Elite + PT Mono, 3px instrument radius, role-coded components). The canonical tokens already live in `src/ThePrey/src/theme/variables.scss`, but most existing screens were built before this and drift from it: they hardcode colors, fall back to default Ionic chrome, maintain shadow copies of the tokens, use rounded radii, faux-bold Special Elite, generic copy, and ignore reduced-motion. The result is a visually inconsistent app that does not read as one instrument.

## What Changes

- Establish **design-system compliance** as an explicit, testable capability that every Ionic page in `src/ThePrey` must satisfy (tokens-only color/spacing, correct typefaces, 3px radius, role colors, component patterns, accessibility, tactical voice).
- **Playfields area** — bring `playfields-list`, `playfield-create`, `playfield-detail`, `playfield-selection`, `playfield-area`, and `playfield-map` from near-default Ionic look into the tactical system (Special Elite headings, stacked UPPERCASE inputs, 3px buttons, token surfaces, signal-green Leaflet colors, dim-inset selection).
- **Games area** — remove hardcoded `rgba()` colors, replace the per-page shadow token blocks in `game-hunter`/`game-prey` with the global `--tp-*` tokens, fix circular/pill/8px radii to 3px, correct the lobby hunter badge to threat red, theme the surroundings-warning modal and `ion-select` overlays, move "Signal lost…" strings into i18n, and add `prefers-reduced-motion` guards.
- **Gameplay-view structural parity (prey + hunter)** — beyond token cleanup, the prey gameplay view is structurally non-conformant with §16 (it lacks the status bar, corner brackets, role tag, status pill, and map wash that the hunter view already has). Bring the prey view to structural parity with the hunter view, give both the documented three-state **threat escalation** (normal green → final amber → critical red, with frame-color shift and timer flash) driven by the game-timer phase, and render HUD numeric readouts in Special Elite. *(See Impact for the data dependency that bounds this.)*
- **Shell & auth area** — make the login CTA a solid signal-green primary, replace hardcoded `rgba()` fallbacks with tokens, fix `app.component` boot-screen wrong-shade fallback, and convert the settings page's raw `<input>`/`<button>` controls to Ionic `ion-input` / `ion-segment`.
- Add a small set of **shared tactical primitives** (token-based glow/scrim helpers, themed-overlay CSS classes, reduced-motion mixin) so pages stop duplicating values.
- **No functional/behavioral change** to any feature — this is a visual-conformance pass only.

## Capabilities

### New Capabilities
- `design-system-compliance`: The normative rules every front-end screen must follow to conform to The Prey Design System v1.0 — color tokens, typography, spacing/radius, component patterns, role-color semantics, themed overlays, motion/accessibility, and tactical copy — plus per-area conformance requirements for playfields, games, and shell/auth screens.

### Modified Capabilities
<!-- None. Existing page specs describe functional behavior; this change alters only visual conformance, which is captured by the new capability above. -->

## Impact

- **Code (styling/templates only):** `src/ThePrey/src/app/**` page `.html`, `.scss`, and inline-template/style `.ts` files across playfields, games, settings, home, login; `app.component`; shared `src/ThePrey/src/global.scss` and `src/ThePrey/src/theme/variables.scss` (new derived tokens/helpers).
- **i18n:** new translation keys for currently-hardcoded strings (e.g. `GAME_PROGRESS.GPS_SIGNAL_LOST`).
- **Reference:** `designs/The Prey Design System.html` (v1.0) is the source of truth; `variables.scss` is its token implementation.
- **No API, data, or backend impact.** No change to component behavior, routing, or game logic.
- **Data dependency (bounds the prey rework):** The §16 prey "Hunter distance" HUD readout and any prey threat escalation driven by hunter *proximity* require hunter-location data the prey client does not currently receive. These are **out of scope** for this visual-compliance change and are deferred to a separate, backend-touching change. The prey structural rework here covers only what is achievable with data the client already has (status bar, role tag, status pill, map wash, Special Elite HUD, and timer-phase threat escalation).
