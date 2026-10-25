# Tasks

Run Hugo from `website/src` (`hugo --gc --minify`, and `hugo server` to view). Every text change lands in English and Dutch in the same task, per the `the-prey-multilingual` skill. Before archiving, archive `static-game-info-website` first (this delta modifies its `game-info-site` capability).

## 1. Fact sheet

- [x] 1.1 Write `openspec/changes/website-update-to-kotlin-app/facts.md`: game settings and defaults (`GameSettings.kt`), lobby size, tag range, delay and boundary penalties, penalised reporting (`Game.cs`, `GameConfiguration.cs`), marker visibility (`MarkerStyle.kt`), create / join / lobby / playfield / HUD / result flows, and every app label the site quotes in English and Dutch (`strings.xml`, `values-nl/strings.xml`), each with its source file; verify every number in design §4 appears with a source

## 2. Design system v2.0

- [x] 2.1 Replace the `:root` block in `assets/css/main.css` with v2 primitive and semantic tokens, spacing, radius, elevation, glow and motion tokens copied from `designs/the-prey-design-system.html`, and repoint existing rules to them; verify `hugo` builds and no `var(--…)` in the CSS references a removed token (grep)
- [x] 2.2 Apply the v2 rules: remove the `body::before` scanlines, radius scale on cards / buttons / pills, labels in Text Soft, 48 px min-height on buttons, nav links and the language switcher, glow only on hover / active / live / radar; verify in `hugo server` at 375 px and 1280 px widths against the manual's Buttons, Lists & cards and Status sections
- [x] 2.3 Put the v2 mark in `layouts/partials/nav.html`, replace `static/favicon.svg` with `designs/logo/the-prey-mark.svg`, add `static/apple-touch-icon.png` (from the 512 app icon) and `static/images/brand/the-prey-share.png` (1200×630 lockup on Void), link them in `baseof.html` and set `params.ogImage`; verify the rendered `<head>` has the favicon, touch icon and an `og:image` PNG URL

## 3. Screenshots

- [x] 3.1 Rewrite `layouts/shortcodes/screenshot.html` to take `name`, `alt`, `caption` and resolve `/images/screens/{lang}/{name}.webp` with width / height and lazy loading; verify with a temporary page in both languages
- [ ] 3.2 Capture the eleven screens of design §3 in English and Dutch from the current app (local Aspire backend, emulators, made-up callsigns and playfield names), export as 1080 px WebP ≤ 200 KB into `static/images/screens/en/` and `static/images/screens/nl/`; delete the six v1 PNGs from `static/images/screens/`; verify 22 files exist and `public/` after a build contains no v1 PNG

## 4. Home, Rules and Roles

- [x] 4.1 Update `content/{en,nl}/_index.md`: Android framing, phases 5 / ~15 / 10 min of a 30-minute default game, role taglines; verify the home page against the "Phases use the app defaults" scenario in both languages
- [x] 4.2 Rewrite `content/{en,nl}/rules/_index.md` from the fact sheet (settings table, pings, tagging within 50 m, penalties, silent players, win conditions, result dialog); verify each Rules scenario in the spec, including that "honour" / "eer" no longer appears anywhere in `content/` (grep)
- [x] 4.3 Rewrite `content/{en,nl}/roles/prey.md`, `hunter.md`, `game-creator.md` and the roles index (HUD explanations, tag sheet, creator steps, leaving cancels) with `prey-hud`, `hunter-hud`, `tag-sheet` and `lobby` screenshots; verify against the Player roles scenarios

## 5. Playfield, How-To and FAQ

- [x] 5.1 Rewrite `content/{en,nl}/playfield/_index.md` (tabs, public search, creation steps, area-editor table, Public naming rule, penalties summary linking to Rules, playfield chosen when creating a game) with `playfields-list`, `playfield-details` and `area-editor` screenshots; verify against the Playfield scenarios, including that "Confirm Polygon", "New Playfield" and "Start Game" from a playfield are gone (grep both languages)
- [x] 5.2 Add `content/{en,nl}/how-to/get-started.md` (install, sign in, callsign, language, location "All the time" and notifications) with `main-menu` and `settings` screenshots, ordered first in the How-To list (`weight`); verify the index lists six guides in both languages
- [x] 5.3 Rewrite `how-to/create-a-game.md`, `join-a-game.md` (invitation link only), `draw-a-playfield.md`, `understand-your-role.md` (HUD instead of a map tab, result dialog) and `gps-signal-lost.md` (Android settings path, "Allow all the time", tracking and resume notifications, silent → ruled out) in both languages with their screenshots; verify every quoted label matches `facts.md`
- [x] 5.4 Rewrite `content/{en,nl}/faq/_index.md`: correct join, player count 2–16, permissions, tagging range, leaving the playfield, spectators, Android only, creator playing; keep ≥ 10 entries; verify the FAQ scenarios
- [x] 5.5 Update `description` and `keywords` front matter of every changed page and `params.description` in `hugo.toml` if it no longer fits, with real Dutch search terms in `nl`; fix "the The Prey" in `become-a-tester.md` (both languages); verify `hugo` builds without missing-i18n warnings

## 6. Integration check

- [x] 6.1 Grep `website/src/content` and `layouts` for `Ionic`, `MAUI`, `iOS` (except "no iOS"), `Join Game`, `Always Allow`, `Confirm Polygon`, `honour`, `scanline`; verify no stale hits remain
- [ ] 6.2 Re-check every quoted app label in both languages against the current `strings.xml` files, and that `/games/join/<id>/<code>`, `/.well-known/assetlinks.json`, `/privacy/` and `/account-removal/` still render unchanged; verify with `hugo server`
- [ ] 6.3 Review every page in both languages at phone and desktop width with the `the-prey-design-system` checklist (contrast, 48 px targets, glow use, red only for hunter / warnings), and the language switcher on each page lands on its twin, including Get Started
