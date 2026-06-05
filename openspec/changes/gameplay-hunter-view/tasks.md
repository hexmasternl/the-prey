## 1. Backend — Extend GameStatusDto with Participants

- [x] 1.1 Add `ParticipantSnapshotDto` record to `HexMaster.ThePrey.Games.Abstractions` with fields `Role` (string), `Latitude` (double?), `Longitude` (double?)
- [x] 1.2 Add `Participants` array (IReadOnlyList<ParticipantSnapshotDto>) to `GameStatusDto` in `HexMaster.ThePrey.Games.Abstractions`
- [x] 1.3 Update `GetGameStatusQueryHandler` to populate `Participants` from the game's hunter and preys, mapping each participant's current location (null when not yet recorded)
- [x] 1.4 Add unit tests for `GetGameStatusQueryHandler` covering: all participants included, null coordinates when no location recorded, correct Role values

## 2. Backend — Extend SSE Stream with Prey→Hunter Location Events

- [x] 2.1 Update `RecordLocationCommandHandler` (or location-recording logic) to publish a `participant-located` event via `IGameEventBus` when a **prey** records a location (in addition to the existing hunter→preys broadcast)
- [x] 2.2 Update the SSE endpoint subscription logic to route prey `participant-located` events to the **hunter** connection only (not to other prey connections)
- [x] 2.3 Add unit/integration tests: prey location event delivered to hunter; prey location event NOT delivered to other preys

## 3. Frontend — GameHunterPage Scaffold

- [x] 3.1 Generate `GameHunterPage` at `src/ThePrey/src/app/games/game-hunter/game-hunter.page.ts` (and `.html`, `.scss`)
- [x] 3.2 Register the page route in the games routing module; add a route guard that activates `GameHunterPage` when `role === 'Hunter'` and `gameStatus === 'InProgress'`
- [x] 3.3 Inject `GamesService` and initialise the status polling cycle on `ngOnInit` using `reportingIntervalSeconds` from the first response (default 30 s)
- [x] 3.4 Wire `ngOnDestroy` to clear the polling interval and close the EventSource connection

## 4. Frontend — Leaflet Map and Playfield Overlay

- [x] 4.1 Initialise a Leaflet map in `GameHunterPage` with OpenStreetMap tiles and `--bg-void` background
- [x] 4.2 Render the playfield polygon overlay using coordinates from `GameStatusDto.PlayfieldBoundary`; stroke `#ff2f1f`, fill `rgba(255,47,31,0.10)`
- [x] 4.3 Add corner-bracket UI chrome in `--hunter` red matching the design in `designs/hunter-gameplay-view.html`

## 5. Frontend — Hunter Self-Dot

- [x] 5.1 Start `navigator.geolocation.watchPosition` with `enableHighAccuracy: true` and `maximumAge: 5000`
- [x] 5.2 Render a custom green pulsing Leaflet marker (using `--signal` #64ff00 with glow animation from the design file) at the hunter's GPS position; update marker on each position event
- [x] 5.3 Keep the map centered on the hunter self-dot while auto-follow is active; add a re-center FAB button that re-enables auto-follow after manual pan
- [x] 5.4 Show alert banner "Signal lost. Find open sky." and hide the self-dot when `watchPosition` errors

## 6. Frontend — Prey Blips

- [x] 6.1 On initial status load, iterate `Participants` where `Role === 'Prey'` and render a red flashing Leaflet marker (using `--hunter` #ff2f1f with flash animation) for each prey with non-null coordinates; skip preys with null coordinates
- [x] 6.2 Maintain a `Map<participantId, LeafletMarker>` to track active prey blips
- [x] 6.3 On SSE `participant-located` event with `participantRole: "Prey"`, move the corresponding blip to the new coordinates (or add a new blip if first location event)

## 7. Frontend — HUD Panel

- [x] 7.1 Implement the 2×2 HUD grid following the layout in `designs/hunter-gameplay-view.html`: Time Remaining (MM:SS countdown), Preys Remaining (from status), Nearest Prey Distance (Haversine metres, or `--` when unknown), Penalty indicator (`--caution` when active)
- [x] 7.2 Implement the ping-row below the grid showing a progress bar animating from full to empty over the polling interval, and a countdown value in `--hunter` red
- [x] 7.3 Update HUD values on each status poll response
- [x] 7.4 Update Nearest Prey Distance cell whenever a prey blip position changes (poll or SSE)

## 8. Frontend — SSE Integration

- [x] 8.1 Open an `EventSource` connection to `/games/{gameId}/stream` on `ngOnInit`
- [x] 8.2 Handle `participant-located` events: update prey blip position (delegate to blip logic in task 6.3)
- [x] 8.3 Handle `state-changed` and `game-ended` events: stop polling, close EventSource, navigate to game-over screen
- [x] 8.4 Implement exponential back-off reconnect (max 30 s) when the SSE connection drops

## 9. Styling and Polish

- [x] 9.1 Apply `--bg-void`/`--bg-base` dark background, Special Elite / PT Mono fonts, and `--hunter` red accent throughout `game-hunter.page.scss`
- [x] 9.2 Implement status-bar top strip with pulsing `LIVE` pill in hunter red and `HUNTER` role tag
- [x] 9.3 Verify visual output against `designs/hunter-gameplay-view.html` (polygon tint, self-dot glow, blip flash, HUD layout, corner brackets)
