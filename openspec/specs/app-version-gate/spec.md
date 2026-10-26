# app-version-gate Specification

## Purpose

Defines the app version gate: a backend version-checker endpoint that validates a client's app version against a runtime-configurable minimum supported version, and the client-side gating behavior that disables action buttons on the home and game-join screens until the version check resolves, prompting users to update via the Play Store when their app is out of date.

## Requirements

### Requirement: Version-checker endpoint validates client version against configured minimum

The Games API SHALL expose `POST /games/version-checker` that accepts a JSON body
`{ "current-version": "x.x.x" }`, where each `x` is a non-negative integer, and compares the
supplied version against a minimum supported version read from Azure App Configuration. The
comparison SHALL be a numeric, component-wise comparison of `(major, minor, patch)` — not a
lexical string comparison. The endpoint SHALL be reachable without authentication.

#### Scenario: Client version meets the minimum

- **WHEN** the request body contains a version greater than or equal to the configured minimum
- **THEN** the endpoint returns `204 No Content`

#### Scenario: Client version is below the minimum

- **WHEN** the request body contains a version lower than the configured minimum
- **THEN** the endpoint returns `409 Conflict`

#### Scenario: Numeric ordering is respected

- **WHEN** the configured minimum is `1.9.0` and the client sends `1.10.0`
- **THEN** the endpoint returns `204 No Content` (1.10.0 is not below 1.9.0)

#### Scenario: No minimum configured

- **WHEN** the minimum-version App Configuration key is absent or empty
- **THEN** every well-formed client version is treated as up to date and the endpoint returns `204 No Content`

#### Scenario: Malformed version payload

- **WHEN** the request body is missing `current-version`, or it is not three non-negative integers separated by dots
- **THEN** the endpoint returns `400 Bad Request` (distinct from the `409` update-required signal)

#### Scenario: Endpoint is callable without a bearer token

- **WHEN** the request carries no `Authorization` header
- **THEN** the endpoint still evaluates the version and returns `204` or `409` accordingly (it is not rejected with `401`)

### Requirement: Minimum supported version is runtime-configurable

The minimum supported client version SHALL be sourced from an Azure App Configuration key so it
can be changed at runtime without redeploying the Games API. Changes SHALL take effect via the
App Configuration refresh within 5 minutes, without restarting the service. The key SHALL be
written by the app promotion workflow (see `app-update-policy`) or by an operator, and SHALL NOT
be overwritten by infrastructure deployments.

#### Scenario: Operator raises the minimum at runtime

- **WHEN** an operator updates the minimum-version key in Azure App Configuration to a value above a client's version
- **THEN** subsequent `POST /games/version-checker` calls from that client return `409 Conflict` without a redeploy

#### Scenario: Operator clears the minimum to roll back

- **WHEN** an operator clears or lowers the minimum-version key
- **THEN** subsequent calls from previously-blocked clients return `204 No Content`

#### Scenario: Change picked up while running

- **WHEN** the minimum-version key is changed while the Games API is running and serving requests
- **THEN** version checks made more than 5 minutes after the change use the new value

### Requirement: Version check is observable

The version-check handler SHALL emit an OpenTelemetry activity using the Games activity source,
tagged with a low-cardinality outcome value. It SHALL NOT use the raw client version string as a
metric dimension.

#### Scenario: Activity records the outcome

- **WHEN** a version check completes
- **THEN** an activity is recorded whose outcome tag is one of `up_to_date` or `update_required`

### Requirement: Update message links to the Play Store

When the version gate blocks the app, the displayed update message SHALL include an action that
opens the app's Play Store listing, in the Play Store app when it is installed and in a browser
otherwise. The store URL SHALL be sourced from client configuration rather than hard-coded in page
logic.

#### Scenario: Opening the store

- **WHEN** the update message is shown and the user activates the update action
- **THEN** the app's Play Store listing is opened

#### Scenario: No Play Store app

- **WHEN** the update action is activated on a device without the Play Store app
- **THEN** the app's Play Store listing is opened in a browser

### Requirement: The app checks its version while it is open

The app SHALL post its own version to `POST /games/version-checker` when it starts, every time it
returns to the foreground, and every 10 minutes while it is in the foreground. The check SHALL work
signed in and signed out.

#### Scenario: Cold start

- **WHEN** the app is started
- **THEN** a version check is made

#### Scenario: App already open

- **WHEN** the app stays in the foreground and the minimum version is raised above its version
- **THEN** the app learns that an update is required within 10 minutes of the backend picking up the new minimum

#### Scenario: Back to the foreground

- **WHEN** the app returns from the background
- **THEN** a version check is made

### Requirement: A required update blocks the whole app

When the version check returns `409 Conflict` and the player is not in an unfinished game, the app
SHALL show a full-screen update message over every screen. The message SHALL NOT be dismissable;
only the update action and leaving the app SHALL be possible. Text SHALL be available in English
and Dutch.

#### Scenario: Update required on start

- **WHEN** the app starts, the check returns `409` and the player is in no game
- **THEN** the update message is shown and no other screen can be used

#### Scenario: Update required while browsing

- **WHEN** a signed-in player is on the main menu, the playfields or the settings and a check returns `409`
- **THEN** the update message replaces the screen

#### Scenario: Signed-out user

- **WHEN** the check returns `409` and nobody is signed in
- **THEN** the update message is shown

#### Scenario: Back pressed

- **WHEN** the update message is shown and the user presses back
- **THEN** the message stays or the app is left; no other screen is shown

### Requirement: Players in an unfinished game finish it on the old version

When the version check returns `409 Conflict` and the player belongs to a game that has not ended
(lobby, ready, started or in progress), the app SHALL NOT block. It SHALL keep checking and SHALL
show the update message as soon as the player no longer belongs to an unfinished game, except while
the result of the game that just ended is on screen.

#### Scenario: Update required during a game

- **WHEN** a check returns `409` while the player is playing a running game
- **THEN** no update message is shown and the game continues

#### Scenario: Update required in a lobby

- **WHEN** a check returns `409` while the player waits in a lobby
- **THEN** no update message is shown and the player can stay in the lobby and play the game

#### Scenario: Game ends

- **WHEN** the game the player was exempted for ends and the player leaves its result
- **THEN** the update message is shown

#### Scenario: Cannot tell whether the player is in a game

- **WHEN** a check returns `409` and the app cannot reach the server to ask for the player's active game
- **THEN** no update message is shown and the app asks again at the next check

### Requirement: The gate fails open

Only an HTTP `409 Conflict` from the version check SHALL lead to the update message. A `204`, a
`400`, a `404`, any other error status, a network error or an unreadable response SHALL leave the
app usable, and a later successful `204` SHALL remove a shown update message.

#### Scenario: Server unreachable

- **WHEN** the version check fails with a network error
- **THEN** the app stays usable and no update message is shown

#### Scenario: Minimum lowered again

- **WHEN** the update message is shown and a later check returns `204`
- **THEN** the update message disappears and the app is usable
