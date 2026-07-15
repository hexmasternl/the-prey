## ADDED Requirements

### Requirement: Tag button visibility

A Tag icon button SHALL be shown right-aligned above the HUD only when the player is the hunter; it SHALL NOT be shown to preys.

#### Scenario: Hunter sees the Tag button

- **WHEN** the player is the hunter
- **THEN** the Tag button is shown above the HUD

#### Scenario: Prey does not see the Tag button

- **WHEN** the player is a prey
- **THEN** no Tag button is shown

### Requirement: Finding nearby preys to tag

When the hunter activates the Tag button, the app SHALL request the preys currently within tag range from the server and present them in a modal selection dialog listing each candidate; when there are none it SHALL inform the hunter without opening the dialog.

#### Scenario: Candidates are listed

- **WHEN** the hunter activates the Tag button and the server returns one or more preys in range
- **THEN** a modal dialog opens listing each candidate prey (callsign and distance) for the hunter to tap

#### Scenario: No preys in range

- **WHEN** the hunter activates the Tag button and the server returns no preys in range
- **THEN** the hunter is told no preys are in range and no selection dialog opens

#### Scenario: Candidate request rejected

- **WHEN** the candidate request responds forbidden, not found, or fails
- **THEN** an error is surfaced and no selection dialog opens

### Requirement: Confirming and tagging a prey

After the hunter taps a candidate prey, the app SHALL ask for confirmation before tagging; on confirmation it SHALL send a tag request to the server for the selected prey, and on cancellation it SHALL make no tag request.

#### Scenario: Confirm then tag

- **WHEN** the hunter taps a candidate and confirms the "really tag this player?" prompt
- **THEN** a tag request is sent for the selected prey and, on success, the tag is applied and the HUD's preys-active count reflects the change on the next refresh

#### Scenario: Cancel the confirmation

- **WHEN** the hunter taps a candidate but cancels the confirmation prompt
- **THEN** no tag request is sent and the game state is unchanged

#### Scenario: Prey no longer taggable

- **WHEN** the tag request responds that the target is no longer taggable (out of range or already tagged)
- **THEN** a message is surfaced and the hunter may re-open the candidate list to try another prey

#### Scenario: Tag request unauthorized

- **WHEN** the tag request responds unauthorized
- **THEN** the cached access token is invalidated and an error is surfaced without crashing
