## ADDED Requirements

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
existing App Configuration refresh without restarting the service.

#### Scenario: Operator raises the minimum at runtime

- **WHEN** an operator updates the minimum-version key in Azure App Configuration to a value above a client's version
- **THEN** subsequent `POST /games/version-checker` calls from that client return `409 Conflict` without a redeploy

#### Scenario: Operator clears the minimum to roll back

- **WHEN** an operator clears or lowers the minimum-version key
- **THEN** subsequent calls from previously-blocked clients return `204 No Content`

### Requirement: Version check is observable

The version-check handler SHALL emit an OpenTelemetry activity using the Games activity source,
tagged with a low-cardinality outcome value. It SHALL NOT use the raw client version string as a
metric dimension.

#### Scenario: Activity records the outcome

- **WHEN** a version check completes
- **THEN** an activity is recorded whose outcome tag is one of `up_to_date` or `update_required`

### Requirement: Main screen disables menu and prompts to update when below minimum

The client SHALL post its local app version to `POST /games/version-checker` once when the main
(home) screen loads. The client SHALL change behavior only on an HTTP `409 Conflict` response:
it SHALL disable all main-menu actions and SHALL show a message instructing the user to update
the app in the app store. For every other outcome — `204`, `404`, `400`, or a network/parse
error — the client SHALL leave the main menu fully enabled (the gate fails open).

#### Scenario: Update required

- **WHEN** the version-checker call returns `409 Conflict`
- **THEN** all main-menu action buttons are disabled
- **AND** a message telling the user to update the app in the store is displayed

#### Scenario: App is up to date

- **WHEN** the version-checker call returns `204 No Content`
- **THEN** the main menu is fully enabled and no update message is shown

#### Scenario: Backend predates the endpoint

- **WHEN** the version-checker call returns `404 Not Found`
- **THEN** the main menu is fully enabled and no update message is shown

#### Scenario: Network or server error

- **WHEN** the version-checker call fails with a network error or a non-409 error status
- **THEN** the main menu is fully enabled and no update message is shown
