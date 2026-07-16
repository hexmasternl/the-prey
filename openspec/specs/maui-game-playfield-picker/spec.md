# maui-game-playfield-picker Specification

## Purpose
TBD - created by archiving change maui-game-create-new. Update Purpose after archive.
## Requirements
### Requirement: Opening the picker with the user's own playfields

The playfield picker SHALL open showing the playfields the current user created (their own private and public playfields), loaded with the caller's access token.

#### Scenario: Picker shows own playfields on open

- **WHEN** the user opens the playfield picker
- **THEN** the list shows the playfields the current user created, including both their private and public ones

#### Scenario: Own playfields fail to load

- **WHEN** loading the user's own playfields fails (network, timeout, or unexpected status)
- **THEN** an error state is shown and the user may retry without crashing

#### Scenario: User has no playfields

- **WHEN** the user has created no playfields and no search is active
- **THEN** an empty state is shown

### Requirement: Debounced minimum-length search

The picker search SHALL start only when the trimmed query is at least 3 characters and SHALL be debounced by 300 ms, with each new keystroke superseding any in-flight search.

#### Scenario: Query shorter than the minimum shows the own list

- **WHEN** the trimmed query is fewer than 3 characters
- **THEN** no search is sent and the picker continues to show the user's own playfields

#### Scenario: Search runs after the debounce window

- **WHEN** the user types a query of at least 3 characters and pauses for the debounce window
- **THEN** a single search is performed for that query

#### Scenario: Rapid typing supersedes the earlier search

- **WHEN** the user types further characters before the debounce window elapses
- **THEN** only the search for the latest query is performed and earlier in-flight searches are discarded

### Requirement: Merged private-and-public search results

For a query of at least 3 characters the picker SHALL return playfields matching the query drawn from both the user's own (private and public) playfields and public playfields from other owners, merged and de-duplicated by playfield id.

#### Scenario: Results include own private and public matches plus others' public matches

- **WHEN** a query of at least 3 characters matches one of the user's own private playfields, one of the user's own public playfields, and a public playfield owned by someone else
- **THEN** all three appear in the results

#### Scenario: A playfield matched by both sources appears once

- **WHEN** a query matches one of the user's own public playfields that is also returned by the public search
- **THEN** that playfield appears exactly once in the results, retaining its public/private badge

#### Scenario: No matches

- **WHEN** a query of at least 3 characters matches no playfield from either source
- **THEN** a no-results state is shown

#### Scenario: Search failure

- **WHEN** the public search fails to complete (network, timeout, or unexpected status)
- **THEN** an error state is shown and the user may retry

#### Scenario: Unauthorized search

- **WHEN** a picker load or search responds `401`
- **THEN** the cached access token is invalidated and an error state is shown without crashing

### Requirement: Selecting a playfield returns it to the configuration page

Tapping a playfield in the picker SHALL select it and return it to the game-configuration page; dismissing the picker without a selection SHALL leave the configuration page's selected playfield unchanged.

#### Scenario: Selecting a playfield

- **WHEN** the user taps a playfield in the picker
- **THEN** the picker closes and the game-configuration page holds that playfield as the selected playfield

#### Scenario: Dismissing without selecting

- **WHEN** the user dismisses the picker without tapping a playfield
- **THEN** the configuration page's previously selected playfield (if any) is unchanged

