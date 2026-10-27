# Spec Delta

## MODIFIED Requirements

### Requirement: Public tab shows a focused search field
When the Public tab is selected, it SHALL show a search field that receives focus and opens the on-screen keyboard, and a "Near me" option next to it that is off. Until a search has been made and while Near me is off, the tab SHALL show a hint that at least 3 characters are needed or that Near me lists playfields around the player.

#### Scenario: Search field focused
- **WHEN** the player selects the Public tab
- **THEN** a search field is shown, it has focus and the keyboard is open, and the Near me option is shown switched off

#### Scenario: Hint before searching
- **WHEN** the Public tab is shown, Near me is off and the search field holds fewer than 3 characters
- **THEN** no results are shown and a hint says to type at least 3 characters or use Near me

### Requirement: Public search is debounced and needs three characters
Typing in the search field SHALL send a public playfield search to the server only when the trimmed text has 3 or more characters, and only after the text has not changed for 366 ms. When a newer search is sent, results of older searches SHALL be ignored. With Near me off, shortening the text below 3 characters SHALL clear the results without a request. With Near me on, text shorter than 3 characters SHALL NOT filter by name, and the near-me results SHALL be shown.

#### Scenario: Fewer than three characters
- **WHEN** Near me is off and the player types "vo"
- **THEN** no search request is sent

#### Scenario: Debounced typing
- **WHEN** the player types "von", "vond" and "vondel" with less than 366 ms between keystrokes and then stops
- **THEN** exactly one search request is sent, for "vondel", 366 ms after the last keystroke

#### Scenario: Surrounding spaces do not count
- **WHEN** Near me is off and the player types "  vo "
- **THEN** no search request is sent, and when they type "von" a request for "von" is sent

#### Scenario: Stale results ignored
- **WHEN** a search for "von" is still running and a search for "vondel" is sent
- **THEN** only the results for "vondel" are shown

#### Scenario: Cleared below three characters
- **WHEN** Near me is off, results for "vondel" are shown and the player deletes text until "vo" remains
- **THEN** the results are cleared, the hint is shown and no request is sent

#### Scenario: Short text with Near me on
- **WHEN** Near me is on and the player deletes text until "vo" remains
- **THEN** the public playfields near the player are listed without a name filter

## ADDED Requirements

### Requirement: Near me lists public playfields around the player
Switching Near me on SHALL take one location fix from the device and search the public playfields within 25 km of it, nearest first. With 3 or more characters typed, the search SHALL also filter by name. Each result SHALL show its distance from the player. Switching Near me off SHALL return to name search. The Near me state SHALL survive a configuration change but not leaving the screen.

#### Scenario: Near me without typing
- **WHEN** location access is granted and the player switches Near me on with an empty search field
- **THEN** the public playfields within 25 km are listed nearest first, each with its distance, such as "1.2 km"

#### Scenario: Near me with a name
- **WHEN** Near me is on and the player types "park"
- **THEN** only nearby public playfields whose name contains "park" are listed, nearest first

#### Scenario: Near me switched off
- **WHEN** Near me is on and the player switches it off with "vo" in the search field
- **THEN** the near-me results are cleared and the hint is shown

#### Scenario: Rotation keeps Near me
- **WHEN** Near me is on and the device is rotated
- **THEN** Near me is still on and the nearby playfields are still listed

### Requirement: Near me sends only an approximate position
The app SHALL send the player's position for Near me rounded to 2 decimals of latitude and longitude, only when the player switched Near me on or changed the search text while it is on, and only while the Public tab is visible. Distances shown to the player SHALL be computed on the device from the unrounded fix.

#### Scenario: Rounded position sent
- **WHEN** the device fix is 52.35812, 4.86871 and the player switches Near me on
- **THEN** the request carries `lat=52.36` and `lon=4.87`

#### Scenario: No position without Near me
- **WHEN** the player searches by name with Near me off
- **THEN** the request carries no position

### Requirement: Near me explains when it cannot work
Near me SHALL work with approximate or precise location permission and SHALL NOT request a permission itself. Without location permission it SHALL say that Near me needs location access, which can be granted in Settings, and stay off. When no fix arrives within 10 seconds it SHALL say the location could not be determined and stay off. Name search SHALL keep working in both cases.

#### Scenario: No location permission
- **WHEN** location permission is missing and the player taps Near me
- **THEN** no permission prompt appears, a message says Near me needs location access from Settings, Near me stays off and name search keeps working

#### Scenario: No fix
- **WHEN** location permission is granted but no fix arrives within 10 seconds of tapping Near me
- **THEN** a message says the location could not be determined and Near me stays off

### Requirement: Near me shows an empty state
When a near-me search returns no playfields, the Public tab SHALL say that no public playfields were found near the player and suggest searching by name.

#### Scenario: Nothing nearby
- **WHEN** the near-me search returns an empty list
- **THEN** the tab says no public playfields were found near the player and suggests searching by name
