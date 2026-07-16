## ADDED Requirements

### Requirement: Join page entry with a game id

The Join Game page SHALL open for a specific game id supplied by the invite link.

#### Scenario: Page opens for the invited game

- **WHEN** the Join Game page is opened from an invite link carrying a game id
- **THEN** the page is bound to that game id for the join request

### Requirement: Sign-in gate

The Join Game page SHALL require the user to be signed in before joining; when the user is not signed in it SHALL drive the interactive login flow while preserving the pending game id, and it SHALL NOT lose the invite if login is cancelled or fails.

#### Scenario: Signed-out user is prompted to sign in

- **WHEN** the Join Game page opens and no access token is available
- **THEN** the page shows a signed-out state prompting the user to sign in, and the pending game id is retained

#### Scenario: Continue after successful login

- **WHEN** a signed-out user completes the interactive login on the Join Game page
- **THEN** the page continues for the same game id and presents the join-code entry

#### Scenario: Cancelled login keeps the invite

- **WHEN** the interactive login is cancelled or fails
- **THEN** the page returns to the signed-out state with the pending game id intact so the user can retry

### Requirement: Join-code entry

The Join Game page SHALL present a join-code field that accepts exactly 4 decimal digits, styled per the app's single-source-of-truth styling and localized (no inline visual literals, no hard-coded user-facing text).

#### Scenario: Only four digits are accepted

- **WHEN** the user types into the join-code field
- **THEN** only decimal digits are accepted and the field holds at most 4 of them

### Requirement: Join enablement

The JOIN action SHALL be enabled only when the join-code field holds 4 digits, the user is signed in, and no join request is in flight.

#### Scenario: Join disabled without a complete code

- **WHEN** the join-code field holds fewer than 4 digits
- **THEN** the JOIN action is disabled

#### Scenario: Join enabled with a complete code

- **WHEN** the user is signed in, the join-code field holds 4 digits, and no join request is in flight
- **THEN** the JOIN action is enabled

### Requirement: Joining the game

On JOIN the app SHALL send an authenticated `POST /games/{gameId}/join` request carrying the entered join code and the caller's display name; on `200 OK` it SHALL navigate to the game/lobby route.

#### Scenario: Display name sourced from the current user

- **WHEN** the join request is built
- **THEN** the display name is taken from the current user's profile, falling back to a default display name when the profile has none

#### Scenario: Successful join

- **WHEN** JOIN is activated with a 4-digit code and the backend responds `200 OK`
- **THEN** the app navigates to the game/lobby route for the joined game

#### Scenario: Invalid code

- **WHEN** the backend responds `400` (invalid join code)
- **THEN** the page remains open with the entered code and shows an invalid-code message

#### Scenario: Game not found

- **WHEN** the backend responds `404`
- **THEN** the page remains open and shows a game-not-found message

#### Scenario: Game-state conflict

- **WHEN** the backend responds `409` (e.g. the game has already started or is full)
- **THEN** the page remains open and shows a message derived from the returned rule code

#### Scenario: Unauthorized session

- **WHEN** the join request responds `401`
- **THEN** the cached access token is invalidated and the page returns to the signed-out state without crashing

#### Scenario: Transient failure

- **WHEN** the join request fails to complete (network or timeout) or returns an unexpected status
- **THEN** an error state is shown and the user may retry JOIN

#### Scenario: No access token

- **WHEN** JOIN is activated but no access token can be acquired
- **THEN** no request is sent and the page returns to the signed-out state
