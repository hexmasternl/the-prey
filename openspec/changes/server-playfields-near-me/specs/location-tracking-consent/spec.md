# Spec Delta

## MODIFIED Requirements

### Requirement: Disclosure content
The disclosure SHALL state, in the app's display language (English or Dutch): that The Prey tracks the device's precise location; that tracking happens only while the player is in an active game; that it continues while the app is closed, in the background or the screen is off; that a persistent notification is shown while tracking runs; that tracking stops when the game ends or the player leaves; why the game needs it (showing players' positions to the hunter and checking the playfield boundaries); and that when the player uses Near me on the public playfields, the app sends their approximate position once to find nearby playfields, which is not stored.

#### Scenario: Required statements present in English
- **WHEN** the disclosure is shown on a device set to English
- **THEN** the text covers precise location, active game only, background/screen-off collection, the persistent notification, when tracking stops, the gameplay purpose and the one-off approximate position for Near me

#### Scenario: Required statements present in Dutch
- **WHEN** the disclosure is shown on a device set to Dutch
- **THEN** the same statements are shown in Dutch

#### Scenario: Disclosure links to the privacy policy
- **WHEN** the player taps the privacy policy link in the disclosure
- **THEN** the privacy page on theprey.nl opens in the browser, in the player's language
