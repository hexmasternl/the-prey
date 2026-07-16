## ADDED Requirements

### Requirement: Invite link registration

The app SHALL register an HTTPS App Link (Android) and Universal Link (iOS) for invite URLs of the form `https://theprey.nl/join/{gameId}`, so that opening such a link launches or foregrounds the app instead of only opening a browser.

#### Scenario: Android verified App Link

- **WHEN** the app is installed and an `https://theprey.nl/join/{gameId}` link is opened on Android
- **THEN** the app is launched or foregrounded to handle the link (via the verified `/join` intent-filter), rather than the link being handled only by the browser

#### Scenario: iOS Universal Link

- **WHEN** the app is installed and an `https://theprey.nl/join/{gameId}` link is opened on iOS
- **THEN** the app is launched or foregrounded to handle the link (via the `applinks:theprey.nl` association), rather than the link being handled only by the browser

### Requirement: Extracting the game id from the invite link

The app SHALL extract the game id from a valid `/join/{gameId}` invite link and route to the Join Game page carrying that id.

#### Scenario: Valid invite link routes to the join page

- **WHEN** the app receives an `https://theprey.nl/join/{gameId}` link whose last path segment is a valid game id
- **THEN** the Join Game page is opened for that game id

#### Scenario: Malformed invite link is ignored

- **WHEN** the app receives a link that is not of the form `https://theprey.nl/join/{gameId}` or whose id segment is not a valid game id
- **THEN** the link is ignored and the Join Game page is not opened

### Requirement: Cold-start and running-app links

The app SHALL handle an invite link both when it launches the app from a cold start and when the app is already running.

#### Scenario: Link received while the app is running

- **WHEN** the app is already running and an invite link is received
- **THEN** the app routes to the Join Game page for the link's game id

#### Scenario: Link launches the app from cold start

- **WHEN** the app is not running and is launched by opening an invite link
- **THEN** once the app is ready it routes to the Join Game page for the link's game id
