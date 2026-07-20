## ADDED Requirements

### Requirement: Prominent disclosure before requesting OS location permission

Before the app triggers the operating system's location-permission prompt for background tracking, the app SHALL present a prominent in-app disclosure. The disclosure SHALL clearly state that a background service will collect the device's location and send it to the game's server, that this happens for the duration of the game (including while the app is in the background or closed), and SHALL require an explicit affirmative user action to continue. The app SHALL NOT invoke the OS location-permission prompt or start location tracking until the user has given this consent. This requirement applies to both the MAUI app and the Ionic app.

#### Scenario: Disclosure precedes the OS prompt

- **WHEN** a game is about to start background location tracking and the player has not yet consented in the app
- **THEN** the app shows the prominent disclosure explaining background location collection and its purpose before any OS location-permission prompt appears

#### Scenario: Consent proceeds to the OS prompt

- **WHEN** the player takes the affirmative action ("Allow" / "Continue") on the disclosure
- **THEN** the app proceeds to request the OS location permission and, once granted, starts background tracking

#### Scenario: Applies to both clients

- **WHEN** either the MAUI app or the Ionic app is about to request background location permission
- **THEN** the same prominent disclosure-and-consent gate is shown first in that client

### Requirement: Declining the disclosure blocks tracking gracefully

If the player declines the prominent disclosure, the app SHALL NOT request the OS location permission and SHALL NOT start location tracking. The game SHALL continue without location broadcasting rather than crashing, looping the prompt, or blocking play. The player SHALL be able to reach the disclosure again on a later attempt to start tracking.

#### Scenario: Decline does not trigger the OS prompt

- **WHEN** the player dismisses or declines the disclosure
- **THEN** no OS location-permission prompt is shown and no background tracking is started

#### Scenario: Game continues without location after decline

- **WHEN** the player has declined the disclosure and the game is in progress
- **THEN** the app remains functional without location broadcasting and does not repeatedly force the disclosure within the same attempt

### Requirement: Consent is remembered across games

Once the player has given consent, the app SHALL remember it so the prominent disclosure is not shown again on every subsequent game. The disclosure SHALL be shown again only when consent has not been recorded or when the underlying OS location permission is no longer granted (for example it was revoked in system settings).

#### Scenario: Returning player is not re-disclosed

- **WHEN** the player has previously consented and the OS location permission is still granted
- **THEN** starting a new game proceeds to tracking without showing the disclosure again

#### Scenario: Re-disclosure after permission revoked

- **WHEN** the player previously consented but the OS location permission has since been revoked
- **THEN** the app shows the prominent disclosure again before re-requesting the OS permission

### Requirement: Disclosure copy is localized and styled through the single source of truth

The disclosure text and its affirmative/decline actions SHALL be localized (English and Dutch) and styled through each app's central styling — no hard-coded, unlocalized strings and no inline visual literals. In the MAUI app the copy SHALL come from `AppResources.resx` / `AppResources.nl.resx`; in the Ionic app from the `i18n` `en.json` / `nl.json` resources.

#### Scenario: Disclosure honours the active language

- **WHEN** the app language is Dutch and the disclosure is shown
- **THEN** the disclosure text and its action buttons are rendered in Dutch from the localized resources

#### Scenario: No hard-coded disclosure text

- **WHEN** the disclosure surface is built
- **THEN** all user-facing text is drawn from localized resource keys rather than literals embedded in the page or component
