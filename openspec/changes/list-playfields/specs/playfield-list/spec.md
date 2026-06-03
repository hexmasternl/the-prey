## ADDED Requirements

### Requirement: Fetch playfields on page load
When the playfields page is opened and the device is online, the app SHALL request the authenticated user's private playfields from the server and display them in a list. Successfully fetched playfields SHALL be persisted to local storage, replacing any previously cached data.

#### Scenario: Online fetch succeeds
- **WHEN** the user opens the playfields page and the device is online
- **THEN** the app calls the server, displays the returned playfields, and writes them to local cache

#### Scenario: Online fetch fails with a network error
- **WHEN** the playfields page is opened, the device appears online, but the request fails
- **THEN** the app displays an error message and falls back to showing the locally cached playfields (if any)

#### Scenario: Online fetch returns 401 Unauthorized
- **WHEN** the server returns 401 during a playfields fetch
- **THEN** the app navigates the user to the login page

### Requirement: Offline fallback
When the device is offline or the server is unreachable, the app SHALL display playfields loaded from local cache. If no cached data exists, the app SHALL display an appropriate empty-state message.

#### Scenario: Device is offline, cache exists
- **WHEN** the user opens the playfields page while offline
- **THEN** the app displays the locally cached playfields without attempting a server call

#### Scenario: Device is offline, no cache
- **WHEN** the user opens the playfields page while offline and no local cache exists
- **THEN** the app displays an empty-state message indicating no playfields are available offline

### Requirement: Navigate to create new playfield
The playfields page SHALL display a "Create new" button. Tapping it SHALL navigate to the playfield creation page.

#### Scenario: User taps Create new
- **WHEN** the user taps the "Create new" button on the playfields page
- **THEN** the app navigates to the playfield creation page

### Requirement: Navigate to playfield detail
Tapping a playfield in the list SHALL navigate to the detail view for that playfield.

#### Scenario: User taps a playfield
- **WHEN** the user taps a playfield entry in the list
- **THEN** the app navigates to the detail page for the selected playfield

### Requirement: Swipe-to-delete with confirmation
The user SHALL be able to swipe a playfield entry to the left to reveal a delete action. Activating the delete action SHALL prompt the user to confirm before proceeding.

#### Scenario: User swipes and confirms delete
- **WHEN** the user swipes a playfield left and taps the delete action and confirms the confirmation prompt
- **THEN** the app sends a DELETE request to the server, removes the playfield from local cache, and removes it from the displayed list

#### Scenario: User swipes and cancels delete
- **WHEN** the user swipes a playfield left and taps the delete action but cancels the confirmation prompt
- **THEN** the playfield remains in the list and no server call is made

#### Scenario: Delete request fails
- **WHEN** the user confirms deletion but the server returns an error
- **THEN** the app displays an error message, the playfield remains in the list, and local cache is unchanged
