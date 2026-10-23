# Proposal

## Why

While a game is active, the Android app will run a foreground service that tracks the player's precise GPS position, including while the screen is off or another app is in front. Google Play's User Data and Location policies require a prominent in-app disclosure, shown before the runtime permission prompt, that explains what location data is collected, when, and why. Without that disclosure the app can be rejected or removed from the Play Store. The app has no permission flow yet (the main menu only reads location if permission happens to be granted), so this is the point to add one, before the tracking service lands.

## What Changes

- After a player signs in for the first time on a device, the app shows a full-screen modal dialog that explains the location tracking: precise location is used during an active game, it keeps being collected while the app is in the background or the screen is off, a persistent notification is shown while tracking runs, tracking stops when the game ends, and why the game needs it.
- The dialog has two actions: **Continue** opens the system location permission prompt (precise location), and on Android 13+ the notification permission prompt as well. **Not now** closes the dialog without asking for permissions.
- The app stores that the disclosure was shown on this device and does not show it again after later sign-ins, unless location permission is missing again (declined or revoked). In that case the disclosure appears again before the app asks for the permission.
- The system location permission is never requested before the disclosure has been accepted. The main menu position readout only starts once permission has been granted through this flow.
- Players who decline can still use the menus. Starting or joining a game later requires the permission, and the app shows the disclosure again at that point. That gate is specified here and wired in once the game screens exist.
- The disclosure has English and Dutch text, and the same wording goes into the Play Console declaration and the privacy page on the website.

## Capabilities

### New Capabilities
- `location-tracking-consent`: Prominent disclosure and runtime permission flow for in-game location tracking on the Android app. Covers when the disclosure is shown, what it must say, how consent and refusal are handled, and how it gates location access.

### Modified Capabilities
<!-- None: no existing spec covers client-side permissions or disclosure. -->

## Impact

- **Android app (`app/src`)**: new consent screen/dialog composable and view model, a device-local preference for "disclosure shown", changes to `ThePreyNavHost` so signed-in players see the disclosure before the main menu, and the main menu location readout gated on the permission granted through this flow.
- **Manifest**: `POST_NOTIFICATIONS` permission added (needed for the tracking notification on Android 13+). The foreground service declarations (`FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, the `<service>` element) and any `ACCESS_BACKGROUND_LOCATION` request are added by the separate change that builds the background tracking service.
- **Strings**: new English and Dutch disclosure strings (`the-prey-multilingual` skill).
- **Website**: the privacy page (theprey.nl) describes in-game location tracking in English and Dutch, consistent with the in-app disclosure.
- **Play Console**: the Data safety form and location permission declaration have to match the disclosure.
- **Backend**: none.
