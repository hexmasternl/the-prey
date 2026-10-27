# Spec Delta

## ADDED Requirements

### Requirement: Allow all the time step follows a precise grant
On Android 10 and later, when the player grants precise location in a disclosure flow but background location ("Allow all the time") is not granted, the app SHALL continue that flow with an "Allow all the time" step instead of closing it. The step SHALL say why the game needs it, where to set it, and offer Open settings and Not now. The app SHALL NOT request background location through a system prompt.

#### Scenario: Precise granted, background missing
- **WHEN** the player accepts the disclosure on Android 10 or later and grants precise location in the system prompt, and background location is not granted
- **THEN** the dialog shows the "Allow all the time" step with Open settings and Not now
- **AND** no system prompt for background location is shown

#### Scenario: Background already granted
- **WHEN** the player grants precise location in a disclosure flow and background location is already granted
- **THEN** the flow closes without the "Allow all the time" step

#### Scenario: Android 9 or lower
- **WHEN** the player grants precise location in a disclosure flow on Android 9 or lower
- **THEN** the flow closes without the "Allow all the time" step

#### Scenario: Precise location denied
- **WHEN** the player denies the precise location prompt
- **THEN** the "Allow all the time" step is not shown

### Requirement: Allow all the time step closes on return or decline
Open settings in the "Allow all the time" step SHALL open the app's page in the system settings and keep the step open. When the app returns to the foreground with background location granted, the step SHALL close. Returning without it, or tapping Not now, SHALL close the step as well; the main menu then shows its existing background-location error. A game action that waited for location SHALL continue on the precise grant, whatever the player does in the step.

#### Scenario: Granted in settings
- **WHEN** the player taps Open settings, sets location to "Allow all the time" and returns to the app
- **THEN** the step closes and, once the active-game check has completed, the play button is enabled

#### Scenario: Returned without granting
- **WHEN** the player taps Open settings and returns to the app without setting "Allow all the time"
- **THEN** the step closes and the main menu shows the background-location error with Open settings

#### Scenario: Not now
- **WHEN** the player taps Not now in the "Allow all the time" step
- **THEN** the step closes without opening the settings and the main menu stays usable

#### Scenario: Pending game action
- **WHEN** a start or join action opened the disclosure and the player granted precise location
- **THEN** the action continues as it does today, and the "Allow all the time" step is shown above it

### Requirement: Allow all the time step never opens by itself
The "Allow all the time" step SHALL appear only as part of a disclosure flow that the player went through in the same session: the one-time automatic flow on the main menu, or a flow started by a start or join action. The app SHALL NOT show it by itself on a later visit to the main menu, after an app restart, or when background location is revoked in the system settings.

#### Scenario: Later visit with precise only
- **WHEN** precise location is granted, background location is not, and the player opens the main menu after an earlier flow ended
- **THEN** no dialog is shown and the main menu shows the background-location error with Open settings

#### Scenario: Background revoked in settings
- **WHEN** the player revokes "Allow all the time" in the system settings and returns to the app
- **THEN** no "Allow all the time" step is shown by itself
