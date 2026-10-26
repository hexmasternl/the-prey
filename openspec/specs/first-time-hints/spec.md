# first-time-hints Specification

## Purpose
Decides when the Android app's first-time tours (playfield area, prey HUD, hunter HUD) are shown, how the player moves through them, and how their per-device "seen" state is kept and reset.

## Requirements

### Requirement: Three independent first-time tours

The app SHALL keep a separate per-device "seen" flag for each of three tours: the playfield area tour, the prey HUD tour and the hunter HUD tour. Every flag SHALL start as not seen. Seeing one tour SHALL NOT mark any other tour as seen.

#### Scenario: Fresh install

- **WHEN** the app is installed and the player has never finished or skipped a tour
- **THEN** all three tours are not seen

#### Scenario: Flags are independent

- **WHEN** the player has seen the prey HUD tour but never the hunter HUD tour
- **THEN** becoming the hunter of an in-progress game still shows the hunter HUD tour

### Requirement: A tour is shown at most once until reset

Once a tour has been completed or skipped, the app SHALL NOT show that tour again until the hints are reset. A tour that was neither completed nor skipped (for example because the player left the screen or the app was closed mid-tour) SHALL stay not seen and SHALL be shown again the next time its situation occurs.

#### Scenario: Completed tour does not repeat

- **WHEN** the player completed the prey HUD tour and later plays as a prey again
- **THEN** no tour is shown

#### Scenario: Skipped tour does not repeat

- **WHEN** the player skips the playfield area tour on its first step and later creates another playfield
- **THEN** no tour is shown on the area screen

#### Scenario: Leaving mid-tour

- **WHEN** the player is on step 3 of the hunter HUD tour and goes back to the main menu
- **THEN** the hunter HUD tour is still not seen and starts again from step 1 when the player returns to the game screen

### Requirement: Seen state is device-local

The seen flags SHALL be stored on the device only. They SHALL survive app restarts and signing out and back in, SHALL NOT be sent to the server, and SHALL NOT be restored from a backup.

#### Scenario: Survives sign-out

- **WHEN** the player has seen the playfield area tour, signs out and signs in again
- **THEN** the playfield area tour is still seen

#### Scenario: Reinstall starts fresh

- **WHEN** the app is uninstalled and installed again
- **THEN** all three tours are not seen

### Requirement: Tour steps and controls

A tour SHALL show one step at a time over a dimmed screen, spotlighting the step's control when it is on screen, with a title, an explanation and a step counter such as "2 / 6". Each step SHALL offer Skip and Next; the last step SHALL offer Done instead of Next; every step after the first SHALL offer Back. Done marks the tour completed and Skip marks it skipped.

#### Scenario: Advancing

- **WHEN** the player taps Next on step 2 of a 6-step tour
- **THEN** step 3 is shown with the counter "3 / 6"

#### Scenario: Going back

- **WHEN** the player taps Back on step 3
- **THEN** step 2 is shown again

#### Scenario: Finishing

- **WHEN** the player taps Done on the last step
- **THEN** the tour closes and is marked seen

#### Scenario: Control not on screen

- **WHEN** a step's control is not currently laid out on screen
- **THEN** the step is still shown, as a centred card without a spotlight

### Requirement: Tour blocks the screen underneath

While a tour is open, touches SHALL NOT reach the screen underneath, so no point is added, no button is pressed and the map does not pan. The system Back gesture SHALL skip the tour and SHALL NOT leave the screen.

#### Scenario: Tap during the area tour

- **WHEN** the playfield area tour is open and the player taps the map
- **THEN** no point is added

#### Scenario: Back during a tour

- **WHEN** a tour is open and the player uses the system Back gesture
- **THEN** the tour closes, is marked seen, and the player stays on the same screen

### Requirement: Tour position survives configuration changes

The open tour and its current step SHALL be kept when the device rotates or the app is briefly sent to the background.

#### Scenario: Rotation

- **WHEN** step 4 of the prey HUD tour is shown and the device rotates
- **THEN** step 4 is still shown, spotlighting the same control in its new position

### Requirement: Tours are accessible

A screen reader SHALL read each step's title, explanation and counter when the step appears, and SHALL reach Skip, Back, Next and Done as buttons. Spotlight movement SHALL NOT animate when the device has animations turned off.

#### Scenario: Screen reader on a new step

- **WHEN** TalkBack is on and the tour moves to the next step
- **THEN** the step's title, explanation and counter are announced

#### Scenario: Reduced motion

- **WHEN** the device has animations turned off and the player taps Next
- **THEN** the spotlight jumps to the next control without animating

### Requirement: Hint storage failures never block the screen

A failure to read the seen flags SHALL be treated as not seen, so the tour may be shown. A failure to write a flag SHALL close the tour normally and at worst let it show again later. Neither SHALL stop the player from using the screen.

#### Scenario: Read failure

- **WHEN** the seen flags cannot be read
- **THEN** the screen stays fully usable and the tour is offered as if not yet seen

#### Scenario: Write failure

- **WHEN** marking a tour as seen fails
- **THEN** the tour closes and the screen is fully usable

### Requirement: Tour text in English and Dutch

All tour titles, explanations, counters, buttons and accessibility descriptions SHALL be available in English and Dutch and SHALL follow the app language.

#### Scenario: Dutch device

- **WHEN** the app language is Dutch and a tour is shown
- **THEN** its titles, explanations and the Skip, Back, Next and Done buttons are in Dutch
