# Spec Delta

## MODIFIED Requirements

### Requirement: Public tab searches public playfields for selection
The Public tab SHALL show a search field that receives focus, opening the keyboard, when the tab is selected, and a "Near me" option that is off. Once the trimmed text has 3 or more characters, the screen SHALL search the server's public playfields 366 ms after the last change; a newer search SHALL supersede one still in flight. With Near me off and fewer than 3 characters it SHALL send nothing and show a hint. It SHALL show a no-results message and a search error message when applicable.

#### Scenario: Search after the debounce
- **WHEN** the player types "von" on the Public tab and stops typing
- **THEN** after 366 ms `GET /playfields/public?q=von` is sent and the matching public playfields are listed

#### Scenario: Short query shows the hint
- **WHEN** Near me is off and the search field contains fewer than 3 characters after trimming
- **THEN** no request is sent and a hint to type at least 3 characters or use Near me is shown

#### Scenario: Stale results are ignored
- **WHEN** a search for "vond" is still running and the player changes the text to "vondel"
- **THEN** only the results for "vondel" are shown

#### Scenario: No public playfield matches
- **WHEN** the server returns no playfields for the query
- **THEN** a message says no public playfields match the search

## ADDED Requirements

### Requirement: Near me on the selection screen
The selection screen's Public tab SHALL offer the same Near me option as the playfields list's Public tab, with the same behaviour: nearby public playfields within 25 km, nearest first, with distances, combined with name search, an approximate position sent only while it is on, and the same messages without permission, without a fix and when nothing is nearby. A near-me result SHALL be selectable like any other public playfield.

#### Scenario: Pick a nearby playfield
- **WHEN** the player creating a game switches Near me on and taps the nearest result
- **THEN** that playfield is selected and the bottom bar shows its name

#### Scenario: Selection survives switching Near me off
- **WHEN** a near-me result is selected and the player switches Near me off
- **THEN** the selection is kept and its name stays in the bottom bar
