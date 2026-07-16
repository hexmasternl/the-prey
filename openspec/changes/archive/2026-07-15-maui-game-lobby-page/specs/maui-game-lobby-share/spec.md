## ADDED Requirements

### Requirement: Share action opens the native share sheet

The lobby SHALL provide a Share action next to the pass code that hands a localized invite to the device's native share sheet, so the user can send it through any installed messaging app.

#### Scenario: Sharing invokes the native share sheet

- **WHEN** the user activates the Share action with the game loaded
- **THEN** the device's native share sheet opens carrying the invite text

#### Scenario: Dismissing the share sheet is a no-op

- **WHEN** the user dismisses the native share sheet without sharing
- **THEN** the lobby remains unchanged and no error is shown

### Requirement: Invite content carries the pass code and a join deep link

The shared invite SHALL be a short localized message that includes the game's pass code and a join deep link constructed from that pass code. The pass code SHALL appear exactly as returned by the backend.

#### Scenario: Invite includes the code and link

- **WHEN** the invite is built for a game whose pass code is a given value
- **THEN** the invite text contains that pass code verbatim and a join deep link built from it

#### Scenario: Invite text is localized

- **WHEN** the app language is set to a supported language and the invite is built
- **THEN** the invite's fixed wording is drawn from the localized string resources, not hard-coded text
