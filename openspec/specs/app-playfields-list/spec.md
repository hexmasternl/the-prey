# app-playfields-list Specification

## Purpose
Defines the Android app's playfields list screen: where a signed-in player sees and deletes their own playfields, starts creating a new one, and searches the public playfields shared by others.

## Requirements

### Requirement: Playfields screen has a Private and a Public tab
The playfields screen SHALL show two tabs, "Private" and "Public". The Private tab SHALL be selected whenever the screen is opened from the main menu. The selected tab SHALL survive a configuration change such as a rotation.

#### Scenario: Private tab is selected on open
- **WHEN** the player taps Playfields in the main menu
- **THEN** the playfields screen opens with the Private tab selected and its content shown

#### Scenario: Previously selected Public tab is not remembered
- **WHEN** the player selected the Public tab, went back to the main menu and opens Playfields again
- **THEN** the Private tab is selected

#### Scenario: Tab survives rotation
- **WHEN** the Public tab is selected and the device is rotated
- **THEN** the Public tab is still selected and the search text is kept

#### Scenario: Tab labels follow the device language
- **WHEN** the device language is Dutch
- **THEN** the tabs read "Privé" and "Openbaar"

### Requirement: Private tab lists the player's own playfields
The Private tab SHALL download the playfields owned by the signed-in player from the server and show them as a list. While downloading for the first time it SHALL show a loading indicator. The list SHALL be downloaded again when the player returns to the screen from another screen.

#### Scenario: Own playfields are shown
- **WHEN** the Private tab is shown and the server returns two playfields owned by the player
- **THEN** both playfields are listed

#### Scenario: Loading indicator while downloading
- **WHEN** the Private tab is shown and the download has not completed yet
- **THEN** a loading indicator is shown instead of the list

#### Scenario: List refreshes after returning
- **WHEN** the player opens the playfield editor from the create button and then navigates back
- **THEN** the Private tab downloads the list again

### Requirement: Private tab shows empty and error states
When the player owns no playfields, the Private tab SHALL show a message inviting them to create one. When the download fails, it SHALL show an error message that says what to do and a retry action that downloads the list again.

#### Scenario: No playfields yet
- **WHEN** the server returns an empty list
- **THEN** the Private tab shows a message that the player has no playfields yet and can create one with the create button

#### Scenario: Download fails
- **WHEN** the download fails because the server is unreachable or returns an error
- **THEN** the Private tab shows an error message and a retry action

#### Scenario: Retry succeeds
- **WHEN** the error is shown and the player taps retry and the server now returns the playfields
- **THEN** the playfields are listed

### Requirement: Playfield list items show the playfield's details
Each list item, on both tabs, SHALL show the playfield's name, its visibility (private or public), the date it was last updated, and its centre coordinates when the server provides them. The date and the decimal separator follow the device language.

#### Scenario: Item details
- **WHEN** a playfield named "Vondelpark" that is public, last updated on 2026-10-20, with centre 52.3580, 4.8686 is listed on an English device
- **THEN** its item shows "Vondelpark", a "Public" badge, the date 20 Oct 2026 in the device's date format, and the coordinates `52.3580° N // 4.8686° E`

#### Scenario: No centre coordinates
- **WHEN** a listed playfield has no centre coordinates
- **THEN** its item shows the name, badge and date and no coordinate line

### Requirement: Create button opens the playfield editor
The Private tab SHALL show a floating create button in the bottom-right corner, above the system navigation area, that opens the playfield editor. The create button SHALL NOT be shown on the Public tab.

#### Scenario: Create a playfield
- **WHEN** the player taps the create button on the Private tab
- **THEN** the playfield editor opens, and Back returns to the playfields screen on the Private tab

#### Scenario: No create button on the Public tab
- **WHEN** the Public tab is selected
- **THEN** no create button is shown

#### Scenario: Create button is labelled for accessibility
- **WHEN** a screen reader focuses the create button
- **THEN** it is announced as "Create playfield" (Dutch: "Speelveld maken")

### Requirement: Swiping a private-tab item left reveals a delete button
On the Private tab, the player SHALL be able to slide a playfield item to the left to reveal a Delete button behind it. Releasing past halfway SHALL keep the button revealed; otherwise the item slides back. At most one item SHALL be revealed at a time, and sliding right or tapping elsewhere in the list SHALL close it. The same delete SHALL be offered to screen-reader users as an accessibility action on the item.

#### Scenario: Reveal the delete button
- **WHEN** the player slides a playfield item to the left past halfway the button width and releases
- **THEN** the item stays shifted and a Delete button is visible on its right

#### Scenario: Short slide snaps back
- **WHEN** the player slides an item left by less than half the button width and releases
- **THEN** the item slides back and no Delete button is visible

#### Scenario: Only one item revealed
- **WHEN** one item's Delete button is revealed and the player reveals another item's Delete button
- **THEN** the first item slides back

#### Scenario: Delete through a screen reader
- **WHEN** a screen-reader user opens the actions of a Private tab item
- **THEN** a "Delete" action is offered that opens the same confirmation as the button

### Requirement: Deleting a playfield requires confirmation
Tapping Delete SHALL open a confirmation dialog naming the playfield and stating that deletion cannot be undone. The playfield SHALL only be deleted when the player confirms. Cancelling SHALL close the dialog, slide the item back and send nothing to the server.

#### Scenario: Confirmation shown
- **WHEN** the player taps the revealed Delete button of "Vondelpark"
- **THEN** a dialog asks to confirm deleting "Vondelpark", with a destructive confirm action and a cancel action, and no request has been sent

#### Scenario: Cancel
- **WHEN** the confirmation dialog is shown and the player cancels or dismisses it
- **THEN** the dialog closes, the item slides back, the playfield is still listed and no delete request is sent

### Requirement: Confirmed deletion removes the playfield on the server
After confirmation the app SHALL send a delete request for the playfield to the server and SHALL remove the item from the list when the server reports it deleted or not found. While the request runs, the item SHALL show it is busy and cannot be deleted again. When the request fails, the item SHALL stay in the list and the player SHALL see a message saying the playfield was not deleted and to try again. The owner can delete their own playfields whether they are private or public.

#### Scenario: Delete succeeds
- **WHEN** the player confirms deleting a playfield and the server responds that it was deleted
- **THEN** the item is removed from the Private tab

#### Scenario: Already gone
- **WHEN** the player confirms deleting a playfield and the server responds that it does not exist
- **THEN** the item is removed from the Private tab

#### Scenario: Delete fails
- **WHEN** the player confirms deleting a playfield and the request fails (network error, forbidden or server error)
- **THEN** the item stays in the list, slides back, and a message says the playfield was not deleted and to try again

#### Scenario: Owner deletes their own public playfield
- **WHEN** the player slides one of their own public playfields on the Private tab and confirms deletion
- **THEN** the delete request is sent and the item is removed on success

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

### Requirement: Public search shows results, no-results and error states
The Public tab SHALL list the public playfields the server returns for the search, show a loading indicator while a search runs, show a "no public playfields found" message when the result is empty, and show an error message when the search fails. The server SHALL only return public playfields.

#### Scenario: Results shown
- **WHEN** the search for "vondel" returns two public playfields
- **THEN** both are listed with their details

#### Scenario: No results
- **WHEN** the search returns an empty list
- **THEN** the tab shows a message that no public playfields match the search

#### Scenario: Search fails
- **WHEN** the search request fails
- **THEN** the tab shows an error message saying to check the connection and try again, and the next change to the search text searches again

### Requirement: Public playfields cannot be removed from the Public tab
Items on the Public tab SHALL NOT be deletable: they SHALL NOT slide, SHALL NOT show a Delete button and SHALL NOT offer a delete accessibility action, including the player's own public playfields.

#### Scenario: Public item does not slide
- **WHEN** the player slides a Public tab item to the left
- **THEN** the item does not move and no Delete button appears

#### Scenario: Own public playfield on the Public tab
- **WHEN** a search result is a public playfield owned by the player
- **THEN** it is listed without any delete option on the Public tab

#### Scenario: No delete action for screen readers
- **WHEN** a screen-reader user opens the actions of a Public tab item
- **THEN** no delete action is offered

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
