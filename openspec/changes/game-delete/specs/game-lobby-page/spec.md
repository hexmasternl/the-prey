## ADDED Requirements

### Requirement: Display lobby roster

The client application SHALL provide a Game Lobby page at the route `/games/:id/lobby`. The page SHALL display the current lobby roster (all players who have joined), the game code, and the game configuration summary. The page MUST be accessible only to authenticated users. The page SHALL load the current game state on entry via `GET /games/:id`.

#### Scenario: Lobby page loads and shows players

- **WHEN** an authenticated user navigates to `/games/:id/lobby`
- **THEN** the page displays the list of players currently in the lobby and the game code

#### Scenario: Unauthenticated access is blocked

- **WHEN** an unauthenticated user navigates to `/games/:id/lobby`
- **THEN** the application redirects them to the login page

### Requirement: SSE subscription for real-time game events

The lobby page SHALL subscribe to the server-sent events stream at `GET /games/:id/events` using the browser `EventSource` API when the page becomes active (`ionViewWillEnter`). The subscription SHALL use the authenticated user's access token passed as a `token` query parameter. The page SHALL close the `EventSource` connection when the component is destroyed (`ngOnDestroy`). On loss of connection the client SHALL attempt to reconnect and, on reconnect, re-fetch the current game state as a fallback to detect events missed during the gap.

#### Scenario: Lobby page subscribes on enter

- **WHEN** the lobby page becomes active
- **THEN** an `EventSource` connection is opened to `GET /games/:id/events?token=<jwt>`

#### Scenario: Connection is closed on destroy

- **WHEN** the user navigates away from the lobby page
- **THEN** the `EventSource` connection is closed

### Requirement: Game-deleted alert in the lobby

When the lobby page receives a `game-deleted` SSE event, the page SHALL display a dismissible, thematically styled (military/tactical language) red alert banner informing participants that the game session was aborted by the host. The alert text SHALL use translated strings that match the overall tone of the application. The Create Game button (or any start action) SHALL become disabled. The user MAY tap a button in the alert to return to the home page.

#### Scenario: Participants see the game-deleted alert

- **WHEN** the lobby page receives a `game-deleted` SSE event for the current game
- **THEN** a red dismissible alert is displayed with thematic text indicating the host aborted the operation, and a navigation button to the home page is shown

#### Scenario: Alert is also shown when re-fetched state is Deleted

- **WHEN** the lobby page re-fetches game state on SSE reconnect and the status is Deleted
- **THEN** the same red alert is displayed as if the SSE event had been received
