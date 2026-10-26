# game-info-site Specification

## Purpose
TBD - created by archiving change static-game-info-website. Update Purpose after archive.

## Requirements

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
The site SHALL have a top navigation bar containing the v2 logo mark (fox in the crosshairs, from `designs/logo/the-prey-mark.svg`) with "THE PREY" in Special Elite, links to all main sections, and the language switcher. Navigation links SHALL be uppercase PT Mono and highlight with signal green (`#64FF00`) on hover, per design system v2.0.

#### Scenario: Navigation renders on all pages
- **WHEN** any page is rendered
- **THEN** the top navigation contains links to: Home, Rules, Roles, Playfield, FAQ, How-To

#### Scenario: Active page is highlighted
- **WHEN** a visitor is on a given page
- **THEN** the corresponding navigation link has a signal-green left border or underline accent

#### Scenario: Nav shows the v2 mark
- **WHEN** any page is rendered
- **THEN** the nav logo is the v2 fox-in-crosshairs mark, not the v1 reticle icon

---

### Requirement: Home page
The site SHALL have a home page (`content/{lang}/_index.md`) with a hero in the design-system v2.0 style, a short description of The Prey as a GPS hide-and-seek game for Android, the game phases with the app's default durations, the roles, and calls to action linking to Rules and How-To.

#### Scenario: Hero section renders correctly
- **WHEN** the home page is loaded
- **THEN** a hero block is visible with a large heading and at least one primary CTA button

#### Scenario: Home page has intro copy
- **WHEN** the home page is loaded
- **THEN** a short description of The Prey (GPS hide-and-seek) is visible in both English and Dutch versions

#### Scenario: Phases use the app defaults
- **WHEN** the home page is loaded
- **THEN** the phases read: Head Start 5 min, Active Hunt about 15 min, Final Stage 10 min, for a 30-minute game

---

### Requirement: Rules page
The site SHALL have a Rules page (`content/{lang}/rules/_index.md`) that covers: game concept, the five game settings with their options and defaults, location pings, penalties, tagging, players going silent, and win conditions, all as the current Android app and server apply them.

#### Scenario: Rules page lists the game settings
- **WHEN** the Rules page is loaded
- **THEN** it lists Game duration (30, 60, 90 min; default 30), Hunter delay (3, 5, 10; default 5), Final stage (3, 5, 10; default 10), Location ping (2, 3, 5; default 3) and Ping in final stage (1, 2, 3; default 1, never longer than the location ping)

#### Scenario: Rules page covers win conditions
- **WHEN** the Rules page is loaded
- **THEN** the page states both win conditions: the hunter wins if every prey is caught before time runs out; the preys win if at least one prey is still in play when time runs out

#### Scenario: Rules page covers tagging
- **WHEN** the Rules page is loaded
- **THEN** the page explains that the hunter tags from a list of preys within 50 m that the server checks, chooses one and confirms, and that a tagged prey watches the rest of the game as a spectator

#### Scenario: Rules page covers penalties
- **WHEN** the Rules page is loaded
- **THEN** it explains that a hunter who moves more than 25 m during the hunter delay gets a 10-minute penalty, that leaving the playfield can give a 5-minute boundary penalty when boundary penalties are on for the game, and that a penalised player's position is shared far more often while the penalty lasts

#### Scenario: Rules page covers silent players
- **WHEN** the Rules page is loaded
- **THEN** it explains that a player whose location stops reaching the server is seen as silent and can be ruled out, which counts as caught for a prey

#### Scenario: No honour-based tagging
- **WHEN** any page describes tagging
- **THEN** it does not call tagging honour-based and does not say the hunter can tag a prey at any distance

---

### Requirement: Player roles pages
The site SHALL have a Roles section with a landing page and sub-pages for Prey, Hunter and Game Creator. The Prey and Hunter pages SHALL cover goal, the hunter delay, what the HUD shows, and how tagging works for that role. The Game Creator page SHALL cover setting up a game in the app.

#### Scenario: Prey role page covers the HUD
- **WHEN** the Prey role page is loaded
- **THEN** it explains the role pill and penalty flash, the hunter proximity bands (Safe 250 m or more, Close 100–250 m, Warning under 100 m, Unknown), time left, hunter distance and the next-broadcast countdown

#### Scenario: Hunter role page covers the HUD and tagging
- **WHEN** the Hunter role page is loaded
- **THEN** it explains the game-clock pill, the four tiles (next ping, preys in play, closest prey, penalised preys), that prey markers show last known locations, and that the Tag button is locked during the hunter delay and lists only preys within 50 m

#### Scenario: Game Creator page covers setup
- **WHEN** the Game Creator page is loaded
- **THEN** it lists the steps: tap Play Now, choose a playfield, set the game settings, share the invitation, tap a player to make them the hunter, wait until everyone is ready, start the game; and it says that leaving the lobby as creator cancels the game

---

### Requirement: Playfield page
The site SHALL have a Playfield page (`content/{lang}/playfield/_index.md`) explaining what a playfield is, the Private and Public tabs, how to create one, how the area editor works, how to make a playfield public, and that a playfield is chosen when creating a game.

#### Scenario: Playfield page lists creation steps
- **WHEN** the Playfield page is loaded
- **THEN** the page lists the creation steps in order: open Playfields, tap + on the Private tab, enter a name, open Playfield Area, draw the area, Save the area, Save the playfield

#### Scenario: Area editor table is present
- **WHEN** the Playfield page is loaded
- **THEN** a table of area-editor actions is visible: Tap map → add a point; Tap point → select it (red border); Drag selected point → move it; Tap empty map → deselect; Delete button → remove the selected point; Erase All → remove every point; Pinch / drag map → zoom / pan

#### Scenario: No outdated steps
- **WHEN** the Playfield page is loaded
- **THEN** it does not mention "Confirm Polygon", a "New Playfield" button, long-press to insert a point, or starting a game from a playfield (the app's "New playfield" screen title may be named)

#### Scenario: Public playfield naming
- **WHEN** the Playfield page is loaded
- **THEN** it explains that Public needs a name like "CC, City, Fieldname" and that public playfields can be found by searching the Public tab with at least 3 characters

---

### Requirement: FAQ page
The site SHALL have a FAQ page (`content/{lang}/faq/_index.md`) with at least ten question-and-answer entries whose answers match the current Android app. Each Q&A SHALL be implemented as an HTML details/summary or a styled accordion component. FAQ content SHALL be written using the voice-and-tone guidelines: clipped, present-tense, second-person.

#### Scenario: FAQ items are individually expandable
- **WHEN** a visitor clicks a FAQ question
- **THEN** the answer expands in place without navigating away from the page

#### Scenario: FAQ content uses correct voice
- **WHEN** the FAQ page is loaded
- **THEN** answers are written in clipped, present-tense, second-person style (e.g. "You are hidden. Keep moving.") — not "Oops!" or "You might want to consider…"

#### Scenario: FAQ answers match the app
- **WHEN** the FAQ page is loaded
- **THEN** it says a game needs 2 to 16 players, joining happens by tapping the invitation link, the app needs precise location set to "Allow all the time", tagging works only within 50 m, and the app is Android only

---

### Requirement: How-To guides
The site SHALL have a How-To section (`content/{lang}/how-to/`) with guide pages for: getting started, joining a game, creating a game, drawing a playfield, understanding your role mid-game, and what to do when your location is lost. Each guide SHALL use numbered steps that match the screens and labels of the current Android app and follow the voice-and-tone guidelines.

#### Scenario: How-To index page lists all guides
- **WHEN** the How-To index is loaded
- **THEN** links to all six guide pages are visible

#### Scenario: Individual guide uses numbered steps
- **WHEN** any How-To guide page is loaded
- **THEN** the guide content is structured as an ordered list of numbered steps

#### Scenario: Get Started guide
- **WHEN** the Get Started guide is loaded
- **THEN** it covers installing from Google Play, signing in, setting a callsign and language in Settings, and allowing precise location "All the time" plus notifications

#### Scenario: Join guide uses the invitation link
- **WHEN** the Join a Game guide is loaded
- **THEN** it explains joining by tapping the invitation link the creator shared, signing in first if needed, landing in the lobby and tapping Set ready; it does not mention a Join Game button or typing a code

#### Scenario: Labels match the app
- **WHEN** a guide names a button or screen
- **THEN** the name is exactly the label the app shows in that language, taken from the app's `strings.xml` (English) or `values-nl/strings.xml` (Dutch)

---

### Requirement: Design system conformance
The site SHALL implement design system v2.0 (`designs/the-prey-design-system.html`): the same palette and fonts as v1, v2 primitive and semantic tokens, a 4 pt spacing scale, the 4–20 px radius scale with 999 px pills, three elevation levels, no scanline overlay, glow only on live, hover and pressed states, 48 px touch targets, and labels in Text Soft.

#### Scenario: Body background matches design system
- **WHEN** any page is rendered
- **THEN** the `<body>` background color is `#181B17` (Base)

#### Scenario: No scanlines
- **WHEN** any page is rendered
- **THEN** no scanline overlay is drawn over the page

#### Scenario: Headings use Special Elite font
- **WHEN** any page is rendered
- **THEN** all `h1`–`h3` elements use the Special Elite typeface and body text uses PT Mono

#### Scenario: Signal green is reserved for actionable elements
- **WHEN** any page is rendered
- **THEN** `#64FF00` appears only on primary CTA buttons, active navigation links, and key data values — not used for decorative purposes

#### Scenario: Hunter red is used correctly
- **WHEN** any page references hunter-role content or warnings
- **THEN** the hunter red `#FF2F1F` is used for those elements

#### Scenario: Touch targets
- **WHEN** the site is viewed on a phone
- **THEN** buttons, nav links and the language switcher are at least 48 px tall

#### Scenario: v2 brand assets
- **WHEN** a page is shared or opened in a browser tab
- **THEN** the favicon is the v2 mark and the Open Graph image is a raster image of the v2 mark or lockup

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

### Requirement: Screenshots show the current Android app in the page language
Every screenshot on the site SHALL be a capture of the current Kotlin Android app. English pages SHALL show English captures and Dutch pages Dutch captures. Screenshots of the removed Ionic and MAUI clients SHALL NOT appear on the site.

#### Scenario: Dutch page shows Dutch screenshot
- **WHEN** the Dutch Draw a Playfield guide is loaded
- **THEN** its screenshot shows the area editor with Dutch labels ("Annuleren", "Alles wissen", "Opslaan")

#### Scenario: Old screenshots removed
- **WHEN** the site is built
- **THEN** none of the v1 images (`create-new-operation.png`, `in-play-hunter-screen.png`, `player-settings.png` and the other v1 captures) are in the output

#### Scenario: Screenshot alt text
- **WHEN** a screenshot is rendered
- **THEN** it has alt text and a caption in the page's language describing the screen shown
