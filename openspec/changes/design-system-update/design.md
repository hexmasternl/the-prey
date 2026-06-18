## Context

The Prey's front-end is an Ionic 8 / Angular standalone-component app in `src/ThePrey`. The design system was promoted to **v1.0** (`designs/The Prey Design System.html`) — a tactical night-vision language. Its tokens are already implemented in `src/ThePrey/src/theme/variables.scss` (`--tp-*` design tokens + `--ion-*` Ionic overrides, light + dark) and wired in `global.scss` after Ionic's `dark.system.css`.

A file-level audit (games, playfields, shell/auth) found the screens drift from the system in consistent ways:

- **Hardcoded colors** — pervasive `rgba(100,255,0,…)` / `rgba(255,47,31,…)` / surface hex across nearly every `.scss`, plus Tailwind-family colors (`#3b82f6`, `#22c55e`, `#ff9500`) in Leaflet `.ts`.
- **Shadow token systems** — `game-hunter.page.scss` and `game-prey.page.scss` redeclare the whole token set under bare names (`--signal`, `--hunter`…), so global token changes never reach the in-game views.
- **Rounded radii** — pills (`999px`), FABs (`50%`), modals (`8px`) instead of the 3px instrument radius; most playfields buttons fall back to the Ionic default radius.
- **Typography** — faux-bold on Special Elite; playfields headings/titles render in default fonts; inconsistent `letter-spacing` on uppercase labels.
- **Generic Ionic chrome** — playfields use floating labels, default lists, rounded searchbars, inline `style=`; settings uses raw `<input>`/`<button>`.
- **Role/semantic drift** — lobby hunter badge is amber instead of red.
- **Un-themed overlays** — surroundings-warning modal and `ion-select` overlays render default-light.
- **No reduced-motion** — every looping animation runs unconditionally.
- **Hardcoded copy** — "Signal lost. Find open sky." literals bypass i18n.

## Goals / Non-Goals

**Goals:**

- Make every screen conform to Design System v1.0 with **zero behavioral change**.
- Centralize the system so screens reference tokens/helpers instead of duplicating values — preventing future drift.
- Cover the three areas (playfields, games, shell/auth) plus the shared layer (`variables.scss`, `global.scss`).
- Make conformance checkable (token-only color, 3px radius, Special Elite headings, reduced-motion, localized copy).

**Non-Goals:**

- No new features, routes, or game logic; no redesign of the design system itself.
- No backend/API/data changes.
- Not introducing a new CSS framework, Tailwind, or a component library beyond Ionic.
- Not re-theming the rendered design-system HTML reference (it is documentation, not app code).

## Decisions

### D1 — Tokens are the single source of truth; remove shadow copies

All color/spacing flows from `--tp-*` / `--ion-*` in `variables.scss`. The per-page token redeclarations in `game-hunter`/`game-prey` are deleted and their `var(--…)` references repointed to the canonical `--tp-*` names.

*Why:* one place to change the palette; the in-game views currently silently ignore global edits. *Alternative considered:* keep local mixins but import from a shared partial — rejected; still two namespaces and a drift risk.

### D2 — Add a small derived-token + helper layer rather than scattering `color-mix()`

Add the handful of derived tokens the audit shows are reused: glow values (`--tp-signal-glow` already exists; add `--tp-hunter-glow`, `--tp-caution-glow`), a scrim (`--tp-scrim: rgba(0,0,0,0.78)`), and `*-rgb` channels where translucent fills are needed. Translucent one-offs use `color-mix(in srgb, var(--token) N%, transparent)`.

*Why:* keeps call sites declarative and consistent; avoids 50 bespoke `rgba()` literals. *Alternative:* SCSS functions/maps — rejected; the project styles components with CSS custom properties, and runtime theming (light/dark via media query) needs CSS variables, not compile-time SCSS.

### D3 — Theme Ionic-generated overlays via global `cssClass` hooks

Surroundings-warning modal, `ion-select` alerts, and confirmation dialogs are themed through globally-scoped classes in `global.scss` (e.g. `.tp-overlay`, `.tp-select-popover`) applied with `cssClass` / `[cssClass]`. Overlays are teleported to the app root, so page-scoped SCSS cannot reach them.

*Why:* Ionic overlays escape component style encapsulation; global classes are the supported mechanism. *Alternative:* per-page `::ng-deep` — rejected; fragile and non-portable across overlays.

### D4 — Shared reduced-motion handling

Add a single reduced-motion mixin/partial (`@media (prefers-reduced-motion: reduce)`) that pages include to freeze sweeps and neutralize pulses/flashes to a static halo/value. Where simpler, pages add a local guard, but the pattern is shared.

*Why:* accessibility requirement applies to every animated page; one pattern avoids per-page divergence.

### D5 — Leaflet colors come from shared TS constants mirroring the tokens

Leaflet cannot read CSS variables for geometry styling, so define a small `MAP_COLORS` constant set (`SIGNAL '#64ff00'`, `HUNTER '#ff2f1f'`, `CAUTION '#ffb300'`, `TAGGED` grey) referenced by `playfield-map`, `playfield-area`, `game-hunter`, `game-prey`. Replace Tailwind-family literals.

*Why:* keeps map colors aligned with tokens and consistent across editor/preview/in-game. *Alternative:* read computed CSS var via `getComputedStyle` — possible but heavier; a documented constant set is sufficient and matches dark-mode token values.

### D6 — Native Ionic controls over raw HTML

Settings' raw `<input>` → `ion-input`; raw `<button>` language grid → `ion-segment`/`ion-segment-button`. Playfields floating labels → `labelPlacement="stacked"` uppercase labels.

*Why:* the design system's input/control patterns assume Ionic theming hooks (`--background`, `--highlight-color-focused`, indicators) and accessibility semantics; raw elements lose both.

### D7 — Button hierarchy: one glowing solid primary per screen

Standardize: solid `--ion-color-primary` + glow for the single primary action; `fill="outline"` ghost for secondary; threat-red only for destructive. Login CTA becomes solid (was ghost). Home demotes secondary nav. A page-level `ion-button { --border-radius: 3px; }` rule enforces square buttons. Button labels remain Special Elite uppercase (per design system §06).

*Why:* the audit found multiple peers competing as primary and inconsistent radii; the system mandates exactly one primary.

### D8 — Scope: styling/templates/i18n only, area-by-area

Work proceeds shared-layer-first, then per area (playfields, games, shell/auth). Each page is a self-contained conformance edit; no shared component refactor beyond the token/helper layer and overlay classes.

## Risks / Trade-offs

- **Visual regressions in light mode** → Many hardcoded values only ever matched dark mode; switching to tokens may surface light-mode differences. Mitigation: verify each edited page in both `prefers-color-scheme` settings.
- **Removing `game-hunter`/`game-prey` shadow tokens breaks in-game styling if a name is missed** → these are the highest-risk files. Mitigation: do them one at a time, grep for every bare `var(--signal|--hunter|…)` reference, and smoke-test a live game view.
- **Overlay theming via global classes can leak** → scope classes tightly (`.tp-overlay …`) and test each overlay type. Trade-off: some global CSS is unavoidable for teleported overlays.
- **`color-mix()` browser support** → fine for the app's evergreen WebView/mobile targets; acceptable. Fallback tokens can be added if a target lacks support.
- **i18n key additions must land in every locale** → adding `GAME_PROGRESS.GPS_SIGNAL_LOST` to one file leaves others missing. Mitigation: update all locale files together and verify no missing-key warnings.
- **Scope creep into redesign** → strictly conformance-only; defer any "could look nicer" changes not mandated by the system.

## Migration Plan

1. **Shared layer first** — add derived tokens (`--tp-hunter-glow`, `--tp-caution-glow`, `--tp-scrim`, needed `*-rgb`) to `variables.scss`; add overlay classes, reduced-motion pattern, and `MAP_COLORS` constants. This unblocks all pages.
2. **Per area, page-by-page** — playfields → games → shell/auth, each verified in light + dark before moving on.
3. **i18n** — add new keys across all locale files in the same step as the code that uses them.
4. **Verify** — `dotnet`/Angular build, run the app, smoke-test a live game view and the overlays, check reduced-motion via OS setting.
5. **Rollback** — pure styling/template/i18n diffs with no data or API change; revert the change branch to fully roll back.

## Open Questions

- Should the derived tokens (glow/scrim) and `MAP_COLORS` be documented back into the design-system reference, or is `variables.scss` the canonical token home? (Assumption: `variables.scss` is canonical.)
- Is `#ff9500` (live prey-other blip on `game-prey`) intended as caution amber, or a deliberate distinct state? (Assumption: map to `--tp-caution`; confirm during implementation.)
