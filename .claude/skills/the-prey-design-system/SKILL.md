---
name: the-prey-design-system
description: The Prey's visual design system and tactical UI style manual. Use whenever building, styling, or reviewing any UI for The Prey (app, emails, website) — pages, components, overlays, HUD, colors, fonts, spacing — so the result matches the phosphor-green tactical aesthetic. The full visual reference is designs/the-prey-design-system.html.
metadata:
  author: the-prey
  version: "2.0"
---

# The Prey — Design System

The Prey is a location-based hunter-vs-prey game. Its UI is a **dark, tactical, phosphor-green "field operations" aesthetic** — think night-vision HUD, monospaced readouts, corner-bracket chrome, glowing signal markers. Every screen of a client app must read as part of this single visual language.

## The visual reference

The authoritative style manual is **`designs/the-prey-design-system.html`** (relative to repo root). It shows the palette, typography, components, and HUD patterns rendered visually.

⚠️ **Do not `Read` that file as text.** It is a ~400KB self-unpacking *bundled* page — the markup is JS-escaped inside a single `<script type="__bundler/template">` line, so reading it returns noise and burns context. To actually see it, **open it in a browser**:

- Suggest the user run: `! start designs/the-prey-design-system.html` (Windows) to open it, or
- Use the `playwright-cli` skill / Playwright MCP to navigate to the local file and screenshot it when you need to inspect a specific component visually.

Supporting visual assets:
- `designs/screens/*.png` — reference mockups: `main-menu`, `create-new-operation`, `game-lobby`, `player-settings`, `playfield-set-area`, `in-play-hunter-screen`.
- `designs/Additional/` — logos (`logo.svg`, `app-logo.png`, `logo-icon.png`), background art, and `bg-the-prey.html`.
- `designs/SpecialElite-Regular.ttf`, `designs/PTMono-Regular.ttf` — the two typefaces.

## Tokens, not guesswork

**Never hard-code a color, font, or hex value in a component** — define these tokens once in the client's central theme and reference them everywhere.

### Design tokens (dark mode / primary identity)

| Token | Value | Use |
|---|---|---|
| `bg-void` | `#0c0e0c` | Deepest inset (tab bar, insets) |
| `bg-base` | `#181b17` | Page background |
| `bg-surface` | `#23271f` | Cards, items, surfaces |
| `bg-surface-2` | `#2d3328` | Raised surface |
| `line` | `#39402f` | Borders / hairlines |
| `line-soft` | `#2a2f24` | Subtle dividers |
| `signal` | `#64ff00` | **Hero green** — brand, primary action, self-dot |
| `signal-dim` | `#3f9e00` | Secondary accent / light-mode primary |
| `signal-deep` | `#1f4d00` | Deep accent |
| `signal-glow` | `rgba(100,255,0,.35)` | Glow shadow on signal elements |
| `hunter` | `#ff2f1f` | **Hunter red** — danger, hunter chrome, prey blips |
| `hunter-dim` | `#a01408` | Hunter pressed/activated |
| `prey` | `#64ff00` | Prey identity (same green as signal) |
| `caution` | `#ffb300` | **Amber** — penalties, warnings, alert banners |
| `info` | `#13e0c8` | Info teal |
| `text` | `#dcf6d2` | Primary text (pale phosphor) |
| `text-soft` | `#8c9a83` | Secondary text |
| `text-ghost` | `#5a6553` | Disabled / placeholder |
| `head` font | `'Special Elite', 'Courier New', monospace` | Display / headings / numeric readouts |
| `body` font | `'PT Mono', 'Courier New', monospace` | Body and labels |

Dark mode is the primary identity; a light mode, if offered, is a daylight-legible field-green palette using `signal-dim` as primary.

### Semantic mapping

- **primary** = signal green `#64ff00` (hero action) · **secondary** = signal dim `#3f9e00`
- **tertiary** = info teal `#13e0c8` · **success** = signal green
- **warning** = caution amber `#ffb300` · **danger** = hunter red `#ff2f1f`
- **dark** = void `#0c0e0c` · **medium** = text-soft `#8c9a83` · **light** = raised surface `#2d3328`

## Typography rules

- **`Special Elite`** (head) — typewriter display face. Headings, titles, and **all numeric readouts** (countdowns, distances, counts, timers).
- **`PT Mono`** (body) — everything else.
- **Labels are UPPERCASE** PT Mono (small, letter-spaced) — e.g. `NEXT UPDATE`, `PREYS LEFT`, `TIME REMAINING`.

## Aesthetic cues (match these when building UI)

- **Dark, high-contrast** void/base/surface layering; phosphor-green text on near-black.
- **Corner-bracket chrome** — framing UI panels with `⌐ ¬ L ⌐` style corner brackets in the role color (signal green for prey/system, hunter red for hunter screens).
- **Glow** — signal/hunter elements get a soft glow; the player's self-dot **pulses**, enemy/prey blips **flash**.
- **Alert banners** — amber (`caution`) border + icon + UPPERCASE title for warnings (boundary, penalty, "Signal lost. Find open sky.").
- **Role coloring** — hunter UI leans red (`hunter`), prey/system UI leans green (`signal`/`prey`); playfield polygon stroke is green for prey view, red for hunter view.

## Quick workflow when doing UI work

1. Open `designs/the-prey-design-system.html` in a browser (or screenshot via Playwright) to see the intended look of the component you're building.
2. Cross-check the relevant mockup in `designs/screens/` if the screen exists.
3. Style exclusively through the client's central theme tokens — no hard-coded colors or font names.
4. Keep numeric readouts in `Special Elite`, labels UPPERCASE in `PT Mono`.
