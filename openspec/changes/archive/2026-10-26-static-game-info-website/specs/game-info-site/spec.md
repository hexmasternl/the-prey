## ADDED Requirements

### Requirement: Hugo project structure
The site SHALL be a Hugo project located at `/website/` in the repository root, with `hugo.toml` as the main configuration file, content under `content/`, layouts under `layouts/`, and static assets under `static/`.

#### Scenario: Hugo builds without errors
- **WHEN** `hugo build` is run from `/website/`
- **THEN** the site compiles to `public/` with no errors or warnings

#### Scenario: Local development server
- **WHEN** `hugo server` is run from `/website/`
- **THEN** the site is served at `http://localhost:1313` with live reload

---

### Requirement: Multi-language support — English and Dutch
The site SHALL support two languages: English (`en`, default) and Dutch (`nl`), configured via Hugo's `languages` block in `hugo.toml`. Content SHALL live at `content/en/` and `content/nl/` respectively. All pages SHALL have a corresponding translation in both languages.

#### Scenario: Default language resolves to English
- **WHEN** a visitor navigates to the site root (`/`)
- **THEN** they are redirected to or served the English home page at `/en/`

#### Scenario: Dutch language pages exist
- **WHEN** a visitor navigates to `/nl/`
- **THEN** they are served the Dutch home page with all navigation labels in Dutch

#### Scenario: Content parity — page exists in both languages
- **WHEN** a page exists at `content/en/<section>/<page>.md`
- **THEN** a corresponding page SHALL exist at `content/nl/<section>/<page>.md`

---

### Requirement: Language switcher
Every page SHALL include a language switcher in the top navigation that links directly to the same page in the alternate language. The switcher SHALL display the language code of the alternate language (`NL` when on English pages, `EN` when on Dutch pages) styled as a pill using the design system's pill component.

#### Scenario: Language switcher navigates to same page in other language
- **WHEN** a visitor is on the English Rules page (`/en/rules/`) and clicks the language switcher
- **THEN** they are taken to the Dutch Rules page (`/nl/rules/`)

#### Scenario: Language switcher is visible on all pages
- **WHEN** any page is rendered
- **THEN** the language switcher pill is present in the navigation bar

---

### Requirement: Navigation structure
The site SHALL have a top navigation bar containing the site logo (reticle SVG + "THE PREY" in Special Elite), links to all main sections, and the language switcher. Navigation links SHALL be uppercase PT Mono with `letter-spacing: 0.6px` and highlight with signal green (`#64FF00`) on hover, per the style guide.

#### Scenario: Navigation renders on all pages
- **WHEN** any page is rendered
- **THEN** the top navigation contains links to: Home, Rules, Roles, Playfield, FAQ, How-To

#### Scenario: Active page is highlighted
- **WHEN** a visitor is on a given page
- **THEN** the corresponding navigation link has a signal-green left border or underline accent

---

### Requirement: Home page
The site SHALL have a home page (`content/{lang}/_index.md`) that presents a hero section styled per the style guide's `.hero` component — dark void background, corner bracket SVG decorations, large Special Elite display heading, and a brief description of the game. The page SHALL include a summary of what the site offers and calls-to-action linking to Rules and How-To.

#### Scenario: Hero section renders correctly
- **WHEN** the home page is loaded
- **THEN** a hero block is visible with a large heading, corner brackets, and at least one primary CTA button

#### Scenario: Home page has intro copy
- **WHEN** the home page is loaded
- **THEN** a short description of The Prey (GPS hide-and-seek) is visible in both English and Dutch versions

---

### Requirement: Rules page
The site SHALL have a Rules page (`content/{lang}/rules/_index.md`) that covers: game concept, timing phases (head start, active hunt, final stretch, total duration), GPS update schedule, elimination rules, and win conditions. Content SHALL be sourced from the game design documentation.

#### Scenario: Rules page lists all timing phases
- **WHEN** the Rules page is loaded
- **THEN** the page displays the four timing phases: Head Start (10 min), Active Hunt (~45 min), Final Stretch (last 5 min), Total (60 min)

#### Scenario: Rules page covers win conditions
- **WHEN** the Rules page is loaded
- **THEN** the page states both win conditions: hunters win if all preys are tagged before time; preys win if at least one prey survives

#### Scenario: Rules page covers elimination
- **WHEN** the Rules page is loaded
- **THEN** the page explains that elimination occurs via physical tagging confirmed in the app

---

### Requirement: Player roles pages
The site SHALL have a Roles section with a landing page and two sub-pages — one for the Prey role and one for the Hunter role. Each role page SHALL cover: goal, responsibilities, in-app experience during the game, and any special permissions or behaviors. A Game Creator sub-page SHALL also exist covering pre-game setup responsibilities.

#### Scenario: Prey role page covers head-start behavior
- **WHEN** the Prey role page is loaded
- **THEN** the page describes the 10-minute head start: scatter and hide, no location shared

#### Scenario: Hunter role page covers GPS tracking
- **WHEN** the Hunter role page is loaded
- **THEN** the page explains that prey locations arrive as GPS pins on the map after the head start ends

#### Scenario: Game Creator page covers role assignment
- **WHEN** the Game Creator page is loaded
- **THEN** the page lists the four pre-game steps: define playfield, share code, assign roles, start game

---

### Requirement: Playfield page
The site SHALL have a Playfield page (`content/{lang}/playfield/_index.md`) explaining what a playfield is, how to create one (vertex drawing gestures), how to save and reuse playfields, and how to start a game from a playfield. The page SHOULD include a table of map interaction gestures.

#### Scenario: Playfield page lists creation steps
- **WHEN** the Playfield page is loaded
- **THEN** the page lists the five creation steps in order: open New Playfield, map centers on GPS, place vertices, confirm polygon, name and save

#### Scenario: Gesture table is present
- **WHEN** the Playfield page is loaded
- **THEN** a table of map gestures is visible: Tap → place vertex, Drag vertex → reposition, Pinch → zoom, Long press edge → insert vertex, Tap vertex → remove

---

### Requirement: FAQ page
The site SHALL have a FAQ page (`content/{lang}/faq/_index.md`) with at least ten question-and-answer entries covering common player questions. Each Q&A SHALL be implemented as an HTML details/summary or a styled accordion component. FAQ content SHALL be written using the voice-and-tone guidelines: clipped, present-tense, second-person.

#### Scenario: FAQ items are individually expandable
- **WHEN** a visitor clicks a FAQ question
- **THEN** the answer expands in place without navigating away from the page

#### Scenario: FAQ content uses correct voice
- **WHEN** the FAQ page is loaded
- **THEN** answers are written in clipped, present-tense, second-person style (e.g. "You are hidden. Keep moving.") — not "Oops!" or "You might want to consider…"

---

### Requirement: How-To guides
The site SHALL have a How-To section (`content/{lang}/how-to/`) with individual guide pages for: joining a game, creating a game, drawing a playfield, understanding your role mid-game, and what to do if GPS signal is lost. Each guide SHALL use numbered steps and follow the voice-and-tone guidelines.

#### Scenario: How-To index page lists all guides
- **WHEN** the How-To index is loaded
- **THEN** links to all individual guide pages are visible

#### Scenario: Individual guide uses numbered steps
- **WHEN** any How-To guide page is loaded
- **THEN** the guide content is structured as an ordered list of numbered steps

---

### Requirement: Design system conformance
The site SHALL implement the design language defined in `designs/the-prey-style-guide.html` faithfully. This includes:
- Background: `#181B17` (Base), with radial gradient atmosphere on `<body>`
- Scanline overlay via `body::before` repeating-linear-gradient (opacity 0.5)
- Grain texture via `body::after` SVG noise (opacity 0.04)
- Signal green: `#64FF00` for primary actions, headings accent, and live data
- Hunter red: `#FF2F1F` for threat/warning elements only
- Fonts: Special Elite for all headings and numeric readouts; PT Mono for all body text and data
- Border radius: `3px` for cards and buttons; `999px` for status pills
- All button labels MUST be uppercase Special Elite
- Labels MUST be uppercase PT Mono with `letter-spacing: 2px`

#### Scenario: Body background matches design system
- **WHEN** any page is rendered
- **THEN** the `<body>` background color is `#181B17` with the radial green and teal gradient overlays

#### Scenario: Headings use Special Elite font
- **WHEN** any page is rendered
- **THEN** all `h1`–`h3` elements use the Special Elite typeface

#### Scenario: Signal green is reserved for actionable elements
- **WHEN** any page is rendered
- **THEN** `#64FF00` appears only on primary CTA buttons, active navigation links, and key data values — not used for decorative purposes

#### Scenario: Hunter red is used correctly
- **WHEN** any page references hunter-role content or warnings
- **THEN** the hunter red `#FF2F1F` is used for those elements

---

### Requirement: Responsive layout
The site SHALL be readable and usable on mobile viewports (≥ 320px wide) and desktop viewports. Below 880px the navigation SHALL collapse to a vertical stack, matching the responsive breakpoint in the style guide.

#### Scenario: Mobile navigation stacks vertically
- **WHEN** the viewport is narrower than 880px
- **THEN** the navigation sidebar collapses and appears as a top bar or hamburger menu

#### Scenario: Body copy width is constrained on wide screens
- **WHEN** the viewport is wider than 1200px
- **THEN** the main content area is capped at a maximum width and centered, preventing overly long line lengths

---

### Requirement: Translation-pending banner
Pages that have not yet been translated SHALL display a banner informing the visitor that the page is available in English only. This is controlled by a `translationPending: true` front matter flag.

#### Scenario: Banner appears on untranslated pages
- **WHEN** a page has `translationPending: true` in front matter
- **THEN** an alert banner is rendered at the top of the page using the `alert.warn` style from the design system, stating the page is not yet translated

#### Scenario: Banner does not appear on translated pages
- **WHEN** a page does not have `translationPending: true`
- **THEN** no translation-pending banner is rendered

---

### Requirement: GitHub Actions deployment pipeline
The repository SHALL include a GitHub Actions workflow at `.github/workflows/deploy-website.yml` that triggers on pushes to `main` affecting files under `website/`. The workflow SHALL install a pinned version of Hugo, build the site, and deploy the `public/` output to GitHub Pages.

#### Scenario: Workflow triggers on website changes
- **WHEN** a commit is pushed to `main` with changes under `website/`
- **THEN** the GitHub Actions workflow runs automatically

#### Scenario: Build step uses pinned Hugo version
- **WHEN** the workflow runs
- **THEN** it installs the Hugo version specified in `website/.hugo-version`

#### Scenario: Site is published to GitHub Pages
- **WHEN** the build step completes without errors
- **THEN** the `public/` directory is deployed and the site is accessible at the configured GitHub Pages URL
