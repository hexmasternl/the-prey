## ADDED Requirements

### Requirement: Penalty indication banner

While the local player is under an active boundary penalty, the game view SHALL show an alarming, full-width banner pinned to the top of the screen containing a localized "PENALTY" caption and a live countdown of the time remaining in the penalty, formatted as mm:ss. The banner SHALL use the app's alarming (bright red) colour and be styled and localized through the single-source-of-truth styling and string resources (no inline visual literals, no hard-coded user-facing text). The banner SHALL be shown for whichever role — prey or hunter — the local player currently holds, and only when the penalty belongs to the local player.

#### Scenario: Banner appears while penalised

- **WHEN** the local player's own penalty is active during an in-progress game
- **THEN** a bright-red banner pinned to the top of the screen shows a "PENALTY" caption and a countdown of the remaining penalty time

#### Scenario: Countdown ticks down locally

- **WHEN** a second passes while the local player is penalised
- **THEN** the banner's remaining-time countdown decreases by one second

#### Scenario: Banner hides when the penalty expires

- **WHEN** the penalty's end time passes
- **THEN** the banner is hidden automatically without requiring a server event

#### Scenario: No banner when not penalised

- **WHEN** the local player has no active penalty
- **THEN** no penalty banner is shown over the map

#### Scenario: Banner shown for either role

- **WHEN** the local player incurs a penalty regardless of being the prey or the hunter
- **THEN** the penalty banner is shown for that player
