## 1. Reuse check — seams from the hunter change

- [ ] 1.1 Confirm the reusable seams from `maui-game-play-page-hunter` are present and referenced (no new backend calls): `IGameApiClient.GetGameStatusAsync` + `GameStatusDetails`, `GetGameAsync`/`GetActiveGameAsync`, `IGameStreamClient` (Web PubSub channel) + `GameStreamEvent`, `ILivePositionReader`, `IHeadingReader`, the shared head-start countdown overlay, and the outcome-hand-off navigator. If this change is implemented before the hunter change is applied, add those seams here first (per the hunter change's tasks).
- [ ] 1.2 Confirm `GameStatusDetails.Participants` carries `UserId`, `LastKnownLocation`, and `State`, and that the projection exposes `HunterUserId` and `HunterMayMoveAt` — everything the prey map needs. Extend only if a field is missing.

## 2. PreyGameViewModel

- [ ] 2.1 Create `ViewModels/PreyGameViewModel.cs` depending on `IGameApiClient`, `IGameStreamClient`, `ILivePositionReader`, `IHeadingReader`, the navigator seam, `IAccessTokenProvider`, `TimeProvider`, and `ILogger` — all behind interfaces for testability (mirroring `HunterGameViewModel`).
- [ ] 2.2 Implement `LoadAsync`: acquire token (no token → error state), resolve active game id via `GetActiveGameAsync`, then `GetGameAsync(id)`; map each outcome (Success/None/NotFound/Unauthorized/Error) to a distinct state; `Unauthorized` invalidates the token.
- [ ] 2.3 Implement the phase machine (`Waiting` / `HeadStart` / `Live` / `Ended`) from `GameDto.Status` and `HunterMayMoveAt`, identical to the hunter VM: `Ready`→Waiting; `InProgress` future may-move→HeadStart; `InProgress` past/null may-move→Live; `Completed`→Ended (hand off once).
- [ ] 2.4 In the in-progress phases, call `GetGameStatusAsync` to seed the snapshot; treat `Forbidden`/`Conflict` as "not live yet" (stay in Waiting/HeadStart and re-poll), not as errors.
- [ ] 2.5 Expose the green playfield-polygon projection (from `PlayfieldCoordinates`), the self position + heading, and the head-start countdown via `TimeProvider` **with the hunter-must-not-move / penalty warning shown** (prey-framed localized text; same warning flag as the hunter overlay).
- [ ] 2.6 Implement the **blip projection**: skip `UserId == self`; `UserId == HunterUserId` → red; other prey Active/Passive → green; other prey Tagged/Out → grey; no `LastKnownLocation` → no dot. Expose as a pure function of `(isHunter, isSelf, State, hasLocation)`.
- [ ] 2.7 Implement live updates: on activate, subscribe to `IGameStreamClient` (Web PubSub channel) and start the position + heading readers; apply `ParticipantLocated` (upsert a blip — chiefly the hunter's live position), `ParticipantStatusChanged` (recolor; self Tagged/Out → spectator, see 2.8), `StateChanged` (re-poll on the Ready→InProgress edge), and `GameEnded` (hand off once); re-poll status on the server-driven cadence to refresh other-prey dots and on reconnect; on deactivate, cancel the subscription and stop the readers.
- [ ] 2.8 Implement the **spectator** transition: when a status change (or snapshot) reports **self** as Tagged/Out while the game runs, set a `Spectating` flag but keep the channel, re-poll, and (server-side) reporting alive; do **not** hand off — hand off only on `GameEnded`/`Completed`.
- [ ] 2.9 Guard the game-ended hand-off with an idempotent flag so it fires exactly once (from either the channel or a `Completed` snapshot).

## 3. PreyGamePage (XAML + Mapsui code-behind)

- [ ] 3.1 Create `Pages/PreyGamePage.xaml` (+ `.xaml.cs`) bound to `PreyGameViewModel`; wire `OnAppearing`/`OnDisappearing` to the VM activate/deactivate and to starting/stopping the map, position, and heading (mirroring `HunterGamePage`).
- [ ] 3.2 Host a full-screen Mapsui `MapControl` in code-behind: OSM tile layer plus feature layers for the green playfield polygon, the green self arrow, and the player dots.
- [ ] 3.3 Redraw the playfield polygon (semi-transparent green) once; redraw player dots (hunter red / other-prey green / caught grey) from the VM projection; draw and rotate the green self arrow from the VM position + heading (accumulate the angle so it turns the short way across 0°/360°), never plotting the self as a dot.
- [ ] 3.4 Add the waiting-for-server overlay (shown in `Waiting`) and the shared head-start overlay **with the hunter-penalty warning shown** in its prey-framed wording (non-blocking, shown in `HeadStart`), the spectator indication (shown when `Spectating`), and busy/error regions.
- [ ] 3.5 Reserve and host the prey HUD region at the bottom (content owned by the separate `prey-hud` capability).
- [ ] 3.6 Use only named/implicit styles + color resources from the central Colors/Styles (map palette: playfield green, self green, other-prey green, hunter red, caught grey) and `{loc:Translate}` for every user-facing string.

## 4. Navigation wiring

- [ ] 4.1 Point the gameplay router's **prey branch** at `PreyGamePage`, replacing the hunter change's placeholder prey destination.

## 5. Localization & styling resources

- [ ] 5.1 Add all prey-play strings (waiting label, head-start caption, the prey-framed hunter-must-not-move / 10-minute-penalty warning, spectator indication, error/empty states) to `AppResources.resx` and the Dutch `.resx`; reuse shared overlay/countdown strings where applicable.
- [ ] 5.2 Add the prey map marker palette (playfield green fill/outline, self green arrow, other-prey green, hunter red, caught grey) to the central `Colors.xaml`/`Styles.xaml`; reuse the shared overlay/countdown styles from the hunter change.

## 6. Wiring

- [ ] 6.1 In `AppShell.xaml.cs`, register the prey game play route and point the router's prey branch at `PreyGamePage`.
- [ ] 6.2 In `MauiProgram.cs`, register `PreyGamePage` and `PreyGameViewModel`; reuse the already-registered `IGameStreamClient`, `ILivePositionReader`, `IHeadingReader`, status client, and navigator seams.

## 7. Tests

- [ ] 7.1 Routing test: the gameplay router sends a non-hunter to `PreyGamePage` (and the hunter to `HunterGamePage`).
- [ ] 7.2 `PreyGameViewModel` load tests: active→game resolution, and the None/NotFound/Unauthorized/Error states (Unauthorized invalidates the token).
- [ ] 7.3 Phase tests: `Ready`→Waiting; `InProgress` future may-move→HeadStart (no penalty warning); `InProgress` past/null may-move→Live; `Completed`→Ended (hands off once); status `Forbidden`/`Conflict` treated as not-live-yet.
- [ ] 7.4 Blip projection tests: hunter → red; other prey Active/Passive → green; Tagged/Out → grey; no location → no dot; the prey's own row is never a dot.
- [ ] 7.5 Live-update tests (fake `IGameStreamClient`): a `ParticipantLocated` for the hunter moves the red dot; a re-poll snapshot moves other-prey dots; `ParticipantStatusChanged` recolors a dot; `StateChanged` clears Waiting; `GameEnded` hands off exactly once; deactivate cancels the subscription and stops the readers.
- [ ] 7.6 Spectator tests: a self Tagged/Out status change sets `Spectating` and keeps the connections alive without handing off; a subsequent `GameEnded` hands off.
- [ ] 7.7 Head-start test: the countdown derives from `HunterMayMoveAt` via a fake `TimeProvider` and re-anchors on a new snapshot; the prey overlay shows the hunter-penalty warning (prey-framed text) during `HeadStart`.

## 8. Build & verify

- [ ] 8.1 Build the MAUI app and run the new unit tests; ensure ≥80% coverage on the new VM code.
- [ ] 8.2 Manually verify the start→prey and Resume→prey entry paths, the green polygon, green other-prey dots, red hunter dot, grey caught dots, the self green arrow rotating with the compass, the head-start countdown with the prey-framed hunter-penalty warning, and spectator mode after being tagged.
