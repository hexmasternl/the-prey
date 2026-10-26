# Proposal

## Why

theprey.nl still describes and shows the removed Ionic / .NET MAUI clients: screenshots of old screens, a "Join Game" button with code entry, a "Confirm Polygon" step, "Start Game" from a playfield, honour-based tagging, wrong default timings, and the v1 look (old logo, scanlines, heavy glow). Players who read the site before installing the Kotlin Android app get instructions that no longer match what they see. The invitation page (`/games/join/…`) is already correct and stays as it is.

## What Changes

- **Design system v2.0 on the website**: the v2 fox-in-crosshairs mark replaces the old reticle in the nav and the traced favicon; a raster share image from the v2 assets replaces `favicon.svg` as the Open Graph image; the scanline overlay goes; CSS moves to v2 primitive + semantic tokens, the 4 pt spacing scale, the 4–20 px radius scale and the three elevation levels; glow only on live, hover and pressed states; 48 px touch targets; labels in Text Soft instead of Ghost.
- **Content rewritten to match the current app**, in English and Dutch:
  - Home: phases and defaults from the app (30 min game, 5 min hunter delay, 10 min final stage, 3 min / 1 min location pings).
  - Rules: the five game settings and their options, location pings, penalties (hunter moving during the delay; boundary penalties), server-checked tagging within 50 m, silent players being ruled out, the result per role.
  - Roles: Prey and Hunter describe their HUDs; Game Creator covers Play Now, playfield choice, settings, sharing the invitation link, choosing the hunter, players marking themselves ready, and cancelling by leaving.
  - Playfield: Private / Public tabs, public search, create from the Private tab, the details screen (name, Public with the naming rule, Playfield Area), the area editor (tap to add, select and drag, delete selected point, Erase All, Save), and no more "Start Game" from a playfield.
  - How-To: every guide rewritten (create a game, join via invitation link, draw a playfield, understand your role through the HUD, location lost); a new **Get Started** guide (install, sign in, callsign, language, location "Allow all the time").
  - FAQ: answers corrected (joining, lobby size 2–16, location permissions, tagging range, leaving the playfield, spectators, Android only).
- **New screenshots** of the Kotlin app replace the six v1 images, with English and Dutch captures picked by page language.
- Unchanged: the `/games/join/*` invitation page, `assetlinks.json`, privacy and account-removal pages (already describe the Android app), Static Web App config and Bicep.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `game-info-site`: navigation, home, rules, roles, playfield, FAQ and how-to requirements change to describe the native Android app; design-system conformance moves to v2.0; adds requirements for current-app screenshots per language and the Get Started guide. (Capability introduced by the in-flight `static-game-info-website` change.)

## Impact

- **Website** (`website/src`) only: `assets/css/main.css`, `layouts/partials/nav.html`, `layouts/_default/baseof.html`, `layouts/index.html`, `layouts/shortcodes/screenshot.html`, `static/favicon.svg` (+ new share image), `static/images/screens/` (replaced), all `content/en/**` and `content/nl/**` game pages, `i18n/en.yaml` / `i18n/nl.yaml`.
- Screenshots need the Android app running on an emulator against a backend (Aspire locally) to capture real screens.
- No app, backend, API or infrastructure changes. Content describing the delete-point button and the first-time tours assumes `app-playfield-edit-area-allow-delete` ships first.
