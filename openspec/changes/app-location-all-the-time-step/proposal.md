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
- **Texts in English and Dutch**; the website's get-started steps and FAQ describe the guided step.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `location-tracking-consent`: a precise grant inside the disclosure flow is followed by an "Allow all the time" step on Android 10+, which opens the app's settings page and closes when the player returns with background location granted or declines.

## Impact

- **Android:** `ui/consent/LocationConsentViewModel.kt` (new `ConsentUiState.AllTheTime`, background grant in the permission checks), `ui/consent/LocationConsentHost.kt` (reads `ACCESS_BACKGROUND_LOCATION` on resume and after the prompt, maps the new state), `ui/consent/LocationDisclosureDialog.kt` (new `DisclosureVariant.AllTheTime` plus preview), `res/values/strings.xml` + `res/values-nl/strings.xml`. Tests: `LocationConsentViewModelTest`.
- **Unchanged:** `PlayAction.kt` and the main-menu error, the Settings screen's `GrantStep`, `AndroidManifest.xml`, the disclosure text, `docs/play-store/location-disclosure.md` and the Play Console answers.
- **Website:** `how-to/get-started.md` and `faq/_index.md` (EN + NL) mention that the app walks the player to "Allow all the time".
- **Server:** none.
