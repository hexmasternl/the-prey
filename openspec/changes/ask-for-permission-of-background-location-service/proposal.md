## Why

Both clients request the OS background-location permission the moment a game starts tracking, with no prior explanation — the MAUI app calls `Permissions.RequestAsync<LocationAlways>()` and the Ionic app calls `BackgroundGeolocation.addWatcher({ requestPermissions: true })` directly. Google Play's (and the App Store's) **Prominent Disclosure & Consent** policy requires that, before the runtime location prompt, the app prominently disclose that location is collected in the background — including when the app is closed — and why. Without an in-app disclosure the apps risk store rejection and give players no informed choice.

## What Changes

- Add a **prominent disclosure consent screen/dialog** that is shown **before** the OS location-permission prompt in both the MAUI app and the Ionic app.
- The disclosure SHALL clearly state that a background service will send the device's location to our server for the **duration of the game**, and require an explicit affirmative action ("Allow" / "Continue") before the OS permission prompt is triggered.
- If the user declines the disclosure, the OS permission is **not** requested and tracking is not started; the game degrades gracefully (no location broadcasting) instead of crashing or looping.
- Gate the existing permission-request paths behind this consent: MAUI (`AndroidBackgroundExecutionHost.EnsurePermissionsAsync` / the tracker coordinator) and Ionic (`GameLocationService.start`) only proceed to request OS permission after consent is given.
- All disclosure copy is localized (MAUI `AppResources.resx` EN + NL; Ionic `i18n` EN + NL) and styled through each app's single source of truth — no hard-coded, unlocalized text.

## Capabilities

### New Capabilities
- `background-location-consent`: A prominent in-app disclosure-and-consent gate, shown in both clients before the OS background-location permission is requested, explaining that a background service transmits location to the server for the game's duration and requiring explicit consent before any OS prompt or tracking begins.

### Modified Capabilities
<!-- none: the existing permission-request behaviour is unchanged in intent; this change inserts a consent gate before it. The permission-request requirement lives in the pending maui-background-location-service change and is not yet an archived spec, so it is not modified here. -->

## Impact

- **MAUI**:
  - `Platforms/Android/Location/AndroidBackgroundExecutionHost.cs` (and/or the tracker coordinator that starts tracking) — request OS permission only after consent.
  - A new consent surface (page or dialog service) + DI registration; localized copy in `Resources/Strings/AppResources.resx` and `AppResources.nl.resx`; styling via `Styles.xaml`.
  - A persisted "consent given" flag so returning players are not re-prompted every game (re-disclosure only if permission was revoked).
- **Ionic** (`src/ThePrey`):
  - `src/app/games/game-location.service.ts` — do not call `addWatcher` with `requestPermissions: true` until consent is captured; expose/await a consent step.
  - A consent modal/alert component + copy in `src/assets/i18n/en.json` and `nl.json`; callers (`game-hunter.page.ts`, `game-lobby.page.ts`, prey page) await consent before `start(...)`.
- **No backend, API, or realtime changes.** The location-reporting behaviour itself is unchanged; only an informed-consent gate is added ahead of the OS permission prompt.
