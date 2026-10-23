---
name: the-prey-design-system
description: The Prey's visual design system (v2.0) and tactical UI style manual. Use whenever building, styling, or reviewing any UI for The Prey (app, emails, website) — pages, components, overlays, HUD, radar, map markers, logo usage, colors, fonts, spacing, motion, copy tone — so the result matches the phosphor-green tactical aesthetic. The full visual reference is designs/the-prey-design-system.html.
metadata:
  author: the-prey
  version: "3.0"
---

# The Prey — Design System (v2.0)

The Prey is a GPS-based, real-world hide-and-seek game (hunters vs. prey). The UI is a **surveillance instrument for a manhunt**: night-vision optics, GPS trackers, field-radio comms — phosphor green burning against dead, green-tinted grey. It must be readable *at a sprint, outdoors, in sunlight*.

## The visual reference

The authoritative style manual is **`designs/the-prey-design-system.html`** (v2.0, Oct 2026). It is plain, readable HTML/CSS (~1000 lines), so you *can* `Read` or `Grep` it — the `:root` block at the top holds every token, and each section has an `id` (`#logo`, `#color`, `#type`, `#space`, `#motion`, `#icons`, `#a11y`, `#buttons`, `#status`, `#inputs`, `#controls`, `#feedback`, `#overlays`, `#lists`, `#hud`, `#thresholds`, `#radar`, `#screens`, `#tokens`). Grep for the section you need rather than reading the whole file. To see it rendered: `! start designs/the-prey-design-system.html`, or screenshot it via the `playwright-cli` skill. A print version is `designs/the-prey-design-system.pdf`.

Assets:
- `designs/logo/` — **current logo** (v2): `the-prey-app-icon.svg` (+ `-512.png`, `-1024.png`), `the-prey-mark.svg`, `the-prey-mark-mono.svg`, `the-prey-mark-1024.png`, `the-prey-lockup.svg/.png`.
- `designs/SpecialElite-Regular.ttf`, `designs/PTMono-Regular.ttf` — the two typefaces.
- `designs/screens/*.png` and `designs/Additional/` — **v1-era** mockups and the old logo. Useful for screen flow/content, but the v2 HTML wins on look (old logo, scanlines, heavier glow are superseded).

## What changed in v2.0

- **Same palette and fonts** — every hex value and both typefaces carry over.
- **Tokens first** — named primitive + semantic tokens, a 4pt spacing scale, a radius scale (4–20px), three elevation levels.
- **Calmer and more legible** — no scanline overlay; glow only for live and pressed/hover states; 48px touch targets; labels use Soft (not Ghost) so they pass contrast.
- **New logo** — a fox in the crosshairs (see *Logo*).

## Principles

1. **Glanceable over beautiful** — your role, the nearest threat and time left must register in under a second. Hierarchy beats decoration.
2. **Green is information** — Signal green is the most precious pixel. Keep it for live, actionable data. If everything glows, nothing does.
3. **Two sides, one system** — Prey and Hunter share one layout; role shows only through a single accent (green or red).
4. **The world is the canvas** — map and radar come first; chrome stays thin, translucent and recedes.

## Voice & tone

Short, imperative, radio-operator calm. Say what is happening, then what to do. **No exclamation marks, emoji or apologies.** No raw error codes.
- Do: "You are hidden. Keep moving." · "Hunter nearby. Break line of sight." · "Signal lost. Find open sky." · "Hunt begins in 00:10."
- Don't: "Oops! We couldn't find you 😅" · "You might want to consider moving." · "Error 403: location unavailable." · "Welcome back, valued player!"

Button labels say what happens ("Start hunt", "Leave game"), never "OK". Errors say what to do next ("Codes are 6 characters. Check with your host.").

## Tokens, not guesswork

**Never hard-code a color, font, or size in a component** — define tokens once in the client's central theme and reference them everywhere. Components use *semantic* tokens; primitives exist only to define them.

In the Android app (Kotlin + Jetpack Compose, `app/src`) the theme lives in `app/src/app/src/main/java/nl/hexmaster/theprey/ui/theme/`:
- `Color.kt` — the primitives, `internal` so screens can't use them directly.
- `Theme.kt` — `ThePreyTheme` maps them onto the Material 3 color scheme (see *Semantic mapping*) and exposes the game-specific ones (`signal`, `hunter`, `caution`, `prey`, `info`, glow…) as `ThePreyTheme.colors`.
- `Type.kt` — Special Elite on display/headline/title styles, PT Mono on body/label styles.

Screens use `MaterialTheme.colorScheme.*`, `MaterialTheme.typography.*` or `ThePreyTheme.colors.*`. The XML `res/values/colors.xml` exists only for the window background, splash screen and launcher icon.

### Color — primitives → semantic

| Primitive | Hex | Semantic | Use |
|---|---|---|---|
| `--tp-void` | `#0C0E0C` | `bg-inset`, `on-accent` | Map base, inputs, insets; label on Signal/Hunter fills |
| `--tp-base` | `#181B17` | `bg` | App background |
| `--tp-surface` | `#23271F` | `bg-card` | Cards, sheets, panels |
| `--tp-surface-2` | `#2D3328` | `bg-raised` | Raised rows, hover, selected, disabled button fill |
| `--tp-line` | `#39402F` | `border` | Borders, dividers |
| — | `rgba(57,64,47,.55)` | `border-subtle` | Card outlines, row separators |
| `--tp-signal` | `#64FF00` | `accent` | Primary action, live data, Prey role, friendly markers |
| `--tp-signal-dim` | `#3F9E00` | `accent-muted` | Eyebrows, secondary-button borders |
| `--tp-signal-deep` | `#1F4D00` | — | Radar grid, inactive tracks. **Decorative only** |
| `--tp-hunter` | `#FF2F1F` | `danger` | Hunters, tagged, destructive actions |
| `--tp-caution` | `#FFB300` | `warning` | Low time, contested/shrinking zones, approaching threat |
| `--tp-info` | `#13E0C8` | `info` | Waypoints, neutral system messages |
| `--tp-text` | `#DCF6D2` | `fg` | Headings, values, body |
| `--tp-text-soft` | `#8C9A83` | `fg-muted` | Descriptions, **labels**, captions, units |
| `--tp-text-ghost` | `#5A6553` | `fg-disabled` | Disabled and placeholder **only** — never informational |

Tinted fills: `accent-a10` `rgba(100,255,0,.10)`, `accent-a20` `rgba(100,255,0,.20)`, `danger-a12` / `warning-a12` / `info-a12` at 12% alpha of their color. Tinted borders use the color at ~35–40% alpha.

App-only extras (in `Color.kt`, not in v2): `LineSoft #2A2F24` (≈ `border-subtle` flattened) and `HunterDim #A01408` (error container). Prefer the v2 equivalents for new work.

**Usage ratio:** ~60% base · 20% surface · 12% text · 5% signal · ≤3% threat + caution. If a screen reads as "green", there's too much accent.

### Semantic mapping (Material 3)

- **primary** = Signal `#64FF00`, onPrimary = Void · **primaryContainer** = Signal Deep
- **secondary** = Signal Dim `#3F9E00` · **tertiary** = Info `#13E0C8`
- **error** = Hunter `#FF2F1F`, onError = Void · warning = Caution `#FFB300`
- **background** = Base · **surface** = Surface · **surfaceVariant** = Surface 2 · **surfaceContainerLowest** = Void
- **outline** = Line · **onSurfaceVariant** = Text Soft

Dark is the only identity for now; no light palette is defined in v2.

## Typography

| Font | Token | Role |
|---|---|---|
| **Special Elite** | `--font-display: 'Special Elite','Courier New',monospace` | Headings, titles and **all big numeric readouts** (timers, distances, counts, join codes) |
| **PT Mono** | `--font-body: 'PT Mono','Courier New',monospace` | Everything else — body, labels, buttons, data. Fixed width keeps live numbers from jittering |

Type scale (size / line-height):

| Style | Spec | Example |
|---|---|---|
| `display` | 56 / 1.0 · SE | STAY HIDDEN |
| `title-1` | 32 / 1.1 · SE | Mission briefing |
| `title-2` | 24 / 1.2 · SE | Nearest threat |
| `title-3` | 19 / 1.3 · SE | Active players |
| `readout` | 44 / 1.0 · SE | 14:12 |
| `body` | 15 / 1.6 · PT | You are hidden. Keep moving. |
| `body-sm` | 13 / 1.5 · PT, Soft | Last ping 3 seconds ago |
| `label` | 11 / 1.0 · PT · +1.5px tracking, UPPERCASE, Soft | DISTANCE TO EDGE |

Special Elite has one weight — emphasise with size and colour, **never faux-bold**. Keep body lines under ~68 characters.

## Space, shape, depth

- **Spacing (4pt grid):** `s-1` 4 · `s-2` 8 · `s-3` 12 · `s-4` 16 · `s-5` 20 · `s-6` 24 · `s-8` 32 · `s-10` 40 · `s-12` 48 · `s-16` 64.
- **Radius:** `r-xs` 4 (chips, code) · `r-sm` 8 (buttons, inputs) · `r-md` 12 (cards, tiles, alerts) · `r-lg` 20 (sheets, dialogs) · `r-pill` 999 (pills, switches).
- **Layout:** screen gutter 16 · card padding 16–24 · section gap 32 · min touch target **48×48** · respect the bottom safe area on every screen.
- **Elevation** — depth comes from a step up in surface colour plus a soft shadow, not heavier borders:
  - `e-0` inset — Void
  - `e-1` cards — Surface · `0 1px 0 rgba(255,255,255,.03) inset, 0 1px 2px rgba(0,0,0,.4)`
  - `e-2` toasts, HUD — Surface 2 · `… 0 8px 24px rgba(0,0,0,.45)`
  - `e-3` sheets, dialogs — `… 0 24px 64px rgba(0,0,0,.6)`
- **Glow:** `--glow` `0 0 0 1px rgba(100,255,0,.35), 0 0 24px rgba(100,255,0,.25)`; `--glow-danger` same in red. Only on live elements and hover/pressed primary/danger buttons.

## Motion

Motion is telemetry: it loops **only when something is live**, otherwise it is short and decisive.
- Durations: `d-fast` 120ms (press) · `d-base` 200ms (state change) · `d-slow` 320ms (sheets).
- Easing: `ease-out` `cubic-bezier(.2,.8,.2,1)` (enter) · `ease-in-out` `cubic-bezier(.65,0,.35,1)` (loops) · `linear` (radar only).
- Signature loops: radar sweep 3.4s linear · blip ping 2s ease-out · live pulse 1.6s in-out · threat flash 1s in-out.
- Press feedback: scale to .97.
- Every loop stops under reduced-motion; state must then still be shown through colour/label.

## Iconography

24px grid, 2px strokes, round caps, no fills except focal dots. Icons take the colour of their text. Pair with a label wherever space allows. Core set: target, pin, users, clock, signal, eye, alert, info, check, settings, arrow, x.

## Accessibility

Contrast (WCAG 2.2) — on Base / on Surface:
- Primary text 15.0 / 13.1, Signal 13.1 / 11.5, Info 10.4 / 9.1, Caution 9.7 / 8.5 — all text.
- Soft 5.8 / 5.1 — body, labels, captions.
- Signal Dim 5.1 / 4.4 — eyebrows ≥11px on Base, borders.
- Hunter 4.7 / 4.1 — text ≥14px, icons, fills.
- Ghost 2.8 / 2.5 — **disabled/placeholder only**.
- Void on Signal fill 14.6 (primary button) · Void on Hunter fill 5.2 (danger button). **Never put primary text on red** (3.2).

Rules: touch targets ≥48×48 (TAG action 72); **never use colour alone** — role and threat state always carry a label or icon too; pair critical alerts with haptics; respect system reduced-motion and text-size settings.

## Logo

A **fox in the crosshairs**: a broken reticle (hunter's view, Signal green) around a faceted fox mask (prey, Hunter red) with a green lock dot between its eyes. Flat geometry, sharp at 16px.
- 512 grid; reticle radius 168, stroke 18, round caps, 28° gaps at each cardinal with inward ticks. The fox's right half carries a 20% Void overlay for the facet.
- Clear space on all sides = one reticle tick (1/8 of the mark width). Min size: mark 24px, app icon 16px.
- Only on Void, Base or Surface. On a light background use the app-icon tile (Base `#181B17`, rx 112, soft green radial glow).
- One-colour version: Signal on dark, or Void on Signal. **Never red alone.**
- Don't put the full-colour mark on light backgrounds, stretch/skew/crop it, recolour it, or add glows/effects.
- Lockup: mark + "THE **PREY**" (Special Elite, "PREY" in Signal) + tagline "STAY HIDDEN · HUNT SMART" (PT Mono, Soft, wide tracking).

## Components

- **Buttons** — one primary action per screen; UPPERCASE PT Mono, 14px, +1.2px tracking, `r-sm`. Heights: sm 36 · default 48 · lg 56; icon button is square.
  - *Primary*: Signal fill, Void label, glow on hover/focus. *Secondary*: transparent, Signal text, Signal-Dim border → `accent-a10` fill on hover. *Ghost*: transparent, `fg` text → `bg-raised` on hover. *Danger*: Hunter fill, Void label, danger glow on hover. *Disabled*: `bg-raised` fill, Ghost text, no glow. Loading: inline spinner + verb ("Joining").
  - **TAG action (Hunter only)**: 72px round FAB in Hunter red, bottom-right above the HUD; glows only when a prey is in range, otherwise disabled (`bg-raised`) with a cooldown ring and countdown.
- **Pills** (status) — 28px, `r-pill`, UPPERCASE 11.5px, leading 7px dot in currentColor, tinted fill + tinted border. Variants: signal (Live, Hidden), danger (Hunter near, Tagged), warn (Final stage), info (Waypoint), neutral (Offline: Soft text, Line border, no fill). `live` pills pulse their dot.
- **Role tag** — card with a **3px left accent bar** in the role colour (the one place role colour shows in a layout), avatar + "YOUR ROLE" label + role name in Special Elite.
- **Avatar** — 36px circle, `bg-raised`, 2px role-coloured ring, initial in Special Elite in the role colour.
- **Inputs** — 48px, on Void (one level deeper than the page), Line border, `r-sm`. UPPERCASE label above, Soft help text below. Focus: Signal border + 3px `accent-a20` ring. Error: Hunter border + `danger-a12` ring + actionable help text in Hunter. Placeholder in Ghost.
- **Join code** — segmented boxes (48×56), Special Elite 26px in Signal, auto-advance; active box gets the focus ring.
- **Switch** 48×28 (on: `accent-a20` track, Signal border + knob) · **Segmented** control on Void, selected segment `bg-raised` with Signal text (Hunter red when the role is hunter) · **Checkbox** 22px, `r` 6, checked = Signal fill with Void tick · **Slider** 4px track filled Signal, 22px thumb with `accent-a20` halo · **Progress** 6px bar on `bg-raised`.
- **Alerts** (inline, persistent state) — `r-md`, tinted fill + border, icon + Special Elite title + body in `fg` at 80%. Signal (You are hidden), danger (Hunter nearby), warn (Final stage), info (Signal weak).
- **Toasts** (events) — `bg-raised`, Line border, `e-2`, max 420px; auto-dismiss after **4s**. Threat alerts also trigger haptics.
- **Sheets & dialogs** — on mobile prefer **bottom sheets** (thumb-reachable, keep the map visible): Surface, `r-lg`, `e-3`, drag handle, icon badge, Special Elite 24px title. Use centred dialogs only to confirm something destructive. Scrim = Void at 70%.
- **Lists & cards** — rows 12×18 padding with `border-subtle` separators; avatar + name + Soft subline + trailing pill or arrow. Cards: Surface, `border-subtle`, `r-md`, `e-1`.

## Game UI

- **HUD** — 2-column grid of stat tiles pinned above the bottom safe area. Tile: UPPERCASE label in Soft, value in Special Elite (~30px; 20px on compact) in Signal, units smaller in Soft. A zero/inactive value (e.g. Penalty 0) goes Ghost.
- **Status bar** — floats over the map: translucent Void `rgba(12,14,12,.82)` with backdrop blur, `border-subtle`, role pill on the left, signal/live indicators on the right.
- **Formatting** — distances as whole metres up to 999 m, then km with one decimal (`8.8 km`, never `8795248m`). Timers always `mm:ss`.
- **Threat states** — colour changes at fixed thresholds **and the label changes too**:
  - Prey · nearest hunter: ≥100 m **Safe** (Signal) · 50–99 m **Caution** (amber) · <50 m **Danger** (Hunter, flash + haptic).
  - Hunter · closest prey: <50 m **In range** (Signal, TAG enabled) · 50–100 m **Approaching** (amber) · >100 m **Tracking** (Soft).
  - Game timer: normal (Signal, static) · final stage (amber, label appears) · last 5 min (Hunter, 1s flash loop).
- **Radar** — Void disc, Signal Deep rings every 25% of radius plus cross axes, linear 3.4s conic sweep. You are the pale `fg` dot in the centre; blips are 10px with a coloured glow and optional 2s ping ring (green = prey, red = hunter).
- **Map** — dark, desaturated style so markers are the brightest things on screen. Markers are 20px with a **3px Void keyline** + glow: You = `fg`, Prey = Signal, Hunter = Hunter, Waypoint = Info, **Stale** (older than 2 updates) = Ghost.
- **Play area** — 2px border + 6% tinted fill: Signal for the normal area, dashed Caution for shrinking, Hunter for out-of-bounds. In play, the playfield outline takes the viewer's role colour (green for prey, red for hunter).
- **Screen compositions** (see `#screens`): map first, chrome translucent, one primary action. Lobby = title, join-code card with Share, player list with role pills, full-width "Start hunt" pinned at the bottom. In-play = status bar on top, map in the middle, stat tiles at the bottom (prey also gets a "Next update" progress bar; hunter gets the TAG FAB).

## Quick workflow when doing UI work

1. Grep `designs/the-prey-design-system.html` for the relevant section `id` (or open it in a browser) to see the intended look.
2. Check `designs/screens/` for the screen's content/flow — but follow v2 for styling.
3. Style exclusively through the central theme tokens — no hard-coded colors, fonts or sizes.
4. Numeric readouts in Special Elite; labels UPPERCASE PT Mono in Soft; Ghost only for disabled.
5. Signal green only for live/actionable data; glow only for live or pressed states; one primary action per screen.
6. Every role/threat colour comes with a label or icon; copy is short, imperative, no exclamation marks.
