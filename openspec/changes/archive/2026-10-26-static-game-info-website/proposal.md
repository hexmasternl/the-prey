## Why

The Prey has no public-facing web presence. Prospective players have no way to learn the rules, understand their role, or troubleshoot common issues without someone explaining it in person — creating a barrier to adoption and first-session friction. A static informational website fixes this by giving players a self-serve reference they can bookmark and read before downloading the app.

## What Changes

- Add a new Hugo-based static website (`/website/`) at the root of the repository.
- The site covers: game overview, rules, role guides (Prey & Hunter), playfield instructions, FAQ, and how-to guides.
- Full multi-language support for **English** (default) and **Dutch**, using Hugo's built-in i18n system.
- A language switcher is present in the navigation on every page.
- Visual identity matches the existing design system: dark surfaces, signal green (#64FF00), Special Elite / PT Mono typefaces, tactical aesthetic — making the site feel like an extension of the game's instrument panel.
- No backend changes; the site is purely static HTML/CSS deployed via Hugo.
- A GitHub Actions workflow builds and deploys the site (e.g. to GitHub Pages or Azure Static Web Apps).

## Capabilities

### New Capabilities

- `game-info-site`: Public Hugo static website presenting game rules, role guides, playfield instructions, FAQs, and how-to content styled with The Prey design language, with full English and Dutch language support.

### Modified Capabilities

*(none)*

## Impact

- **New directory:** `/website/` containing all Hugo source files.
- **New GitHub Actions workflow** for building and deploying the site.
- No changes to the backend API, Aspire orchestration, or Ionic client.
- Fonts (Special Elite, PT Mono) loaded from Google Fonts — same as the style guide.
- No new NuGet or npm runtime dependencies on the backend.
