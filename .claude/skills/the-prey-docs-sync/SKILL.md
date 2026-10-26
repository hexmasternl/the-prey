---
name: the-prey-docs-sync
description: Keeps the public website (theprey.nl) and the repo documentation (docs/) in step with functional changes to the Android app or the backend. Use whenever a change alters what a player sees or can do, or how the game behaves — new or changed screens, buttons, settings, game rules, defaults, timings, distances, penalties, permissions, location/privacy behaviour, endpoints, realtime events, architecture or deployment — and when proposing, applying or archiving an OpenSpec change (opsx:propose, opsx:apply, opsx:archive). A functional change is not done until the website and docs say the same thing as the code.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Keep Website & Docs in Sync

The website and `docs/` describe what the app and server **actually do**. When a functional change ships without them, players read wrong instructions and future agents read wrong specs. So every functional change carries its documentation with it, **in the same change** (same branch, same PR, same OpenSpec change).

## The one rule

**If the change alters behaviour that a player or a developer can observe, update every place that describes it before the change is done.** No "docs later" follow-ups. If a part truly can't be written yet (for example a screenshot that needs a real device), say so in your summary and leave a tracked task in the OpenSpec `tasks.md` — never silently skip it.

Pure refactors, internal renames, test-only changes, dependency bumps and CI tweaks with no visible effect need no website update. They may still need a `docs/architecture` update if the structure changed.

---

## What to update — impact map

Find the row(s) that match your change and update every target listed.

| Change in the code | Website (`website/src/content/{en,nl}/`) | Docs (`docs/`) |
|---|---|---|
| Game rule, default, limit, timing, distance, penalty, end condition (`server/src/Games/.../DomainModels/Game.cs`, `GameParticipant`, configuration) | `rules/_index.md`, `faq/_index.md`, `roles/*.md`, affected `how-to/*.md` | `game-design/game-mechanics.md`, `game-design/player-roles.md` |
| Role behaviour (Hunter / Prey / Game Creator abilities, views, HUD) | `roles/*.md`, `how-to/understand-your-role.md`, `rules/_index.md` | `game-design/player-roles.md` |
| Lobby, create/join flow, invitations, deep links | `how-to/create-a-game.md`, `how-to/join-a-game.md`, `faq/_index.md` | `game-design/game-mechanics.md` |
| Playfield editing, rules, visibility, search | `playfield/_index.md`, `how-to/draw-a-playfield.md` | `game-design/playfield.md` |
| Main menu, settings, sign-in, first-run flow | `how-to/get-started.md`, `faq/_index.md` | — |
| Location permission, tracking, background service, signal loss, data sent to the server | `how-to/gps-signal-lost.md`, `faq/_index.md`, **`privacy.md`** | `play-store/location-disclosure.md` (and see `the-prey-play-store`) |
| Account data, deletion, what's stored | **`privacy.md`**, `account-removal.md` | — |
| Install / test programme / supported platforms | `become-a-tester.md`, `_index.md`, `faq/_index.md` | `README.md` |
| REST endpoint, DTO, status or error code | — | `api/endpoints.md` (see `the-prey-api-contract-sync`) |
| Realtime event, envelope, delivery rules | — | `api/realtime.md` (see `the-prey-realtime-communication`) |
| New module, service, data store, messaging, hosting | — | `architecture/overview.md`, `architecture/server.md`, `README.md` |
| Azure resources, deploy pipeline, environments | — | `deployment/azure-deployment.md` (see `the-prey-azure-infra`) |
| UI change on a screen that has a screenshot | Recapture `website/src/static/images/screens/{en,nl}/<name>.webp` | — |

If none of the existing pages fits a new feature, add a page (usually under `how-to/`) and follow `the-prey-website-content` for structure and front matter.

To find every page that mentions something you changed, search both content trees and the docs for the old value or term:

```bash
# e.g. you changed the tag distance or the head-start default
grep -rniE "50 ?m|meter|head start|voorsprong" website/src/content docs
```

---

## How to write the update

- **Code is the source of truth.** Read the value from the code (constant, default, string resource), not from older docs. Quote UI labels exactly as they appear in `res/values/strings.xml` and `res/values-nl/strings.xml`, bold in Markdown (`**Play now**`).
- **Trace every claim.** `openspec/changes/website-update-to-kotlin-app/facts.md` is the fact sheet behind the website copy: each claim points to the file or string it comes from. When you change a fact, update its row (or add one) there too while that change is active; once archived, keep the same discipline in your summary — name the source of every number you write on the site.
- **Both languages, always.** Every website edit lands in `content/en/` **and** `content/nl/` with the same filename, front matter translated too (`title`, `description`, `keywords`). Follow `the-prey-multilingual` for the glossary and the je/jij voice.
- **Update SEO fields when the meaning changes**: a page's `description` and `keywords` often repeat the number or feature you just changed (the FAQ description mentions "tagging within 50 meters").
- **Screenshots**: the `screenshot` shortcode loads `static/images/screens/{lang}/<name>.webp`. If the UI changed, recapture in both app languages; if you cannot, list the stale screenshot names in your summary. A missing file gives a `hugo` build warning, which must be fixed before a release.
- **docs/ is for developers**: describe behaviour and contracts precisely (values, states, endpoints). Fix stale statements you come across in the section you touch, e.g. `docs/README.md` still calls realtime "SignalR / push notifications" while the system uses Azure Web PubSub only.
- **Tone on the website** follows `the-prey-design-system`: short, calm, imperative. No marketing claims the app can't back up, and never describe features that haven't shipped.

---

## OpenSpec integration

When you **propose** a change (`opsx:propose`), add a "Documentation" section to `tasks.md` that lists the website pages (EN + NL) and `docs/` files from the impact map, e.g.:

```markdown
## 5. Documentation
- [ ] 5.1 Update rules/_index.md (en + nl): tag distance 50 m → 30 m
- [ ] 5.2 Update faq/_index.md (en + nl) answer and front-matter description
- [ ] 5.3 Update docs/game-design/game-mechanics.md
- [ ] 5.4 Recapture screenshot `in-play-hunter-screen` (en + nl)
```

When you **apply** a change, do these tasks with the code, not after it. Before **archiving**, check that every documentation task is ticked; an unticked one blocks archiving unless the user agrees to defer it.

---

## Verify

```bash
cd website/src
# both languages still have the same pages (prints nothing)
diff <(cd content/en && find . -type f | sort) <(cd content/nl && find . -type f | sort)
# the old value is gone everywhere (adjust the pattern)
grep -rniE "<old value or term>" content ../../docs
# site builds; no "screenshot" or missing-i18n warnings
hugo --gc --minify
```

Then read the changed pages once as a player would: does following the steps on the site work in the current app?

---

## Checklist for any functional change

1. Looked up the change in the impact map and listed every affected website page and `docs/` file.
2. Searched `website/src/content` and `docs/` for the old value or term; no stale mentions left.
3. Website pages updated in **English and Dutch**, including `description`/`keywords` where the meaning changed.
4. `privacy.md` reviewed whenever data collection, location or account handling changed.
5. `docs/` updated: game design for rules, `api/` for contracts, `architecture/` and `deployment/` for structure.
6. Values and labels taken from the code and string resources, traceable to their source.
7. Screenshots recaptured, or the stale ones reported.
8. `hugo --gc --minify` runs clean; OpenSpec documentation tasks ticked.
9. Anything deferred is named in the summary to the user.
