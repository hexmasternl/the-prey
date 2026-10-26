# app-landing-page Specification

## Purpose
Defines the first interactive screen a signed-out player sees and the launch routing that sends signed-in players straight to the main menu, so returning players never pass through the landing page.

## Requirements

### Requirement: Landing page invites the player to sign in
When no player is signed in, the first interactive screen SHALL be the landing page. It SHALL show the Prey mark, the title "THE PREY", the tagline and a single primary "Sign in" action, styled with The Prey design system, with all texts available in English and Dutch.

#### Scenario: Signed-out player opens the app
- **WHEN** the app starts and no player is signed in
- **THEN** the landing page is shown with the Prey mark, title, tagline and a "Sign in" button

#### Scenario: Dutch device language
- **WHEN** the device language is Dutch
- **THEN** all landing-page texts are shown in Dutch

### Requirement: Landing page shows sign-in progress and errors
While sign-in or player identification is in progress, the landing page SHALL show a busy state and SHALL disable the "Sign in" action so it cannot be started twice. Failures SHALL be shown inline as a short message, after which the action is enabled again.

#### Scenario: Sign-in in progress
- **WHEN** the player has tapped "Sign in" and the flow has not finished
- **THEN** a busy indicator is shown and tapping "Sign in" again has no effect

#### Scenario: Sign-in error
- **WHEN** sign-in or identification fails
- **THEN** an inline message describes the problem and "Sign in" can be tapped again

### Requirement: Successful sign-in leads to the main menu
After the player is signed in and identified, the app SHALL navigate to the main menu and remove the landing page from the back stack, so pressing Back on the main menu leaves the app instead of returning to the landing page.

#### Scenario: Sign-in completes
- **WHEN** sign-in and identification succeed
- **THEN** the main menu is shown

#### Scenario: Back from the main menu
- **WHEN** the player presses Back on the main menu
- **THEN** the app closes and the landing page is not shown

### Requirement: Launch routing skips the landing page for signed-in players
On every app start, the app SHALL decide the first interactive screen from the locally stored session: the main menu when a restorable session exists, otherwise the landing page. The decision SHALL NOT wait on network calls, and the landing page SHALL NOT be shown, even briefly, to a signed-in player.

#### Scenario: Signed-in player opens the app
- **WHEN** the app starts and a restorable session with a cached profile exists
- **THEN** the main menu is the first interactive screen and the landing page is never displayed

#### Scenario: Launch after the session was lost
- **WHEN** the app starts and the stored session has been cleared or rejected
- **THEN** the landing page is the first interactive screen

#### Scenario: Launch sequence hand-off
- **WHEN** the launch sequence finishes or is skipped
- **THEN** the app shows the destination chosen by launch routing (main menu or landing page)

### Requirement: Main menu greets the identified player
The main menu SHALL show the identified player's callsign, or their display name when no callsign is set, and SHALL offer a "Sign out" action. When the background profile refresh returns updated details, the greeting SHALL update without restarting the screen.

#### Scenario: Main menu after sign-in
- **WHEN** the main menu is shown
- **THEN** it greets the player by callsign (or display name) and shows a "Sign out" action

#### Scenario: Profile updated in the background
- **WHEN** the background profile refresh returns a changed callsign
- **THEN** the main menu shows the new callsign

### Requirement: Session loss anywhere returns to the landing page
If the session becomes unrecoverable while the app is open, the app SHALL navigate to the landing page and clear the back stack, so no signed-in screen remains reachable.

#### Scenario: Session revoked while on the main menu
- **WHEN** the session is found to be unrecoverable while the main menu is shown
- **THEN** the landing page is shown and pressing Back closes the app
