---
name: the-prey-multilingual
description: The Prey's multilingual (English + Dutch) rules for the Android app and the Hugo website. Use whenever adding, changing, renaming or removing any user-facing text — app strings, screens, error/notification messages, website pages, front matter, menus, footer, SEO descriptions/keywords, alt text, Play Store texts — and when reviewing such changes. Every change must land in BOTH English and Dutch in the same change.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Multilingual Support (EN + NL)

The Prey ships in **English (default)** and **Dutch**. This applies to both clients of the brand:

- the Android app — `app/src/app/src/main/res/`
- the website (theprey.nl) — `website/src/`

## The one rule

**No user-facing text changes in one language only.** Whenever you add, change, rename or remove text in English, make the matching Dutch change in the same commit, and the other way round. That includes small changes: typo fixes, punctuation, button labels, SEO keywords, alt text.

A change isn't done until both languages are complete and their keys and pages line up. If you really can't produce a good Dutch text (for example legal wording in `privacy.md`), write your best Dutch version anyway and say so in your summary so the user can review it. Never leave the English copy in the Dutch file without saying so.

---

## Android app

### Where text lives

| File | Language |
|---|---|
| `res/values/strings.xml` | English, the default and fallback |
| `res/values-nl/strings.xml` | Dutch |

Rules:
- **All user-facing text comes from string resources.** Read it with `stringResource(R.string.…)` in Compose, or `context.getString(…)` outside it. Never write literals in Kotlin, including `contentDescription`, snackbars, toasts, notification titles and dialog text.
- **Every translatable key in `values/` must exist in `values-nl/`**, with the same name and the same placeholders (`%1$s`, `%2$d`) in the same order. Dutch can reorder words, so always use positional placeholders (`%1$s`, not `%s`) when a string has more than one.
- Mark text that must never be translated with `translatable="false"` and keep it **only** in `values/`, never in `values-nl/`. That covers the brand name "The Prey", URLs and format-only strings. Lint flags `MissingTranslation` / `ExtraTranslation` otherwise.
- Write counts as `<plurals>` (`one` / `other`) in **both** files. Never build them with "if count == 1" in code.
- Never build sentences by joining strings in code. Make the whole sentence a resource with placeholders.
- Units and numbers: follow the design system's formatting rules (whole metres up to 999 m, then km with one decimal; timers `mm:ss`). For decimals, use the device locale (Dutch uses a comma: `8,8 km`), e.g. `String.format(Locale.getDefault(), …)` or a `NumberFormat`.
- Group keys per screen with a prefix: `lobby_start_hunt`, `hud_time_left`, `error_game_code_invalid`.
- Server errors: the API returns machine-readable codes or HTTP statuses. Map them to string resources in the app. Never show the server's English message text to the user.
- If you add an in-app language picker, or Android 13+ per-app language settings (`res/xml/locales_config.xml` with `en` and `nl` plus `android:localeConfig` in the manifest), list both locales.

### Example

```xml
<!-- res/values/strings.xml -->
<string name="hud_nearest_hunter">Nearest hunter</string>
<string name="lobby_players_ready">%1$d of %2$d players ready</string>
<plurals name="hud_prey_left">
    <item quantity="one">%d prey left</item>
    <item quantity="other">%d prey left</item>
</plurals>

<!-- res/values-nl/strings.xml -->
<string name="hud_nearest_hunter">Dichtstbijzijnde jager</string>
<string name="lobby_players_ready">%1$d van %2$d spelers klaar</string>
<plurals name="hud_prey_left">
    <item quantity="one">Nog %d prooi</item>
    <item quantity="other">Nog %d prooien</item>
</plurals>
```

### Verify

```powershell
# from app/src
./gradlew lintDebug   # fails or warns on MissingTranslation / ExtraTranslation
```

Also compare the keys by hand when you've touched strings: every translatable `name` in `values/strings.xml` should appear in `values-nl/strings.xml`, and nothing extra.

---

## Website (Hugo)

`website/src/hugo.toml` defines both languages: `en` is the default, `nl` is second, and both are served under a subfolder (`/en/…`, `/nl/…`). Content for each lives in its own `contentDir`.

### Where text lives

| What | English | Dutch |
|---|---|---|
| Page content and front matter | `content/en/<path>.md` | `content/nl/<path>.md` (**same path and filename**) |
| Template strings (nav, footer, banners, labels) | `i18n/en.yaml` | `i18n/nl.yaml` |

Rules:
- **Pages come in pairs.** A new page `content/en/how-to/foo.md` needs `content/nl/how-to/foo.md` with the **same filename**: Hugo links translations by path, and the language switcher depends on it. Don't translate the filename or slug. Renaming, moving or deleting a page means doing the same to its twin.
- **Translate the front matter too**: `title`, `description`, `keywords` (write real Dutch search terms, don't just translate word for word), every structured block (`hero`, `intro`, …) and `alt`/`caption` in shortcodes such as `screenshot`. Keep the **same keys and structure** in both files, and leave machine values (`type`, `weight`, image `src`, URLs) as they are.
- **No literal text in layouts.** Templates in `layouts/` get text from `{{ i18n "key" }}` and build internal links with `relLangURL`, e.g. `{{ "/rules/" | relLangURL }}`, so they stay in the current language. A new key goes into **both** `i18n/en.yaml` and `i18n/nl.yaml`, and both files must keep the same set of keys.
- **Temporary fallback only**: when a Dutch translation truly can't be written yet, create the NL page anyway with `translationPending: true` in its front matter. This shows the `translation-pending-banner` partial. Tell the user that you did. Remove the flag as soon as the page is translated, and treat this as the exception, never the default.

### Verify

```bash
cd website/src
# same page set in both languages (should print nothing)
diff <(cd content/en && find . -type f | sort) <(cd content/nl && find . -type f | sort)
# same i18n keys in both languages (should print nothing)
diff <(grep -oE '^[A-Za-z0-9_]+:' i18n/en.yaml | sort) <(grep -oE '^[A-Za-z0-9_]+:' i18n/nl.yaml | sort)
# pages still flagged as untranslated
grep -rl "translationPending: true" content/nl
hugo --gc --minify   # build must succeed; warnings about missing i18n keys must be fixed
```

---

## Dutch style guide

- **Informal "je/jij"**, never "u". This matches the existing site ("Jij bent de Jager…").
- Keep the design system's voice in both languages: short, imperative, calm like a radio operator. No exclamation marks, emoji or apologies. "Je bent verborgen. Blijf bewegen." not "Oeps! We kunnen je niet vinden 😅".
- Headings on the website follow the existing Title Case style in both languages ("Stap In Voordat de Jacht Live Gaat"). Labels and buttons in the app are UPPERCASE by styling, so write them in normal case in the resource.
- Translate the meaning, not word for word. Dutch is usually about 20–30% longer, so check that buttons, pills and HUD labels still fit, and use the shorter word if they don't.
- **"The Prey" is the brand name and is never translated.** The *role* is.

### Glossary (keep it consistent)

| English | Dutch | Notes |
|---|---|---|
| Hunter | Jager | Capitalised when it means the role |
| Prey (role) | Prooi | Plural/group: "Prooi" as a collective ("alle Prooi"), "prooien" when counting |
| Game Creator | Spelmaker | |
| Playfield | Speelveld | |
| Game | Spel | |
| Lobby | Lobby | |
| Tag / tagged | taggen / getagd | |
| Head start | Voorsprong | |
| Penalty | Straf | |
| Become a Tester | Word Tester | |
| Signal lost | Signaal kwijt | |

When you introduce a new game term, add it to this glossary in the same change, so the app and the website keep using the same Dutch word.

---

## Checklist for any change with user-facing text

1. English text changed → the matching Dutch text changed in the same change, and the other way round.
2. App: key exists in `values/` **and** `values-nl/` with the same placeholders; `translatable="false"` only for brand, URLs and format strings; no literals in Kotlin.
3. Website: page pair exists with the same filename; front matter translated with the same structure; new `i18n` keys in both YAML files; links use `relLangURL`.
4. Dutch follows the glossary and the informal, calm voice, and still fits the layout.
5. Verification commands above run clean (`lintDebug`; the website diff commands; `hugo` builds).
6. Any fallback (`translationPending`, an uncertain translation) is reported to the user.
