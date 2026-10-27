# Design

## Context

`LocationConsentViewModel` drives the disclosure flow; `LocationConsentHost` feeds it permission checks (on every `ON_RESUME`) and prompt results, and performs its effects. Today the flow knows only about fine and coarse location: `granted` is the fine grant, and the flow closes (`open = false`) as soon as fine is granted. Background location is read only by `readLocationAccess` (`LocationAccess.ForegroundOnly` vs `Granted`), which drives the main menu's `PlayAction` and the Settings screen's `GrantStep`. Neither is part of the flow.

`LocationDisclosureDialog` has two variants, `Disclosure` (Continue → system prompt) and `OpenSettings` (location blocked or approximate only → app settings). The host maps `ConsentUiState` onto them.

Constraints: `ACCESS_BACKGROUND_LOCATION` is never requested at runtime (`the-prey-play-store`). The disclosure must precede any prompt, and the step itself isn't a prompt. Below API 29 a fine grant already includes background.

## Goals / Non-Goals

**Goals:**
- A player who approves location in the flow is guided straight to "Allow all the time".
- The flow ends cleanly whether they set it, come back without it, or tap Not now.

**Non-Goals:**
- Changing what the play button requires (`PlayAction`) or the main-menu error.
- Changing the Settings screen's Grant location access action, which already sends players to settings for "all the time".
- Requesting background location through a system prompt, or dropping the "Allow all the time" requirement.

## Decisions

### 1. A third consent state, `ConsentUiState.AllTheTime`

The view model gets an `allTheTimeStep` flag, set in `onPermissionResult` when `fineGranted && !backgroundGranted && sdkInt >= 29` while the flow is open. `refresh()` maps it to `ConsentUiState.AllTheTime` before the existing rules, and `open` stays true while it is set. The host maps it to a new `DisclosureVariant.AllTheTime`, whose primary button calls `onOpenSettings()`.

*Alternative:* reuse the `OpenSettings` variant with other text. Rejected because its close rules differ: `OpenSettings` stays until fine is granted, while the new step must close on any return.

### 2. Background grant is an input, not a derived value

`onPermissionResult` and `onPermissionChecked` take a `backgroundGranted` argument (default `true`, so existing callers and tests keep their meaning). The host computes it as `readLocationAccess(context) == LocationAccess.Granted`, which already handles API < 29. The view model still touches no Android API.

### 3. Close on the return from settings, not on any resume

The step must survive the resumes caused by the system prompt closing and by the notification prompt that follows it on Android 13+. `onOpenSettings()` sets `sentToSettings = true` when the step is showing. The next `onPermissionChecked` closes the step if background is granted, or if `sentToSettings` is set (returned without it), and then clears both flags. A resume that reports background granted closes the step whatever caused it.

### 4. Pending game actions keep running on the precise grant

`runPendingAction()` still runs in `onPermissionResult` on a fine grant, before the step is shown. A start or join therefore behaves as today, and the step is shown above whatever screen follows: the host overlays all signed-in content. Making the action wait for "all the time" would change what joining requires, and that is out of scope.

### 5. One-shot by construction

The step is only set inside `onPermissionResult`, which only follows `onContinue()` in an open flow. A later resume with `ForegroundOnly` never sets it, so the "never by itself" rule needs no extra stored state. `onNotNow()` and sign-out clear the flag.

### 6. Texts

New strings `location_consent_all_the_time_title`, `location_consent_all_the_time_body` and `location_consent_all_the_time_steps` (Permissions → Location → Allow all the time) in EN + NL. The buttons reuse `location_consent_settings_open` and `location_consent_not_now`. The body repeats why (tracking continues with the screen off or the app in the background), consistent with `location_consent_point_background`.

## Risks / Trade-offs

- **Settings labels differ per OEM and Android version.** The steps text names the stock labels; Settings → Location may read differently on some phones. Mitigation: keep the text short and name the end state ("Allow all the time"), as the Settings screen already does.
- **Two dialogs in a row** (disclosure, then the step) plus the notification prompt on Android 13+. It's the minimum for the first run, and later visits never repeat it.
- **Returning without granting closes the step silently.** The main-menu error with Open settings shows right after, so the way forward stays visible.
