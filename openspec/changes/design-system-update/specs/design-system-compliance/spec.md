## ADDED Requirements

### Requirement: Color values use design tokens only

Every front-end screen in `src/ThePrey` SHALL express color through the design-system CSS custom properties (`--tp-*` design tokens or `--ion-*` Ionic tokens defined in `src/ThePrey/src/theme/variables.scss`). Components MUST NOT hardcode hex or `rgba()` color literals in SCSS, inline `style` attributes, or inline component templates. Where a translucent or blended color is needed, it MUST be derived from a token via `color-mix()` or a token-based `rgba(var(--token-rgb), a)` form, or from a dedicated derived token (e.g. `--tp-signal-glow`, `--tp-scrim`). A single page-local shadow copy of the global tokens (e.g. redeclaring `--signal`, `--hunter` under bare names) is prohibited; pages MUST reference the canonical `--tp-*` / `--ion-*` tokens.

#### Scenario: No hardcoded color literals remain

- **WHEN** any page `.scss`, `.html`, or inline-template/style `.ts` under `src/ThePrey/src/app` is searched for hex (`#rrggbb`) or `rgba(` literals applied to color, background, border, box-shadow, text-shadow, or fill
- **THEN** no such literal is found except as the right-hand value of a derived token definition inside `variables.scss`

#### Scenario: In-game views drop their shadow token blocks

- **WHEN** `game-hunter.page.scss` and `game-prey.page.scss` are inspected
- **THEN** the local `@mixin tactical-theme` / `:host` blocks that redeclare `--signal`, `--hunter`, `--caution`, `--bg-*`, `--text*`, `--line` are removed and all `var(--…)` references resolve to the global `--tp-*` tokens

#### Scenario: Map/Leaflet colors match the signal palette

- **WHEN** Leaflet polygon, marker, and blip colors are set in `playfield-map.component.ts`, `playfield-area.page.ts`, `game-hunter.page.ts`, and `game-prey.page.ts`
- **THEN** they use the design-system values (signal green `#64FF00`, threat red `#FF2F1F`, caution amber `#FFB300`, tagged grey) referenced through shared constants — never ad-hoc Tailwind-family colors such as `#3b82f6`, `#22c55e`, or `#ff9500`

### Requirement: Typography follows the two-typeface system

Screens SHALL use `var(--tp-head)` (Special Elite) for headings and numeric readouts and `var(--tp-body)` (PT Mono) for running text and data. Buttons labels SHALL be Special Elite, uppercase. UPPERCASE labels (input labels, section labels, status tags) SHALL use PT Mono with `letter-spacing: 2px` and `text-transform: uppercase`. Special Elite has no bold weight: elements rendered in Special Elite MUST NOT set `font-weight: 700`/`bold` (emphasis is achieved through size and color). Font families MUST be referenced through the `--tp-head` / `--tp-body` tokens, not hardcoded font-stack strings.

#### Scenario: Headings and readouts use Special Elite

- **WHEN** a page renders a heading (`ion-title`, `h1`/`h2`, page/section titles) or a numeric readout (HUD values, timers, distances, stat counts, language codes)
- **THEN** the element's font-family resolves to `var(--tp-head)`

#### Scenario: No faux-bold on Special Elite

- **WHEN** any element using `var(--tp-head)` is inspected (including HUD countdowns, spectator titles, delay-overlay countdown/warning titles)
- **THEN** it does not declare `font-weight: 700` or `bold`

#### Scenario: Uppercase labels carry correct spacing

- **WHEN** an input label, section label, or status/role tag is rendered
- **THEN** it is uppercase PT Mono (`var(--tp-body)`) with `letter-spacing: 2px`

### Requirement: Spacing and radius follow the instrument grid

Screens SHALL use a 4-point spacing scale and a near-square corner radius of `3px` for buttons, cards, inputs, modals, and surfaces ("instrument, not rounded consumer toy"). Rounded/pill/circular radii (e.g. `border-radius: 8px`, `999px`, `50%`) on these elements are prohibited; FABs and status pills MUST adopt the 3px square treatment. Buttons across every page MUST resolve to `--border-radius: 3px` rather than the Ionic default.

#### Scenario: Buttons are square

- **WHEN** any `ion-button` (primary, ghost, FAB, footer, segment) is rendered on any screen
- **THEN** its computed border-radius is `3px`

#### Scenario: No rounded surfaces remain

- **WHEN** status pills, FABs, modal hosts, cards, and overlay containers are inspected
- **THEN** none uses `border-radius` of `8px`, `999px`, or `50%`; all use `3px`

### Requirement: Components match the design-system patterns

Screens SHALL render the documented component patterns: exactly one solid signal-green primary action per screen (with ambient glow), secondary actions as outlined ghost buttons, inputs as dark insets with a stacked UPPERCASE label above and a hairline border that lights signal-green on focus, list/roster rows carrying a colored role/identity bar, alerts as left-bordered banners color-coded by urgency, and lobby/settings controls as segmented controls, toggles, and sliders with a green-lit active state. Default/generic Ionic chrome (floating labels, rounded searchbars, raw `<input>`/`<button>` elements where an Ionic equivalent exists, unstyled lists) is non-compliant.

#### Scenario: One glowing primary per screen

- **WHEN** a screen with a primary call-to-action is rendered (e.g. login, playfield save, selection confirm, game start)
- **THEN** that action is a single solid `--ion-color-primary` button with a persistent `box-shadow` glow, and no other button on the screen uses the solid primary fill

#### Scenario: Inputs use stacked uppercase labels with signal focus

- **WHEN** a text input is rendered on `playfield-create` or `playfield-detail`
- **THEN** it uses `labelPlacement="stacked"` with an UPPERCASE PT Mono label and a focus highlight color of `var(--tp-signal)`, not a floating label

#### Scenario: Settings uses native Ionic controls

- **WHEN** the settings page renders its text field and language selector
- **THEN** the text field is an `ion-input` (not a raw `<input>`) and the language selector is an `ion-segment`/`ion-segment-button` pair with a `var(--tp-signal)` active indicator (not raw `<button>` elements)

### Requirement: Role color semantics are consistent

Screens SHALL encode player roles and threat consistently: prey/safe/confirm states in signal green (`--tp-prey` / `--tp-signal`), hunter/destructive/threat states in threat red (`--tp-hunter` / `--ion-color-danger`), and warnings/penalties in caution amber (`--tp-caution`). The hunter role identifier MUST be threat red, not caution amber. Destructive/irreversible actions (tag, eliminate) MUST be threat red and only those.

#### Scenario: Hunter badge is threat red

- **WHEN** the lobby renders the hunter role badge
- **THEN** its color and border derive from `--tp-hunter` (threat red), not `--tp-caution` (amber)

#### Scenario: Destructive actions are red

- **WHEN** a tag/eliminate or other irreversible action button is rendered
- **THEN** it uses the threat-red treatment, and no non-destructive action on the screen uses threat red

### Requirement: Overlays adopt the tactical theme

Modals, dialogs, and Ionic-generated overlays (`ion-select` alert/popover, surroundings-warning modal) SHALL render with the tactical aesthetic: dark token surface, `var(--tp-head)` title, `var(--tp-body)` body, `3px` radius, corner brackets where specified, and a ~78% void scrim (`--tp-scrim`). Confirmation dialogs SHALL place the safe action on the left and the committed/filled action on the right, with destructive confirmations in threat red.

#### Scenario: Surroundings-warning modal is themed

- **WHEN** the surroundings-warning modal opens on the hunter or prey page
- **THEN** it renders on a dark token surface with Special Elite title, PT Mono body, and 3px radius — not the default light Ionic card

#### Scenario: Select overlays are themed

- **WHEN** an `ion-select` overlay opens on `game-create` or `game-lobby`
- **THEN** it renders with the tactical dark theme via a supplied `cssClass`, not default Ionic styling

### Requirement: Motion honors reduced-motion

Every continuous or attention animation (radar sweep, scanlines, deploy/ready pulses, pill pulse, tag pulse, flash cycles) SHALL be disabled or reduced to a static state inside a `@media (prefers-reduced-motion: reduce)` block, so the app is usable and non-distracting when the OS requests reduced motion.

#### Scenario: Animations freeze under reduced-motion

- **WHEN** the OS reports `prefers-reduced-motion: reduce`
- **THEN** every page that defines an infinite/looping animation provides a matching reduced-motion rule that halts the sweep and replaces pulses/flashes with a static halo or value

### Requirement: User-facing copy is tactical and localized

User-facing strings SHALL use the tactical voice (clipped, present-tense, second-person, imperative; the player is an "operative") and MUST be sourced from the i18n translation system rather than hardcoded in TypeScript or templates. Existing hardcoded strings such as "Signal lost. Find open sky." MUST be replaced with translation keys.

#### Scenario: GPS-lost copy is localized

- **WHEN** the GPS signal is lost on the hunter or prey page
- **THEN** the displayed message is resolved from a translation key (e.g. `GAME_PROGRESS.GPS_SIGNAL_LOST`) present in every locale file, not a hardcoded English literal

### Requirement: Playfields screens conform to the tactical system

The playfields screens (`playfields-list`, `playfield-create`, `playfield-detail`, `playfield-selection`, `playfield-area`, `playfield-map`) SHALL be brought from default Ionic appearance into full design-system conformance: Special Elite headings, stacked UPPERCASE inputs with signal focus, 3px token-styled buttons with primary glow, token-driven toolbar/footer/list surfaces, signal-green Leaflet geometry, a dim-inset (`--tp-signal-deep`) selection highlight with a signal-green left bar, and no inline `style` attributes.

#### Scenario: Playfields pages no longer read as default Ionic

- **WHEN** any playfields page is rendered
- **THEN** headings use Special Elite, buttons are 3px with the primary action glowing, inputs use stacked uppercase labels, surfaces use `--tp-*`/`--ion-*` tokens, and no `style="..."` attribute remains in the template

#### Scenario: Selection highlight is a dim inset

- **WHEN** a row is selected on `playfield-selection`
- **THEN** its background is `var(--tp-signal-deep)` with a `var(--tp-signal)` left border, not a full bright `--ion-color-primary-tint` fill

### Requirement: Games screens conform to the tactical system

The games screens (`game-join`, `game-create`, `game-lobby`, `game-hunter`, `game-prey`, `game-outcome`, `hunter-delay-overlay`) SHALL conform: token-only colors (including the shared tactical-background grid/radar/scanline effects), global `--tp-*` tokens instead of per-page shadow copies, 3px radii on pills/FABs/modals, threat-red hunter semantics, themed overlays, localized copy, primary-action glow, and reduced-motion guards.

#### Scenario: Games area uses shared token-based effects

- **WHEN** the tactical background (grid, radar, scanline) and pulse effects render on `game-join`, `game-create`, and `game-lobby`
- **THEN** they are driven by tokens (no duplicated hardcoded `rgba(100,255,0,…)` literals) and a `prefers-reduced-motion` rule freezes them

#### Scenario: HUD readouts are consistent across roles

- **WHEN** HUD numeric values render on `game-hunter` and `game-prey`
- **THEN** both use `var(--tp-head)` with signal-green treatment, with no faux-bold

### Requirement: Shell and auth screens conform to the tactical system

The shell and auth screens (`login`, `home`, `settings`, `app.component`) SHALL conform: the login CTA is a solid signal-green primary with glow, token references carry no redundant or wrong-shade hardcoded fallbacks (e.g. `app.component` boot screen uses `var(--tp-bg-void)` not `#0a0c0a`), settings uses native Ionic controls, and the home screen presents a single primary action with secondary navigation demoted to a lower visual weight.

#### Scenario: Login presents a solid primary CTA

- **WHEN** the login page renders its single action button
- **THEN** it is a solid `var(--tp-signal)` fill with black contrast text and a glow, not an outlined ghost button

#### Scenario: Token fallbacks are removed or corrected

- **WHEN** `app.component.scss`, `login.page.scss`, and `home.page.scss` reference design tokens
- **THEN** they use the canonical token with no hardcoded hex/rgba fallback, and the boot-screen background resolves to the correct `--tp-bg-void` value
