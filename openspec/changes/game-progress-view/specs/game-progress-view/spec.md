## ADDED Requirements

### Requirement: Game Progress view shows a map with the playfield
The app SHALL provide a Game Progress page that is shown while a game session is running. Its main surface SHALL be a map (OpenStreetMap tiles via the existing Mapsui integration) with the game's playfield polygon rendered as a semi-transparent overlay. The playfield geometry SHALL be resolved from the local playfield cache by the game's playfield identifier. The map SHALL initially be fitted to the playfield bounds.

#### Scenario: Playfield rendered transparently
- **WHEN** the Game Progress page opens for a game whose playfield is in the local cache
- **THEN** the map shows the playfield polygon with a semi-transparent fill and a visible outline, fitted into view

#### Scenario: Playfield not cached
- **WHEN** the Game Progress page opens and the playfield is not present in the local cache
- **THEN** the map renders without an overlay and the rest of the page functions normally

### Requirement: Map shows the local player as a green dot
The map SHALL render a green dot at the local player's current location, taken from `GameStateContext.CurrentLocation`. The dot SHALL move whenever `CurrentLocation` changes. While `CurrentLocation` is null (no GPS fix yet), no dot SHALL be drawn.

#### Scenario: Own location shown
- **WHEN** the game engine publishes a GPS fix to `GameStateContext.CurrentLocation`
- **THEN** the map shows a green dot at that coordinate

#### Scenario: Own location updates
- **WHEN** `CurrentLocation` changes after a later GPS fix
- **THEN** the green dot moves to the new coordinate

#### Scenario: No fix yet
- **WHEN** the page opens before any GPS fix has been acquired
- **THEN** no green dot is drawn until the first fix arrives

### Requirement: Hunter sees prey positions as red dots
When the local player's role is Hunter, the map SHALL render one red dot for each entry in `GameStateContext.PreyLocations` at its last known coordinate, refreshing whenever the collection is replaced by the sync loop. Prey players SHALL NOT see red dots.

#### Scenario: Prey dots for the hunter
- **WHEN** the player is the hunter and the state sync delivers two prey locations
- **THEN** the map shows two red dots at those coordinates alongside the hunter's own green dot

#### Scenario: Prey dots refresh
- **WHEN** a later sync replaces the prey locations
- **THEN** the red dots move to the new coordinates

#### Scenario: No red dots for prey players
- **WHEN** the player is a prey
- **THEN** the map shows only the player's own green dot

### Requirement: HUD shows the remaining game time
The bottom HUD SHALL show the remaining game time as a minutes-and-seconds countdown derived from `GameStateContext.GameEndsAt` against the current UTC time, updating every second and clamping at 00:00. While `GameEndsAt` is null, the HUD SHALL show a dash.

#### Scenario: Countdown ticks
- **WHEN** `GameEndsAt` is 9 minutes 30 seconds in the future
- **THEN** the HUD shows 09:30 and counts down each second

#### Scenario: Game time exhausted
- **WHEN** the current time passes `GameEndsAt`
- **THEN** the HUD shows 00:00 and does not go negative

#### Scenario: End time unknown
- **WHEN** no state sync has delivered `GameEndsAt` yet
- **THEN** the HUD shows a dash for the remaining game time

### Requirement: HUD shows the remaining location send time
The bottom HUD SHALL show the time until the next location push as a minutes-and-seconds countdown derived from `GameStateContext.NextLocationPushDueAt`, updating every second and clamping at 00:00. While `NextLocationPushDueAt` is null, the HUD SHALL show a dash.

#### Scenario: Send countdown ticks
- **WHEN** the engine schedules the next push 45 seconds from now
- **THEN** the HUD shows 00:45 counting down

#### Scenario: Send time unknown
- **WHEN** no push has completed yet
- **THEN** the HUD shows a dash for the location send countdown

### Requirement: Prey HUD shows the hunter distance in red
When the player's role is Prey, the HUD SHALL show the distance to the hunter in meters, rendered in red, taken from `GameStateContext.HunterDistanceMeters`, together with a small caption stating how long ago the value was measured (derived from `GameStateContext.LastStateSyncAt`). When the distance is null, the HUD SHALL show a dash and no caption.

#### Scenario: Hunter distance shown
- **WHEN** the player is a prey and the last sync delivered a hunter distance of 250 meters 8 seconds ago
- **THEN** the HUD shows "250 m" in red with a small "8s ago"-style caption

#### Scenario: Hunter distance unknown
- **WHEN** the player is a prey and `HunterDistanceMeters` is null
- **THEN** the HUD shows a dash instead of a distance

### Requirement: Hunter HUD shows the nearest prey distance
When the player's role is Hunter, the HUD SHALL show the distance in meters to the nearest prey, computed on-device as the minimum haversine distance between `GameStateContext.CurrentLocation` and the entries of `GameStateContext.PreyLocations`. When either the own location or the prey list is unavailable/empty, the HUD SHALL show a dash.

#### Scenario: Nearest prey distance shown
- **WHEN** the player is the hunter, has a current location, and two preys are 120 m and 480 m away
- **THEN** the HUD shows 120 m

#### Scenario: Nearest prey unknown
- **WHEN** the hunter has no GPS fix yet or the prey list is empty
- **THEN** the HUD shows a dash

### Requirement: Page lifecycle drives the game engine
The Game Progress page SHALL start the game engine (`IGameEngineService.StartAsync(gameId, role)`) when it appears, relying on the engine's idempotent start. When `GameStateContext.GameEnded` becomes true, the page SHALL inform the player with a localized message, stop the engine, and navigate back to the main menu. An explicit leave action SHALL likewise stop the engine before navigating away.

#### Scenario: Engine starts with the page
- **WHEN** the Game Progress page appears for a started game
- **THEN** the game engine is started with the game id and the local player's role

#### Scenario: Game ends
- **WHEN** `GameStateContext.GameEnded` becomes true while the page is shown
- **THEN** the player sees a localized game-ended message and the app stops the engine and returns to the main menu

#### Scenario: Player leaves the game
- **WHEN** the player uses the leave action
- **THEN** the engine is stopped before navigating away

### Requirement: All HUD texts are localized
All user-visible strings of the Game Progress page (labels, captions, the game-ended message, the leave action) SHALL be provided via `AppLocalizer` with entries in both the English and Dutch resource files.

#### Scenario: Dutch locale
- **WHEN** the device language is Dutch
- **THEN** all HUD labels and messages appear in Dutch
