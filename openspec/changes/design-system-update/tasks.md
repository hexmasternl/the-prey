## 1. Shared token & helper layer

- [ ] 1.1 Add derived tokens to `src/ThePrey/src/theme/variables.scss`: `--tp-hunter-glow`, `--tp-caution-glow`, `--tp-scrim` (`rgba(0,0,0,0.78)`), and any `--tp-*-rgb` channels needed for translucent fills (light + dark)
- [ ] 1.2 Add a shared reduced-motion pattern (SCSS mixin/partial) that freezes sweeps and neutralizes pulses/flashes under `@media (prefers-reduced-motion: reduce)`
- [ ] 1.3 Add globally-scoped overlay theme classes in `src/ThePrey/src/global.scss` (e.g. `.tp-overlay`, `.tp-select-popover`): dark token surface, `--tp-head` title, `--tp-body` body, 3px radius, `--tp-scrim` backdrop, corner brackets where specified
- [ ] 1.4 Add a `MAP_COLORS` constant set (signal `#64ff00`, hunter `#ff2f1f`, caution `#ffb300`, tagged grey) for Leaflet usage, in a shared TS location reachable by playfields and games map code
- [ ] 1.5 Add i18n key `GAME_PROGRESS.GPS_SIGNAL_LOST` (and any other newly-extracted strings) to every locale file
- [ ] 1.6 Add a page-level convention/snippet for `ion-button { --border-radius: 3px; }` to apply 3px buttons consistently

## 2. Playfields area

- [ ] 2.1 `playfield-map.component.ts`: move inline container `style` to a class; replace Leaflet `#3b82f6` polygon with `MAP_COLORS.SIGNAL`
- [ ] 2.2 `playfield-area.page.ts`: token-style toolbar/footer; replace `rgba(0,0,0,0.4)` scrim with `--tp-scrim`; replace Tailwind marker/polygon colors (`#22c55e`, `#ef4444`, `#ffffff`) with `MAP_COLORS`; set Reset/Cancel as ghost outline + 3px and Save as solid primary with glow
- [ ] 2.3 `playfields-list.page`: remove inline spinner styles; Special Elite on `h2`/labels; signal-green role bar on rows; theme `ion-searchbar`/`ion-segment` (3px, token surfaces); FAB glow + 3px; uppercase labels at `letter-spacing: 2px`
- [ ] 2.4 `playfield-create.page`: convert inputs to `labelPlacement="stacked"` uppercase PT Mono labels with `--highlight-color-focused: var(--tp-signal)`; remove inline `style` on notes/buttons; primary Save = solid + glow + 3px + Special Elite; help/area buttons = ghost outline + 3px; toggle green-lit; section-label `letter-spacing: 2px`
- [ ] 2.5 `playfield-detail.page`: port all create-page input/button/toggle/searchbar fixes; standardize spinner to `name="lines"` color primary; remove inline styles; primary Save glow + 3px
- [ ] 2.6 `playfield-selection.page`: change selection highlight to `--tp-signal-deep` background + `var(--tp-signal)` left border; Special Elite `h2`; token toolbar/footer surfaces; confirm button = solid primary + glow + 3px; cancel = ghost outline; uppercase empty-label at `letter-spacing: 2px`
- [ ] 2.7 Verify playfields pages in light + dark mode: no inline `style`, no hardcoded colors, all buttons 3px, headings Special Elite

## 3. Games area

- [ ] 3.1 `game-join.page.scss`: replace all hardcoded `rgba(...)` (grid/radar/scanline/code-input/placeholder/keyframes) with token-derived values; add reduced-motion guard; change `color="medium"` back button to ghost outline
- [ ] 3.2 `game-create.page.scss`/`.html`: token-ize tac-bg + config borders + keyframes; reduced-motion guard; `.op-title` → `--tp-signal`; apply `.tp-select-popover` to `ion-select` overlays
- [ ] 3.3 `game-lobby.page`: token-ize tac-bg + badges + keyframes; **fix hunter badge to `--tp-hunter` (red)**; reduced-motion guard on ready/start pulse; apply themed `ion-select` overlay class
- [ ] 3.4 `game-hunter.page.scss`: remove the local `@mixin tactical-theme`/`:host` shadow tokens and repoint every `var(--…)` to `--tp-*`; replace hardcoded fonts with `--tp-head`/`--tp-body`; fix radii (status-pill `999px`→3px, FABs `50%`→3px, tag-modal `8px`→3px); token-ize all `rgba()` surfaces/glows; reduced-motion guard
- [ ] 3.5 `game-hunter.page.ts`: replace hardcoded `#ff2f1f`/`#888888` Leaflet styles with `MAP_COLORS`; replace "Signal lost. Find open sky." literals with `GAME_PROGRESS.GPS_SIGNAL_LOST`
- [ ] 3.6 `game-prey.page.scss`: remove the `:host` shadow token block and repoint to `--tp-*`; remove `font-weight: 700` faux-bold (spectator title); HUD numeric values → `--tp-head` + signal treatment; token-ize all `rgba()` surfaces/glows/fonts
- [ ] 3.7 `game-prey.page.ts`: replace hardcoded Leaflet colors with `MAP_COLORS` (resolve `#ff9500` intent → caution amber); replace "Signal lost…" literals with the i18n key
- [ ] 3.8 `game-outcome.page`: token-ize `rgba()` defeat glow + stat background; `letter-spacing: 3px` on outcome button; add persistent primary glow
- [ ] 3.9 `hunter-delay-overlay.component.ts`: add `font-family` (`--tp-head` for countdown, `--tp-body` for hints/body); remove `font-weight: 700`; token-ize all colors; card radius `8px`→3px; ensure component can resolve `--tp-*` tokens
- [ ] 3.10 Theme the surroundings-warning modal (hunter + prey) with the `.tp-overlay` class (dark surface, Special Elite title, PT Mono body, 3px)
- [ ] 3.11 Verify games pages in light + dark and in a live game view: tokens resolve, radii 3px, reduced-motion freezes animations, overlays themed

## 4. Shell & auth area

- [ ] 4.1 `login.page`: make the CTA a solid `var(--tp-signal)` primary with black contrast text + glow (was ghost); remove redundant hardcoded `rgba` fallbacks; keep Special Elite uppercase label
- [ ] 4.2 `home.page`: token-ize gradient/denied `rgba()` values; demote secondary nav (Playfields/Settings/Logout/Quit) below the single primary "PLAY NOW"; keep one glowing primary
- [ ] 4.3 `settings.page`: replace raw `<input>` with `ion-input` (dark inset, signal focus); replace raw `<button>` language grid with `ion-segment`/`ion-segment-button` (green-lit active); fix `transition: all`; add `ion-title` Special Elite/uppercase; `section-label` → PT Mono `letter-spacing: 2px`
- [ ] 4.4 `app.component.scss`: remove wrong-shade fallback so boot screen uses `var(--tp-bg-void)` and spinner uses `var(--tp-signal)`; add tactical `aria-label` to boot screen
- [ ] 4.5 Verify shell/auth pages in light + dark: single primary per screen, native controls, no hardcoded fallbacks

## 5. Final verification

- [ ] 5.1 Grep `src/ThePrey/src/app` for residual hex/`rgba(` color literals and inline `style=` attributes; confirm none remain outside derived-token definitions
- [ ] 5.2 Confirm no element using `--tp-head` declares `font-weight: 700`/`bold`
- [ ] 5.3 Confirm every page with a looping animation has a `prefers-reduced-motion` rule
- [ ] 5.4 Run the Angular build and smoke-test the app (lobby, live hunter/prey view, overlays) in both color schemes; confirm no missing-i18n-key warnings
