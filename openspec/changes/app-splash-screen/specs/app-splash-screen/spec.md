# Spec Delta

## Purpose

Defines what a player sees between tapping The Prey's launcher icon and the first interactive screen: a branded system splash followed by a short tactical launch sequence that sets the game's tone and then hands off to the home screen.

## ADDED Requirements

### Requirement: System splash shows the Prey mark
When the app is launched, the operating-system splash SHALL show the official Prey mark (green scope ring with the red prey head and green centre dot) centred on the void background (`#0C0E0C`). On Android versions that support animated splash icons, the scope ring SHALL draw itself in and the centre dot SHALL light up. The launcher icon SHALL use the same mark.

#### Scenario: Cold launch on a device with animated splash support
- **WHEN** the player taps the launcher icon and the app is not running
- **THEN** the system splash shows the Prey mark on the void background and animates the scope ring and centre dot

#### Scenario: Cold launch on an older Android version
- **WHEN** the app is launched on an Android version without animated splash icons
- **THEN** the system splash shows the static Prey mark on the void background

#### Scenario: Launcher icon
- **WHEN** the player looks at the app on the home screen or app drawer
- **THEN** the icon shows the Prey mark instead of the placeholder crosshair

### Requirement: Launch sequence continues seamlessly from the system splash
After the system splash is dismissed, the app SHALL show an in-app launch sequence whose first frame matches the system splash: same background color and the mark at the same position and size, so the player perceives one continuous animation with no flash, jump or blank frame.

#### Scenario: Hand-off from system splash
- **WHEN** the system splash ends
- **THEN** the launch sequence starts with the mark in the same place on the same background, with no visible discontinuity

### Requirement: Launch sequence plays a tactical boot animation
The launch sequence SHALL follow The Prey design system and SHALL contain: a radar sweep with expanding ping rings behind the mark over a faint field grid; signal-green corner-bracket chrome; a brief "target acquired" flash of the red prey head; the title "THE PREY" revealed letter by letter in the heading typeface; the tagline "STAY HIDDEN. HUNT SMART." with "HIDDEN." and "SMART." in signal green; and an UPPERCASE status line cycling through short boot messages.

#### Scenario: Full sequence
- **WHEN** the launch sequence plays without interruption
- **THEN** the player sees the radar sweep and pings, the prey-head flash, the typed title, the tagline and the cycling status line, in the design system's colors and typefaces

#### Scenario: Dutch device language
- **WHEN** the device language is Dutch
- **THEN** the tagline and status messages are shown in Dutch, while the title "THE PREY" stays untranslated

### Requirement: Launch sequence is short and leads to the home screen
The launch sequence SHALL complete within 3 seconds and SHALL then transition to the home screen with a crossfade of at most 400 ms. It SHALL NOT wait on network calls.

#### Scenario: Sequence completes
- **WHEN** the launch sequence has played to the end
- **THEN** the home screen fades in within 400 ms and becomes interactive

#### Scenario: No network
- **WHEN** the device is offline at launch
- **THEN** the launch sequence plays and ends at the same time as when online

### Requirement: Launch sequence can be skipped
The player SHALL be able to skip the launch sequence by tapping anywhere on the screen, which SHALL immediately start the transition to the home screen.

#### Scenario: Tap to skip
- **WHEN** the player taps the screen during the launch sequence
- **THEN** the sequence stops and the home screen fades in

### Requirement: Launch sequence plays only on a cold start
The launch sequence SHALL play only when the app process starts fresh. It SHALL NOT replay on configuration changes (such as rotation or theme change) or when the app returns from the background.

#### Scenario: Rotation during or after the sequence
- **WHEN** the device is rotated after the launch sequence has finished
- **THEN** the home screen stays visible and the sequence does not replay

#### Scenario: Returning from background
- **WHEN** the player switches to another app and back while the process is still alive
- **THEN** the app resumes on the screen it was on, without the launch sequence

### Requirement: Launch sequence respects reduced motion
When the system animation setting is turned off (animator duration scale of zero), the launch sequence SHALL skip all motion and show its final frame (mark, title, tagline) for about one second before showing the home screen.

#### Scenario: Animations disabled
- **WHEN** the player has disabled animations in the system settings and launches the app
- **THEN** the static final frame is shown briefly and the home screen follows, with no sweep, typing or flashing

### Requirement: Launch sequence is accessible
The launch sequence SHALL expose the app name to accessibility services as a single description, and decorative elements (grid, sweep, rings, brackets, status ticks) SHALL be hidden from them.

#### Scenario: TalkBack enabled
- **WHEN** TalkBack is on during the launch sequence
- **THEN** it announces "The Prey" once and does not read decorative elements or individual typed letters
