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

### Requirement: Home and game-join screens gate their actions on the version check

The client SHALL post its local app version to `POST /games/version-checker` once when the home
(main) screen loads and once when the game-join screen loads. On both screens the action buttons
SHALL be **disabled by default** while the check is in flight, and SHALL become enabled the moment
the check resolves to anything other than `409 Conflict`. Only an HTTP `409 Conflict` response
SHALL keep the buttons disabled. For every other outcome — `204`, `404`, `400`, or a network/parse
error — the buttons SHALL be enabled (the gate fails open). These version-gate conditions apply in
addition to each screen's existing enable/disable conditions.

#### Scenario: Buttons disabled until the check returns

- **WHEN** a screen loads and the version-checker call has not yet returned
- **THEN** the screen's action buttons are disabled

#### Scenario: Update required

- **WHEN** the version-checker call returns `409 Conflict`
- **THEN** the screen's action buttons remain disabled
- **AND** a message telling the user to update the app is displayed with a link to the Play Store

#### Scenario: App is up to date

- **WHEN** the version-checker call returns `204 No Content`
- **THEN** the screen's action buttons are enabled (subject to the screen's other conditions) and no update message is shown

#### Scenario: Backend predates the endpoint

- **WHEN** the version-checker call returns `404 Not Found`
- **THEN** the screen's action buttons are enabled (subject to the screen's other conditions) and no update message is shown

#### Scenario: Network or server error

- **WHEN** the version-checker call fails with a network error or a non-409 error status
- **THEN** the screen's action buttons are enabled (subject to the screen's other conditions) and no update message is shown

#### Scenario: Gate applies to both screens

- **WHEN** the version-checker returns `409 Conflict`
- **THEN** the home screen's menu actions and the game-join screen's join action are both kept disabled with the update message and Play Store link shown

### Requirement: Update message links to the Play Store

When the version gate blocks a screen, the displayed update message SHALL include an action that
opens the app's Play Store listing. The store URL SHALL be sourced from client configuration rather
than hard-coded in page logic.

#### Scenario: Opening the store

- **WHEN** the update message is shown and the user activates the Play Store link
- **THEN** the app's Play Store listing is opened
