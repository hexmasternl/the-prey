## ADDED Requirements

### Requirement: One-time disclosure gate at app entry

Before the app shows its main menu (the home screen), the app SHALL present a prominent disclosure explaining that, while a game is in progress, a background service collects the device's location and sends it to the game's server for the duration of the game — including when the app is in the background or closed. The disclosure SHALL require an explicit affirmative action to accept. The app SHALL NOT proceed to the main menu until the player has accepted. This gate applies to both the MAUI app and the Ionic app, and is shown ahead of any OS location-permission prompt (which is still requested later, at game start, unchanged).

#### Scenario: Disclosure precedes the main menu

- **WHEN** the app finishes starting up and has not yet recorded the player's consent
- **THEN** the prominent disclosure is shown before the main menu, and the app does not navigate to the main menu until the player accepts

#### Scenario: Applies to both clients

- **WHEN** either the MAUI app or the Ionic app starts and consent has not been recorded
- **THEN** the same disclosure-and-consent gate is shown before that client's main menu

### Requirement: Acceptance is remembered permanently

When the player accepts the disclosure, the app SHALL persist that consent and SHALL NOT show the disclosure again on any subsequent launch. The disclosure is a one-time gate, not a per-game prompt.

#### Scenario: Accepting continues to the app

- **WHEN** the player accepts the disclosure
- **THEN** the app records consent and continues to the main menu

#### Scenario: Returning player is not re-prompted

- **WHEN** the player has previously accepted the disclosure and launches the app again
- **THEN** the disclosure is not shown and the app goes straight to the main menu

### Requirement: Declining blocks entry per platform

If the player declines the disclosure, the app SHALL NOT proceed to the main menu. On platforms where an application may terminate itself — Android and desktop — the app SHALL close. On platforms where programmatic termination is not permitted or possible — iOS and the web/PWA build — the app SHALL instead present a full-screen, non-dismissable consent wall that offers only an affirmative action to accept; the player cannot reach the app until they accept, and accepting records consent and continues to the main menu.

#### Scenario: Decline closes the app on Android and desktop

- **WHEN** the player declines the disclosure on Android or a desktop build
- **THEN** the app closes

#### Scenario: Decline shows a blocking wall on iOS and web

- **WHEN** the player declines the disclosure on iOS or the web/PWA build
- **THEN** a full-screen, non-dismissable consent wall is shown with only an accept action, and the main menu remains unreachable until the player accepts

#### Scenario: Accepting the wall continues

- **WHEN** the player accepts on the blocking consent wall
- **THEN** the app records consent and continues to the main menu

### Requirement: Disclosure copy is localized and styled through the single source of truth

The disclosure text and its actions SHALL be localized (English and Dutch) and styled through each app's central styling — no hard-coded, unlocalized strings and no inline visual literals. In the MAUI app the copy SHALL come from `AppResources.resx` / `AppResources.nl.resx`; in the Ionic app from the `i18n` `en.json` / `nl.json` resources.

#### Scenario: Disclosure honours the active language

- **WHEN** the app language is Dutch and the disclosure (or the consent wall) is shown
- **THEN** its text and actions are rendered in Dutch from the localized resources

#### Scenario: No hard-coded disclosure text

- **WHEN** the disclosure or consent wall is built
- **THEN** all user-facing text is drawn from localized resource keys rather than literals embedded in the page or component
