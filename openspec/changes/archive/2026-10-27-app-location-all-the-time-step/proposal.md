# Proposal

> GitHub issue: [#47](https://github.com/hexmasternl/the-prey/issues/47)

## Why

A player who approves location in the first-run flow on the main menu still can't play. Reported in #47. **Continue** in the disclosure requests only precise location (fine + coarse), which Android grants as "While using the app". **Play now** needs "Allow all the time", so straight after approving, the button is grey and the menu shows "Location is allowed only while the app is in use…". The flow the player just went through never mentions this second step.

The system prompt can't ask for "always" itself. From Android 11 no permission dialog offers "Allow all the time"; it is only on the app's settings page. The project also never requests `ACCESS_BACKGROUND_LOCATION` at runtime (`the-prey-play-store`). The fix is to guide the player to that setting inside the same flow.

## What Changes

- **New "Allow all the time" step in the consent flow.** On Android 10 and later, when a disclosure flow ends with precise location granted but without background location, the consent dialog continues with an **Allow all the time** step instead of closing. The step explains why the game needs it (tracking continues with the screen off or the app in the background), tells the player where to set it (Permissions → Location → Allow all the time) and offers **Open settings** and **Not now**.
- **Return from settings closes the step.** The resume check closes the step once "Allow all the time" is granted. Returning without it, or tapping **Not now**, closes it too. The main menu's existing background-location error with **Open settings** remains the fallback.
- **Same flow, same limits.** The step belongs to the flow that showed the disclosure: the one-time automatic flow on the main menu and every flow started by a game action. It never opens by itself on a later visit. A game action that is waiting for location keeps running on the precise grant, as today.
- **Android 9 and lower:** precise location already includes background, so there's no step.
- **No runtime request for `ACCESS_BACKGROUND_LOCATION`.** No manifest change and no Play Console change. The disclosure text already covers background collection.
- **Settings: Grant location access moves above the warning.** In the Location section the instruction text and the **Grant location access** button come directly below the status card, above the location tracking warning.
- **Settings: the warning becomes a collapsible panel.** Collapsed by default, it shows the orange warning triangle and "Why The Prey uses your location". Tapping it expands the panel to the full disclosure (body, the four points and the privacy link); tapping again collapses it. It stays visible in every permission state.
- **Settings: the disclosure dialog comes before the system prompt.** With the panel collapsed and below the button, it no longer counts as the disclosure before a prompt. So when Grant location access would show the system location prompt, it first shows the same disclosure dialog as the main menu, and only **Continue** leads to the prompt. When the action opens the system settings page instead, nothing changes: the app shows no prompt there.
- **Texts in English and Dutch**; the website's get-started steps and FAQ describe the guided step and the new Settings layout. `docs/play-store/location-disclosure.md` describes the Settings flow as it now works.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `location-tracking-consent`: a precise grant inside the disclosure flow is followed by an "Allow all the time" step on Android 10+, which opens the app's settings page and closes when the player returns with background location granted or declines.
- `app-settings`: the disclosure becomes a collapsible warning panel ("Why The Prey uses your location") below the Grant location access action, and the action shows the disclosure dialog before any system location prompt.

## Impact

- **Android:** `ui/consent/LocationConsentViewModel.kt` (new `ConsentUiState.AllTheTime`, background grant in the permission checks), `ui/consent/LocationConsentHost.kt` (reads `ACCESS_BACKGROUND_LOCATION` on resume and after the prompt, maps the new state), `ui/consent/LocationDisclosureDialog.kt` (new `DisclosureVariant.AllTheTime` plus preview), `res/values/strings.xml` + `res/values-nl/strings.xml`. Tests: `LocationConsentViewModelTest`.
- **Android, Settings:** `ui/settings/SettingsScreen.kt` (`LocationSection` order, collapsible `DisclosurePanel`, disclosure dialog before the `RequestPrecise` prompt, previews), new string `settings_location_why` (EN + NL).
- **Unchanged:** `PlayAction.kt` and the main-menu error, the Settings screen's `GrantStep`, `AndroidManifest.xml`, the disclosure text and the Play Console answers.
- **Docs:** `docs/play-store/location-disclosure.md`: the reference paragraph and reviewer step 4 (Settings shows the disclosure dialog before the prompt; the panel is collapsible).
- **Website:** `how-to/get-started.md` and `faq/_index.md` (EN + NL) mention that the app walks the player to "Allow all the time" and the Settings layout.
- **Server:** none.
