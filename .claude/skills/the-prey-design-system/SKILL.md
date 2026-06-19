---
name: the-prey-design-system
description: The Prey's visual design system and tactical UI style manual for the Ionic/Angular client (src/ThePrey). Use whenever building, styling, or reviewing any UI in The Prey app — pages, components, overlays, HUD, colors, fonts, spacing — so the result matches the phosphor-green tactical aesthetic. The full visual reference is designs/the-prey-design-system.html.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Design System

The Prey is a location-based hunter-vs-prey game. Its UI is a **dark, tactical, phosphor-green "field operations" aesthetic** — think night-vision HUD, monospaced readouts, corner-bracket chrome, glowing signal markers. Every screen in the Ionic/Angular client (`src/ThePrey`) must read as part of this single visual language.

## The visual reference

The authoritative style manual is **`designs/the-prey-design-system.html`** (relative to repo root). It shows the palette, typography, components, and HUD patterns rendered visually.

⚠️ **Do not `Read` that file as text.** It is a ~400KB self-unpacking *bundled* page — the markup is JS-escaped inside a single `<script type="__bundler/template">` line, so reading it returns noise and burns context. To actually see it, **open it in a browser**:

- Suggest the user run: `! start designs/the-prey-design-system.html` (Windows) to open it, or
- Use the `playwright-cli` skill / Playwright MCP to navigate to the local file and screenshot it when you need to inspect a specific component visually.

Supporting visual assets:
- `designs/screens/*.png` — reference mockups: `main-menu`, `create-new-operation`, `game-lobby`, `player-settings`, `playfield-set-area`, `in-play-hunter-screen`.
- `designs/Additional/` — logos (`logo.svg`, `app-logo.png`, `logo-icon.png`), background art, and `bg-the-prey.html`.

## Tokens are code, not guesswork

The design system is already encoded as CSS custom properties. **Never hard-code a color, font, or hex value** — always use a token. The canonical, ground-truth source is:

**`src/ThePrey/src/theme/variables.scss`** — defines both the design tokens (`--tp-*`) and the Ionic semantic tokens (`--ion-*`), with full **light + dark** variants. Dark mode is the primary identity; light mode is a daylight-legible field-green palette. When in doubt, read this file — it is more current than any summary here.

### Design tokens — `--tp-*` (dark mode / primary identity)

| Token | Value | Use |
|---|---|---|
| `--tp-bg-void` | `#0c0e0c` | Deepest inset (tab bar, insets) |
| `--tp-bg-base` | `#181b17` | Page background |
| `--tp-bg-surface` | `#23271f` | Cards, items, surfaces |
| `--tp-bg-surface-2` | `#2d3328` | Raised surface |
| `--tp-line` | `#39402f` | Borders / hairlines |
| `--tp-line-soft` | `#2a2f24` | Subtle dividers |
| `--tp-signal` | `#64ff00` | **Hero green** — brand, primary action, self-dot |
| `--tp-signal-dim` | `#3f9e00` | Secondary accent / light-mode primary |
| `--tp-signal-deep` | `#1f4d00` | Deep accent |
| `--tp-signal-glow` | `rgba(100,255,0,.35)` | Glow shadow on signal elements |
| `--tp-hunter` | `#ff2f1f` | **Hunter red** — danger, hunter chrome, prey blips |
| `--tp-hunter-dim` | `#a01408` | Hunter pressed/activated |
| `--tp-prey` | `#64ff00` | Prey identity (same green as signal) |
| `--tp-caution` | `#ffb300` | **Amber** — penalties, warnings, alert banners |
| `--tp-info` | `#13e0c8` | Info teal |
| `--tp-text` | `#dcf6d2` | Primary text (pale phosphor) |
| `--tp-text-soft` | `#8c9a83` | Secondary text |
| `--tp-text-ghost` | `#5a6553` | Disabled / placeholder |
| `--tp-head` | `'Special Elite', 'Courier New', monospace` | Display / headings / numeric readouts |
| `--tp-body` | `'PT Mono', 'Courier New', monospace` | Body and labels |

### Ionic semantic mapping (`--ion-color-*`, dark mode)

Built-in Ionic components inherit these — use `color="primary"`, `color="danger"`, etc. rather than overriding:

- **primary** = signal green `#64ff00` (hero action) · **secondary** = signal dim `#3f9e00`
- **tertiary** = info teal `#13e0c8` · **success** = signal green
- **warning** = caution amber `#ffb300` · **danger** = hunter red `#ff2f1f`
- **dark** = void `#0c0e0c` · **medium** = text-soft `#8c9a83` · **light** = raised surface `#2d3328`

### Local aliases on gameplay pages

The in-game HUD pages (`game-hunter.page.scss`, `game-prey.page.scss`, `game-lobby.page.scss`) declare a local short-name alias set at the top of the file — `--signal`, `--signal-glow`, `--hunter`, `--hunter-dim`, `--hunter-glow`, `--caution`, `--caution-glow`, `--bg-void`, `--bg-base`, `--bg-surface`, `--text`, `--text-soft`, `--text-ghost`, `--line` — mirroring the `--tp-*` values. The hunter-view/prey-view specs reference these short names (e.g. `--signal` #64ff00, `--hunter` #ff2f1f). When editing those pages, reuse the page's existing aliases; for new/shared components prefer the global `--tp-*` tokens.

## Typography rules

- **`Special Elite`** (`--tp-head`) — typewriter display face. Headings, titles, and **all numeric readouts** (countdowns, distances, counts, timers).
- **`PT Mono`** (`--tp-body`) — everything else.
- **Labels are UPPERCASE** PT Mono (small, letter-spaced) — e.g. `NEXT UPDATE`, `PREYS LEFT`, `TIME REMAINING`.

## Aesthetic cues (match these when building UI)

- **Dark, high-contrast** void/base/surface layering; phosphor-green text on near-black.
- **Corner-bracket chrome** — framing UI panels with `⌐ ¬ L ⌐` style corner brackets in the role color (signal green for prey/system, hunter red for hunter screens).
- **Glow** — signal/hunter elements get a soft glow via `--*-glow` box-shadows; the player's self-dot **pulses**, enemy/prey blips **flash**.
- **Alert banners** — amber (`--tp-caution`) border + icon + UPPERCASE title for warnings (boundary, penalty, "Signal lost. Find open sky.").
- **Role coloring** — hunter UI leans red (`--tp-hunter`), prey/system UI leans green (`--tp-signal`/`--tp-prey`); playfield polygon stroke is green for prey view, red for hunter view.
- **Ionic-native first** — build with `ion-*` components and theme them through the tokens above; only drop to raw HTML/SVG (e.g. Leaflet map markers) where Ionic has no equivalent.

## Quick workflow when doing UI work

1. Read `src/ThePrey/src/theme/variables.scss` for the live token values (source of truth).
2. Open `designs/the-prey-design-system.html` in a browser (or screenshot via Playwright) to see the intended look of the component you're building.
3. Cross-check the relevant mockup in `designs/screens/` if the screen exists.
4. Style exclusively through `--tp-*` / `--ion-*` tokens (or the gameplay pages' local aliases) — no hard-coded colors or font names.
5. Keep numeric readouts in `Special Elite`, labels UPPERCASE in `PT Mono`.
