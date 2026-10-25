# Design

## Context

- `website/src` is a Hugo site with `en` / `nl` content dirs, structured front matter rendered by `layouts/index.html` and section layouts, and one stylesheet (`assets/css/main.css`, ~1060 lines) whose `:root` holds v1 tokens (`--bg-*`, `--signal*`, `--radius: 3px`), plus a scanline `body::before` and grain `body::after`.
- The nav draws an inline reticle SVG; `static/favicon.svg` is a raster-traced v1 logo; `params.ogImage` points at that SVG (many platforms ignore SVG share images).
- `layouts/shortcodes/screenshot.html` takes a fixed `src`; the six images in `static/images/screens/` (6.6 MB) are v1 captures, copies of `designs/screens/`.
- Truth for the copy: the app's `res/values/strings.xml` and `values-nl/strings.xml` (labels), `GameSettings.kt` (options and defaults), `Game.cs` constants (lobby 2–16, tag range 50 m, hunter-delay threshold 25 m / 10 min penalty, boundary penalty 5 min, penalised reporting every 10 s), `GameConfiguration` (boundary penalties default off, not settable in the app), `MarkerStyle.kt` (who sees which markers), and the in-flight app specs.
- The `game-info-site` capability lives in the unarchived `static-game-info-website` change, so this delta's MODIFIED requirements can only be archived after that change is archived.

## Goals / Non-Goals

**Goals:**
- Every claim on the site is traceable to the app or server code as of this change.
- The site looks like v2.0 without changing its information architecture (same sections, same URLs, plus one new guide).

**Non-Goals:**
- No changes to `/games/join/*`, `assetlinks.json`, `staticwebapp.config.json`, Bicep or the deploy workflow.
- No rewrite of privacy or account-removal (they already describe the Android app); only a check that nothing in them contradicts the new copy.
- No new site sections, search, or analytics.
- Become a Tester keeps its content (closed testing on Google Play); only typos ("the The Prey") and styling change.

## Decisions

### 1. Port v2 tokens into the existing stylesheet, don't replace it

Rename the `:root` block to v2's two layers (`--tp-*` primitives, then semantic `--bg`, `--bg-card`, `--fg`, `--fg-muted`, `--accent`, `--danger`, `--warning`, `--alert`, …, spacing `--s-1…`, radii `--r-xs…--r-pill`, elevation, `--glow`, motion durations), copied from `designs/the-prey-design-system.html`. Then update rules in place: remove `body::before` scanlines (keep the subtle grain and radial atmosphere), swap `--radius: 3px` usages for the radius scale (cards `--r-md`, buttons `--r-sm`, pills `--r-pill`), move label colours from ghost to soft, set button/nav/switcher min-height 48 px, and limit `text-shadow`/`box-shadow` glow to `:hover`, `:active`, `.live` and the radar.
*Alternative:* adopt the design-system HTML's stylesheet wholesale. Rejected: it targets a style-manual page, not Hugo's layouts; a port keeps the diff reviewable.

### 2. Brand assets from `designs/logo/`

Nav uses the v2 mark inline (from `the-prey-mark.svg`, `viewBox="64 64 384 384"` as in the manual) so it inherits no external request; `static/favicon.svg` becomes `the-prey-mark.svg`; add `static/images/brand/the-prey-share.png` (1200×630, lockup on Void, rendered from `the-prey-lockup.svg`) and point `params.ogImage` at it; add `apple-touch-icon.png` from `the-prey-app-icon-512.png` for home-screen bookmarks.

### 3. Language-aware screenshot shortcode

`{{< screenshot name="area-editor" alt="…" caption="…" >}}` resolves `/images/screens/{{ .Page.Language.Lang }}/area-editor.webp`, with `width`/`height` attributes and `loading="lazy"`. Keeping `src` for back-compat is unnecessary: all call sites are rewritten in this change. Hugo's `resources` is not needed; images stay in `static/`.
Screens to capture, both languages (Pixel-class emulator, 1080 px wide, exported as WebP ≤ 200 KB): `main-menu`, `settings`, `playfields-list`, `playfield-details`, `area-editor` (polygon with a selected point), `create-game`, `lobby`, `prey-hud`, `hunter-hud`, `tag-sheet`, `game-result`. Per-app language switching in Settings gives the Dutch set without changing the emulator locale. Callsigns and playfield names in captures are made-up (no real players).
*Alternative:* one English set for both languages. Rejected by the multilingual rules and the new spec.

### 4. Copy is written from a fact sheet

Before rewriting pages, build `openspec/changes/website-update-to-kotlin-app/facts.md`: each rule, number and label with its source file. Every page pair is then rewritten against it, English first, Dutch in the same task, using the glossary in the `the-prey-multilingual` skill (Prey, Hunter, Penalty → Straf, …). Labels are quoted exactly as in the app's strings, in each language.

Key corrections the fact sheet must carry:
- Phases: hunter delay 5 → active hunt ~15 → final stage 10 (30 min default); pings every 3 min, 1 min in the final stage, also during the hunter delay.
- Creating: Play now → choose playfield (Private or Public) → settings → Create game → lobby (game code, Share, settings, tap a player to make them hunter) → players tap Set ready → Start game. Leaving as creator cancels the game.
- Joining: only through the shared invitation link (App Link to the app, or the web invitation page); no code-entry screen in the app.
- Tagging: server-checked within 50 m; the sheet lists candidates; a 409 refusal means the prey got away.
- Penalties: hunter moving > 25 m during the delay → 10 min; boundary penalty 5 min when on for the game (not configurable in the app today, so worded as "when the game has boundary penalties on" and not presented as a setting).
- Silent: tracking runs as a foreground service with a notification; location stops → server sees you silent → can be ruled out; after a reboot a notification asks to resume.

### 5. Home phase cards stay data-driven

The three phase cards keep coming from `_index.md` front matter; only values change (`5` / `~15` / `10` min, final card accent `caution`). No layout change.

## Risks / Trade-offs

- [App copy drifts again as in-flight app changes land] → Fact sheet lists sources; the integration task re-checks labels against `strings.xml` right before merge. Content for the delete-point button and first-time tours is only published once those app changes ship (or worded without the feature).
- [Screenshots need a running backend and two signed-in devices for lobby/HUD shots] → Use the local Aspire stack with two emulators; HUD captures can use a short 30-minute test game.
- [Archive order] → `static-game-info-website` must be archived before this change, or `openspec archive` refuses the MODIFIED delta.
- [Large binary churn in git] → WebP at ~1080 px wide keeps the 22 new files well under the 6.6 MB being removed.

## Migration Plan

Ship as one site deploy through the existing GitHub Actions workflow. URLs stay the same except the new `/en/how-to/get-started/` and `/nl/how-to/get-started/`. Rollback is redeploying the previous commit.
