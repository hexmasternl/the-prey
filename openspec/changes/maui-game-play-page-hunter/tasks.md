## 1. Client API seam — in-progress status read

- [x] 1.1 Add a `GameStatusDetails` projection record in `Services/Api` mapping from the backend `GameStatusDto`: `PlayfieldCoordinates` (lat/lon list), `Participants` (`UserId`, `LastKnownLocation`, `State`), `HunterUserId`, `GameDurationLeft`, `HunterMayMoveAt`, `IsEndgame`, `PreysLeft`.
- [x] 1.2 Add a `GetGameStatusResult` typed result (`Success(GameStatusDetails)` / `Forbidden` / `Conflict` / `NotFound` / `Unauthorized` / `Error`) following the existing `ActiveGameResult` style.
- [x] 1.3 Extend `IGameApiClient` with `GetGameStatusDetailsAsync(Guid id, string accessToken, CancellationToken ct)` (named to avoid colliding with the existing minimal `GetGameStatusAsync`), and reuse `GetGameAsync` (already present) for status/role detection.
- [x] 1.4 Implement `GetGameStatusDetailsAsync` in `GameApiClient`: Bearer header, `GET /games/{id}/status`, map `200`→`Success`, `403`→`Forbidden`, `409`→`Conflict`, `404`→`NotFound`, `401`→`Unauthorized`, and `HttpRequestException`/`TaskCanceledException`→`Error` (mirroring `GetActiveGameAsync`).

## 2. Live game-channel seam (Azure Web PubSub)

- [x] 2.1 Add a `GameStreamEvent` discriminated set in `Services/Api` (`ParticipantLocated(UserId, Latitude, Longitude, State)`, `ParticipantStatusChanged(ParticipantId, NewState)`, `StateChanged(NewState)`, `GameEnded(Outcome, SurvivorCount)`). (`GameNotificationConnection` projection not needed — reused the existing `NotificationsTokenResult(Url)`.)
- [x] 2.2 Add `IGameStreamClient` with `IAsyncEnumerable<GameStreamEvent> Subscribe(Guid gameId, string accessToken, CancellationToken ct)`.
- [x] 2.3 Reused the existing `GetNotificationsTokenAsync(Guid id, string accessToken, CancellationToken ct)` on `IGameApiClient` (already calls `GET /games/{id}/notifications/token` with the Bearer header and maps `401`/`403`/transient) rather than adding a redundant `GetNotificationsConnectionAsync`.
- [x] 2.4 Implement `IGameStreamClient` (`GameStreamClient`) over Azure Web PubSub: request a fresh connection URL (2.3), open a native WebSocket (via the existing `IWebSocketConnectionFactory`) with the `json.webpubsub.azure.v1` subprotocol, send a `joinGroup` control frame for the `{gameId}` group, and — on the join `ack` (success or `Duplicate`) — begin yielding.
- [x] 2.5 Unwrap each `{ type: "message", from: "group", data: { type, data } }` frame to its `{ type, data }` envelope and map by `type` to the matching `GameStreamEvent` (via `GameStreamEventMapper`); handle `system`/`ack` frames internally without surfacing them.
- [x] 2.6 On an unexpected socket close, re-request a fresh connection URL and reconnect with exponential backoff (1 s → 30 s), re-joining the group, until cancelled; tear down cleanly on cancellation.

## 3. Local position & heading seams

- [x] 3.1 Add `ILivePositionReader` with a continuous local-fix stream (event-based `PositionChanged`) plus start/stop, in `Services/Location`.
- [x] 3.2 Implement it (`MauiLivePositionReader`) as a thin adapter over MAUI `IGeolocation` foreground listening (best accuracy), marshalled to the main thread; denial/timeout yields no fix without throwing.
- [x] 3.3 Add `IHeadingReader` yielding the device compass heading (degrees clockwise from north) with start/stop, in `Services/Location`.
- [x] 3.4 Implement it (`MauiHeadingReader`) as a thin adapter over MAUI `ICompass`; unavailable compass yields no heading without throwing.

## 4. Navigation seam — gameplay router & outcome hand-off

- [ ] 4.1 Add a gameplay router seam that fulfils the lobby's post-start hand-off: given the resolved game, route the current user to `HunterGamePage` when they are the hunter, and to the prey destination otherwise (prey branch a placeholder/no-op until the prey change lands), with a Shell-backed implementation.
- [ ] 4.2 Add a game-outcome hand-off method to the navigator seam (invoked once on game-ended / a Completed snapshot), with a Shell-backed implementation targeting the outcome route (placeholder until the outcome change lands).

## 5. HunterGameViewModel

- [ ] 5.1 Create `ViewModels/HunterGameViewModel.cs` depending on `IGameApiClient`, `IGameStreamClient`, `ILivePositionReader`, `IHeadingReader`, the navigator seam, `IAccessTokenProvider`, `TimeProvider`, and `ILogger` — all behind interfaces for testability.
- [ ] 5.2 Implement `LoadAsync`: acquire token (no token → error state), resolve active game id via `GetActiveGameAsync`, then `GetGameAsync(id)`; map each outcome (Success/None/NotFound/Unauthorized/Error) to a distinct state; `Unauthorized` invalidates the token.
- [ ] 5.3 Implement the phase machine (`Waiting` / `HeadStart` / `Live` / `Ended`) derived from `GameDto.Status` and `HunterMayMoveAt`: `Ready`→Waiting; `InProgress` with a future may-move→HeadStart; `InProgress` past (or null) may-move→Live; `Completed`→Ended (hand off once).
- [ ] 5.4 In the in-progress phases, call `GetGameStatusAsync` to seed the snapshot; treat `Forbidden`/`Conflict` as "not live yet" (stay in Waiting/HeadStart and re-poll), not as errors.
- [ ] 5.5 Expose the playfield polygon projection (from `PlayfieldCoordinates`), the self position + heading, and the prey-blip projection (position from `LastKnownLocation`, color from `State`: Active/Passive→red, Tagged/Out→grey), excluding the hunter's own row.
- [ ] 5.6 Implement the head-start countdown: `max(0, HunterMayMoveAt − now)` on a one-second `TimeProvider` tick, formatted `mm:ss`, re-anchored from each status snapshot; reaching zero advances to `Live`. Expose the static move-early / 10-minute-penalty warning flag for the head-start phase (no penalty computation).
- [ ] 5.7 Implement live updates: on activate, subscribe to `IGameStreamClient` (Web PubSub channel) and start the position + heading readers; apply `ParticipantLocated` (upsert prey dot), `ParticipantStatusChanged` (recolor), `StateChanged` (re-poll status on the Ready→InProgress edge), and `GameEnded` (hand off once); re-poll status on reconnect; on deactivate, cancel the subscription and stop the readers.
- [ ] 5.8 Guard the game-ended hand-off with an idempotent flag so it fires exactly once (from either the stream or a `Completed` snapshot).

## 6. HunterGamePage (XAML + Mapsui code-behind)

- [ ] 6.1 Create `Pages/HunterGamePage.xaml` (+ `.xaml.cs`) bound to `HunterGameViewModel`; wire `OnAppearing`/`OnDisappearing` to the VM activate/deactivate and to starting/stopping the map, position, and heading.
- [ ] 6.2 Host a full-screen Mapsui `MapControl` in code-behind (mirroring `DefineAreaPage`): OSM tile layer plus feature layers for the playfield polygon, the self arrow, and the prey dots.
- [ ] 6.3 Redraw the playfield polygon (semi-transparent red) once from the VM projection; redraw prey dots (red/grey by state) from the VM projection; draw and rotate the self green arrow symbol from the VM position + heading (accumulate the angle so it turns the short way across 0°/360°), never plotting the hunter as a prey dot.
- [ ] 6.4 Add the waiting-for-server overlay (shown in `Waiting`) and the hunter head-start overlay (large countdown, head-start caption, red penalty warning; shown in `HeadStart`), and the busy/error regions.
- [ ] 6.5 Reserve and host the hunter HUD region at the bottom (content owned by the separate `hunter-hud` capability).
- [ ] 6.6 Use only named/implicit styles + color resources from the central Colors/Styles (map palette: playfield red, self green, prey red, caught grey) and `{loc:Translate}` for every user-facing string.

## 7. Localization & styling resources

- [ ] 7.1 Add all hunter-play strings (waiting label, head-start caption, countdown, move-early / 10-minute-penalty warning title + body, error/empty states) to `AppResources.resx` and the Dutch `.resx`.
- [ ] 7.2 Add the map marker palette (playfield red fill/outline, self green arrow, prey red, caught grey) and overlay/countdown styles to the central `Colors.xaml`/`Styles.xaml`.

## 8. Wiring

- [ ] 8.1 In `AppShell.xaml.cs`, register the hunter game play route and point the gameplay router's hunter branch at `HunterGamePage`.
- [ ] 8.2 In `MauiProgram.cs`, register `HunterGamePage`, `HunterGameViewModel`, `IGameStreamClient` (Web PubSub channel; uses the typed game `HttpClient` for the token request), `ILivePositionReader`, `IHeadingReader`, and the gameplay router / outcome navigator seams.

## 9. Tests

- [ ] 9.1 `GameApiClient` tests: `GetGameStatusAsync` maps `200/403/409/404/401`/transient correctly and sends the Bearer header.
- [ ] 9.2 `HunterGameViewModel` load tests: active→game resolution, and the None/NotFound/Unauthorized/Error states (Unauthorized invalidates the token).
- [ ] 9.3 Routing test: the gameplay router sends the hunter to `HunterGamePage` and a non-hunter to the prey destination.
- [ ] 9.4 Phase tests: `Ready`→Waiting; `InProgress` future may-move→HeadStart; `InProgress` past/null may-move→Live; `Completed`→Ended (hands off once); status `Forbidden`/`Conflict` treated as not-live-yet.
- [ ] 9.5 Head-start countdown test: value derives from `HunterMayMoveAt` via a fake `TimeProvider`, re-anchors on a new snapshot, and reaching zero advances to `Live`; the warning flag is set during HeadStart.
- [ ] 9.6 Map projection tests: prey with a location and Active state → red; Tagged/Out → grey; prey with no location → no dot; the hunter's own row is never a prey dot.
- [ ] 9.7 Live-update tests (fake `IGameStreamClient`): a `ParticipantLocated` event moves a dot; `ParticipantStatusChanged` recolors it; `StateChanged` clears Waiting; `GameEnded` hands off exactly once; deactivate cancels the subscription and stops the position/heading readers.
- [x] 9.8 Web PubSub channel test: the `IGameStreamClient` implementation requests the connection URL from `/games/{id}/notifications/token`, and maps a group-message `{ type, data }` envelope for each in-game event name to the matching `GameStreamEvent` (isolate the WebSocket behind a seam so envelope mapping + reconnect are unit-testable). (`GameStreamClientTests` + `GameStreamEventMapperTests`.)

## 10. Build & verify

- [ ] 10.1 Build the MAUI app and run the new unit tests; ensure ≥80% coverage on the new VM/client code.
- [ ] 10.2 Manually verify the start→hunter and Resume→hunter entry paths, the waiting overlay, the head-start countdown + penalty warning, the self arrow rotating with the compass, and prey dots appearing/recoloring/greying.
