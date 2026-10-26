## Context

The Prey has a defined visual design system (see `designs/the-prey-style-guide.html`) and documented game mechanics (`docs/game-design/`). The backend is an ASP.NET Core modular monolith; no server-side rendering is needed for a marketing/info site. The site must serve two audiences — English-speaking and Dutch-speaking players — with parity of content between languages.

## Goals / Non-Goals

**Goals:**
- Ship a static Hugo site under `/website/` with English and Dutch content parity.
- Apply The Prey design language faithfully: `#181B17` base, `#64FF00` signal green, Special Elite headings, PT Mono body, 3px radius, scanline atmosphere.
- Support Hugo's built-in multilingual mode (`languages` config) so `/en/` and `/nl/` are separate URL namespaces with a language switcher on every page.
- Cover all content sections: Home, Rules, Roles (Prey / Hunter), Playfield, FAQ, How-To guides.
- GitHub Actions CI/CD pipeline to build on push and deploy to a static host.

**Non-Goals:**
- No server-side logic, authentication, or dynamic content.
- No third language beyond English and Dutch in v1.
- No CMS integration (content lives as Markdown in the repo).
- No interaction with the game API.

## Decisions

### Hugo as the static site generator
Hugo is one of the most widely used static site generators, has first-class multilingual support via its `languages` config block, and produces zero-JavaScript by default (we add only what we write). Alternatives considered:
- **Astro** — excellent but requires Node.js toolchain and has more complex i18n setup.
- **Jekyll** — GitHub Pages native but Ruby toolchain and slower builds; i18n via plugins.
- **Docusaurus** — React-based, opinionated docs layout that fights the custom tactical design.

Hugo wins on build speed, native i18n, and minimal toolchain overhead.

### Multilingual approach: Hugo `languages` config + content directory per language
Hugo's recommended pattern: define `en` and `nl` in `hugo.toml`, place content at `content/en/` and `content/nl/`. Each language renders to `/en/` and `/nl/` prefixes. A `defaultContentLanguage = "en"` makes `/` redirect to `/en/`.

Alternative considered: single content tree with `.en.md` / `.nl.md` suffixes. Rejected because it scatters translations and makes bulk content edits harder to review.

### i18n string files for UI chrome
Navigation labels, button text, and UI microcopy that appear in templates (not Markdown content) are stored in `i18n/en.yaml` and `i18n/nl.yaml`. Hugo's `i18n` function resolves them at build time. This keeps template HTML language-neutral.

### Design system as plain CSS (no Tailwind / CSS framework)
The Prey style guide is already a complete design token set. Copying the CSS variables and component styles from the style guide into a `assets/css/main.css` is the most faithful approach and avoids a build step for CSS. A CSS framework would fight the custom design.

### Deployment target: GitHub Pages via GitHub Actions
Simple, free, zero-infrastructure. The Hugo binary is available as a GitHub Actions step (`peaceiris/actions-hugo`). The build output (`public/`) is pushed to the `gh-pages` branch. Azure Static Web Apps is an acceptable alternative if the team prefers Azure.

### Language switcher placement
Placed in the top navigation bar, showing the alternate language code as a pill (`NL` / `EN`). Hugo provides `.Translations` on every page, giving the direct URL to the same page in the other language. No JavaScript needed.

## Risks / Trade-offs

- **Translation maintenance** — two content trees must be kept in sync manually. If a rules update happens, both `content/en/rules.md` and `content/nl/rules.md` must be updated. Mitigation: a CI lint step can warn when file counts diverge between language trees.
- **Font loading (Google Fonts)** — Special Elite and PT Mono are loaded from `fonts.googleapis.com`, matching the style guide. This adds a network request and a GDPR consideration for Dutch/EU visitors. Mitigation: self-host the fonts in `static/fonts/` and serve them from the site itself.
- **Hugo version pinning** — Hugo's multilingual config syntax changed between v0.110 and v0.120. Mitigation: pin the Hugo version in the GitHub Actions workflow and in a `.hugo-version` file.
- **Content parity drift** — English content may be written first; Dutch translation may lag. Mitigation: mark untranslated pages with a `translationPending: true` front matter flag that the template renders as a banner.

## Migration Plan

1. Create `/website/` directory with `hugo.toml`, theme skeleton, and CSS from the style guide.
2. Add content stubs for all pages in both `content/en/` and `content/nl/`.
3. Verify `hugo server` renders correctly locally.
4. Add GitHub Actions workflow (`.github/workflows/deploy-website.yml`).
5. Enable GitHub Pages on the repository (or configure Azure Static Web Apps).
6. Rollback: the site is purely additive — disabling the Actions workflow or unpublishing the Pages branch has no effect on the backend.

## Open Questions

- **Deployment target confirmed?** GitHub Pages vs Azure Static Web Apps — team preference needed.
- **Custom domain?** e.g. `theprey.nl` — requires DNS config outside this change.
- **Who translates Dutch content?** Developer-written Dutch is acceptable for v1 but a native review pass is recommended before launch.
