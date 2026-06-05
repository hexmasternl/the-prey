## 1. Hugo Project Scaffold

- [ ] 1.1 Create `/website/` directory with `hugo.toml` configuring `defaultContentLanguage = "en"` and `languages` block for `en` and `nl`
- [ ] 1.2 Add `.hugo-version` file pinning a specific Hugo version (e.g. `0.124.0`)
- [ ] 1.3 Create directory structure: `content/en/`, `content/nl/`, `layouts/`, `layouts/partials/`, `layouts/_default/`, `static/`, `assets/css/`, `i18n/`
- [ ] 1.4 Verify `hugo build` and `hugo server` run without errors from `/website/`

## 2. Design System CSS

- [ ] 2.1 Create `assets/css/main.css` with all CSS custom properties from the style guide (`--bg-void`, `--bg-base`, `--signal`, `--hunter`, `--caution`, `--info`, `--text`, `--head`, `--body`, `--space`, `--radius`)
- [ ] 2.2 Add `body` background with radial gradient overlays, scanline `body::before`, and grain texture `body::after`
- [ ] 2.3 Add base typography rules: Special Elite for headings and readouts, PT Mono for body and labels, uppercase labels with `letter-spacing: 2px`
- [ ] 2.4 Add component CSS: `.hero`, `.btn` variants (primary, ghost, hunter, disabled), `.pill` status indicators, `.panel`, `.alert` variants (danger, warn, ok)
- [ ] 2.5 Add responsive breakpoint at 880px — navigation collapses, main content padding adjusts
- [ ] 2.6 Add Google Fonts `<link>` tags for Special Elite and PT Mono in the base layout (or self-host fonts in `static/fonts/`)

## 3. Base Layouts and Partials

- [ ] 3.1 Create `layouts/_default/baseof.html` with `<html lang="{{ .Lang }}">`, `<head>` (meta, CSS, fonts), `<body>` shell, and `{{ block "main" . }}` slot
- [ ] 3.2 Create `layouts/partials/nav.html` — top nav with reticle SVG logo, section links, and language switcher pill linking to `{{ range .Translations }}{{ .Permalink }}{{ end }}`
- [ ] 3.3 Create `layouts/partials/footer.html` — minimal footer with "THE PREY // END OF TRANSMISSION" copy and signal-green accent
- [ ] 3.4 Create `layouts/partials/translation-pending-banner.html` — `alert.warn` banner rendered when `{{ .Params.translationPending }}`
- [ ] 3.5 Create `layouts/_default/single.html` and `layouts/_default/list.html` base templates

## 4. i18n String Files

- [ ] 4.1 Create `i18n/en.yaml` with UI strings: nav labels (Home, Rules, Roles, Playfield, FAQ, How-To), language switcher label, translation-pending banner text, footer copy
- [ ] 4.2 Create `i18n/nl.yaml` with Dutch equivalents of all strings in `en.yaml`

## 5. Content — English

- [ ] 5.1 Create `content/en/_index.md` — Home page with hero copy, game overview, and CTAs to Rules and How-To
- [ ] 5.2 Create `content/en/rules/_index.md` — Rules page covering timing phases, GPS update schedule, elimination, and win conditions
- [ ] 5.3 Create `content/en/roles/_index.md` — Roles landing page linking to Prey, Hunter, and Game Creator sub-pages
- [ ] 5.4 Create `content/en/roles/prey.md` — Prey role: goal, head-start behavior, in-app experience, background service requirement
- [ ] 5.5 Create `content/en/roles/hunter.md` — Hunter role: goal, head-start wait, GPS tracking, tagging flow
- [ ] 5.6 Create `content/en/roles/game-creator.md` — Game Creator: four pre-game steps, role assignment rules
- [ ] 5.7 Create `content/en/playfield/_index.md` — Playfield: what it is, creation steps, gesture table, saving, starting a game
- [ ] 5.8 Create `content/en/faq/_index.md` — FAQ with at least 10 Q&A entries in correct voice and tone, using `<details>`/`<summary>` accordion
- [ ] 5.9 Create `content/en/how-to/_index.md` — How-To index listing all guides
- [ ] 5.10 Create `content/en/how-to/join-a-game.md` — numbered steps for joining a game
- [ ] 5.11 Create `content/en/how-to/create-a-game.md` — numbered steps for creating a game
- [ ] 5.12 Create `content/en/how-to/draw-a-playfield.md` — numbered steps for drawing a playfield
- [ ] 5.13 Create `content/en/how-to/understand-your-role.md` — guide to understanding your role mid-game
- [ ] 5.14 Create `content/en/how-to/gps-signal-lost.md` — what to do if GPS signal is lost

## 6. Content — Dutch

- [ ] 6.1 Create `content/nl/_index.md` — Dutch Home page (translation of 5.1)
- [ ] 6.2 Create `content/nl/rules/_index.md` — Dutch Rules page (translation of 5.2)
- [ ] 6.3 Create `content/nl/roles/_index.md` — Dutch Roles landing page (translation of 5.3)
- [ ] 6.4 Create `content/nl/roles/prey.md` — Dutch Prey role page (translation of 5.4)
- [ ] 6.5 Create `content/nl/roles/hunter.md` — Dutch Hunter role page (translation of 5.5)
- [ ] 6.6 Create `content/nl/roles/game-creator.md` — Dutch Game Creator page (translation of 5.6)
- [ ] 6.7 Create `content/nl/playfield/_index.md` — Dutch Playfield page (translation of 5.7)
- [ ] 6.8 Create `content/nl/faq/_index.md` — Dutch FAQ page (translation of 5.8)
- [ ] 6.9 Create `content/nl/how-to/_index.md` — Dutch How-To index (translation of 5.9)
- [ ] 6.10 Create `content/nl/how-to/join-a-game.md` — Dutch join guide (translation of 5.10)
- [ ] 6.11 Create `content/nl/how-to/create-a-game.md` — Dutch create game guide (translation of 5.11)
- [ ] 6.12 Create `content/nl/how-to/draw-a-playfield.md` — Dutch draw playfield guide (translation of 5.12)
- [ ] 6.13 Create `content/nl/how-to/understand-your-role.md` — Dutch role guide (translation of 5.13)
- [ ] 6.14 Create `content/nl/how-to/gps-signal-lost.md` — Dutch GPS signal lost guide (translation of 5.14)

## 7. Section-Specific Layouts

- [ ] 7.1 Create `layouts/faq/single.html` rendering `<details>`/`<summary>` accordion with signal-green summary border
- [ ] 7.2 Create `layouts/how-to/single.html` rendering numbered steps with signal-green step counters
- [ ] 7.3 Create `layouts/roles/list.html` — roles landing page with player card components (green for Prey, red for Hunter)
- [ ] 7.4 Create `layouts/index.html` — home page layout with `.hero` block, corner brackets, and CTA buttons

## 8. Verification and Polish

- [ ] 8.1 Verify language switcher on every page links correctly to the same page in the other language
- [ ] 8.2 Verify signal green (`#64FF00`) is used only for actionable/live-data elements — not decorative
- [ ] 8.3 Verify hunter red (`#FF2F1F`) appears only on hunter-role content and warning elements
- [ ] 8.4 Verify all headings use Special Elite and all body text uses PT Mono
- [ ] 8.5 Test responsive layout at 320px, 768px, and 1280px viewport widths
- [ ] 8.6 Add `translationPending: true` to any content page not yet fully translated and confirm banner renders

## 9. GitHub Actions Deployment

- [ ] 9.1 Create `.github/workflows/deploy-website.yml` triggered on push to `main` with path filter `website/**`
- [ ] 9.2 Add build step using `peaceiris/actions-hugo` with version pinned to match `website/.hugo-version`
- [ ] 9.3 Add deploy step publishing `website/public/` to the `gh-pages` branch using `peaceiris/actions-gh-pages`
- [ ] 9.4 Enable GitHub Pages on the repository pointing to the `gh-pages` branch
- [ ] 9.5 Verify a test push triggers the workflow and the site is accessible at the GitHub Pages URL
