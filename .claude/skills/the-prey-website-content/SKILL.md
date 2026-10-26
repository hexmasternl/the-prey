---
name: the-prey-website-content
description: How to build and change pages on The Prey's Hugo website (theprey.nl, website/src) — content/en + content/nl page pairs, the front matter fields the templates actually read, the home and tester data-driven pages, layouts, partials, the qa and screenshot shortcodes, nav/footer entries, i18n strings, images and per-language screenshots, SEO/Open Graph fields, styling with the design-system tokens in assets/css/main.css, and the build/deploy pipeline (deploy-website.yml, Azure Static Web App, staticwebapp.config.json, App Links files). Use whenever you add, edit, move or delete a website page, section, menu or footer link, FAQ entry, how-to guide, screenshot, SEO description or keyword, layout, partial, shortcode or website CSS/JS, and whenever you touch website/infrastructure or the website workflow.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Website Content (Hugo)

The site is plain Hugo, no theme, no Node toolchain: everything lives in `website/src/`. It is bilingual (EN default, NL) — **every change follows the-prey-multilingual** (page pairs, i18n keys, Dutch style). Colours, fonts and tone follow **the-prey-design-system**. Game facts on the site must match **the-prey-game-rules**.

## Layout of `website/src`

| Path | What |
|---|---|
| `hugo.toml` | `baseURL https://theprey.nl/`, languages `en` (weight 1) and `nl` (weight 2), both in a subfolder (`/en/…`, `/nl/…`); `params.description`, `params.keywords`, `params.ogImage` site fallbacks; goldmark `unsafe = true` (inline HTML allowed) |
| `.hugo-version` | Hugo version CI installs (currently `0.124.0`, **non-extended**) |
| `content/en/`, `content/nl/` | Pages, same paths in both |
| `layouts/_default/` | `baseof.html` (head, SEO, nav, footer), `single.html`, `list.html`, `_markup/render-table.html` (wraps tables in `.table-scroll`) |
| `layouts/index.html` | Home page, driven by front matter of `content/*/_index.md` |
| `layouts/{how-to,faq,roles,tester}/` | Section/type layouts |
| `layouts/partials/` | `nav`, `footer`, `page-header`, `page-list`, `role-card`, `radar`, `tester-burst`, `translation-pending-banner` |
| `layouts/shortcodes/` | `qa`, `screenshot` |
| `i18n/en.yaml`, `i18n/nl.yaml` | Template strings (nav, footer, banners) |
| `assets/css/main.css`, `assets/js/main.js` | One stylesheet and one script, minified + fingerprinted via Hugo Pipes |
| `static/` | Copied as-is: favicon, `images/brand/the-prey-share.png` (OG image), `images/screens/<lang>/*.webp`, `.well-known/assetlinks.json`, `games/join/index.html`, `privacy/index.html` (redirect), `staticwebapp.config.json` |

`public/`, `resources/_gen/`, `.hugo_build.lock` and `hugo_stats.json` are build output and git-ignored (`website/src/.gitignore`). Never commit them.

## Pages and front matter

Sections: `rules/`, `roles/` (`hunter`, `prey`, `game-creator`), `playfield/`, `faq/`, `how-to/` (six guides), plus single pages `become-a-tester`, `privacy`, `account-removal`.

Fields the templates read — don't invent others without a template change:

| Field | Used by | Notes |
|---|---|---|
| `title` | every page, `<title>` = `Title // The Prey`, page header, lists | Title Case |
| `description` | `<meta name=description>`, OG description, page lede, list blurbs, role-card tagline | ~120–160 chars; falls back to `params.description` |
| `keywords` | `<meta name=keywords>` | list; real search terms per language |
| `image` | `og:image` | optional, path under `static/`; default `images/brand/the-prey-share.png` (1200×630) |
| `weight` | ordering in `page-list` | used by how-to guides (1–6); set it on every new guide |
| `type` | layout selection | `type: "tester"` → `layouts/tester/single.html` |
| `translationPending` | NL banner | temporary only, see the-prey-multilingual |
| `hero`, `game`, `phases`, `roles` | home (`content/*/_index.md`) | `phases[]`: `value`, `unit`, `accent` (`signal`/`caution`), `title`, `desc` — numbers must match the default settings |
| `mailSubject`, `hero`, `intro`, `steps`, `requirements`, `cta` | tester page | strings may contain inline HTML (`safeHTML`) |

Default page skeleton:

```markdown
---
title: "Leave a Game"
weight: 7
description: "How to leave a game in The Prey before it starts, and what happens when the Game Creator leaves."
keywords: ["leave a game", "lobby", "The Prey how-to"]
---

1. Open the lobby…
```

Body is Markdown. Use `##` headings (the page header renders the `h1`), Markdown tables (auto-wrapped for horizontal scroll), numbered lists for steps (`.howto-content ol` is styled as steps), `---` between major sections as on the rules page. Internal links in content: `[Draw one first]({{< relref "/how-to/draw-a-playfield" >}})` — `relref` resolves to the same language.

## Shortcodes

| Shortcode | Usage |
|---|---|
| `qa` | FAQ accordion: `{{< qa q="How does tagging work?" >}}Markdown answer{{< /qa >}}` — only in `faq/_index.md` |
| `screenshot` | `{{< screenshot name="lobby" alt="…" caption="…" >}}` renders `static/images/screens/<lang>/<name>.webp` with real width/height and lazy loading. Same `name` in the EN and NL twin, translated `alt`/`caption`. A missing file renders nothing and logs `WARN screenshot …` |

Names in use: `area-editor`, `create-game`, `game-result`, `hunter-hud`, `lobby`, `main-menu`, `playfield-details`, `playfields-list`, `prey-hud`, `settings`, `tag-sheet`. **None of the `.webp` captures exist yet**, so every build currently warns. Adding them: capture the Android app in English and in Dutch, convert to WebP offline (CI Hugo is non-extended, so no image processing at build time), keep a consistent phone width, and save as `static/images/screens/en/<name>.webp` and `static/images/screens/nl/<name>.webp`. The old PNGs in `static/images/screens/*.png` and `designs/screens/` are unused legacy captures.

## Adding things

**A page in an existing section:** create `content/en/<section>/<slug>.md` and `content/nl/<section>/<slug>.md` (same slug, English). Section list pages pick it up automatically via `page-list`.

**A new top-level section:** `content/{en,nl}/<section>/_index.md`. If it needs a custom look, add `layouts/<section>/list.html` (copy `how-to/list.html`, which passes an i18n eyebrow to `page-header`). Then add it to the menu.

**A menu entry:** the nav is hand-written in `layouts/partials/nav.html` — add an `<li>` like the others, using `{{ "/<section>/" | relLangURL }}`, the `hasPrefix .RelPermalink (printf "/%s/<section>" .Lang)` active check, and a new `nav_<section>` key in **both** `i18n/en.yaml` and `i18n/nl.yaml`. Check the mobile menu (hamburger under the breakpoint) still fits. Footer links live in `partials/footer.html` with `footer_*` keys.

**A FAQ entry:** add a `qa` block to both `faq/_index.md` files, in the same position.

**Text in templates:** never literal — `{{ i18n "key" }}` with the key in both YAML files. (Known exception to clean up: the home ticker strings in `layouts/index.html` are hard-coded English.)

## Styling

- Use the tokens in the `:root` block of `assets/css/main.css` (`--accent`, `--danger`, `--warning`, `--bg-card`, `--font-display`, `--s-4`, `--r-md`, …). The primitives (`--tp-*`) are the design-system values and must not change; no hex values in new rules.
- Reuse existing components before writing CSS: `.btn` (`primary`, `ghost`, `btn-sm`), `.pill`, `.panel`, `.alert` (`ok`, `warn`), `.pcard`, `.eyebrow`, `.phase-grid`, `.roles-grid`, `.page-list`, `.steps`, `.signal-divider`, `.hero` with `.br` corner brackets.
- Scroll reveals: add `class="reveal"` (or `reveal-stagger` on a grid); count-up: `data-count`. Both are progressive enhancement from `assets/js/main.js`; the page must work without JS.
- Headings in Special Elite, body in PT Mono (Google Fonts in `baseof.html`). Calm, imperative tone — see the-prey-design-system.
- Test at phone width: no horizontal scroll (the `.site-shell` grid uses `minmax(0, 1fr)` for this reason).

## SEO checklist per page

`title` + `description` + `keywords` in both languages (Dutch keywords are real Dutch search terms), optional `image` for a custom share card, descriptive `alt` on every screenshot. `<html lang>`, canonical language URLs and the language switcher come from Hugo translations — they only work when the twin has the same path.

## Special static files — handle with care

| File | Why |
|---|---|
| `static/.well-known/assetlinks.json` | Android App Links verification for `nl.hexmaster.theprey`; served as JSON via `staticwebapp.config.json`. Changing it breaks invitation links in the app |
| `static/games/join/index.html` | Fallback page for invitation links (`/games/join/*` is rewritten to it); `noindex`, self-contained styles |
| `static/privacy/index.html` | Redirect from the old `/privacy/` URL to `/en/privacy/` (keeps old links working) |
| `static/staticwebapp.config.json` | Static Web App routes, rewrites, MIME types |

## Build and deploy

- `.github/workflows/deploy-website.yml` runs on pushes to `main` touching `website/**` (and manual dispatch): `az deployment sub create` with `website/infrastructure/main.bicep` (resource group `rg-theprey-website-prod`, Free Static Web App `theprey-website-prod` in westeurope), installs Hugo from `.hugo-version`, runs `hugo --minify` in `website/src`, and uploads `website/src/public` with the SWA deployment token. There is no PR build — build locally before you push.
- The custom domain `theprey.nl` is bound to the Static Web App outside Bicep (DNS + portal); the Bicep only outputs the default hostname. Infra changes: **the-prey-azure-infra**.
- Work lands on `development` and reaches production only via the PR to `main` (**the-prey-git-workflow**).

## Verify

```powershell
cd website/src
hugo server -D                 # live preview at http://localhost:1313/en/ and /nl/
hugo --gc --minify             # must succeed; no new WARN lines (missing i18n keys, screenshots)
```

Local Hugo may be newer than CI's `0.124.0`: avoid template functions newer than that version, and don't rely on Hugo extended features. Then run the EN/NL pairing and i18n-key diffs from **the-prey-multilingual**, and click through nav, language switcher and every page you touched at phone width.

## Checklist

- [ ] EN and NL page twins with the same path; front matter translated with the same keys.
- [ ] Only template-backed front matter fields; `weight` on new how-to guides.
- [ ] New template text via `i18n` keys in both YAML files; links via `relLangURL` / `relref`.
- [ ] Styling uses tokens and existing components; works at phone width and without JS.
- [ ] Screenshots as `static/images/screens/{en,nl}/<name>.webp` with translated `alt`.
- [ ] Game facts (durations, 50 m, penalties, player counts) match the-prey-game-rules.
- [ ] `hugo --gc --minify` builds clean; `public/` and `resources/` not committed.
- [ ] `assetlinks.json`, `games/join`, `privacy` redirect and `staticwebapp.config.json` untouched unless intended.
