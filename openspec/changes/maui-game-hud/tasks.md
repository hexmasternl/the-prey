## 1. Client API seam — status, state, tag

- [ ] 1.1 Add client projection records in `Services/Api`: `GameStatusSnapshot` (GameDurationLeft, NextPingDuration, CurrentPingInterval, IsEndgame, PreysLeft, HunterUserId, Participants[UserId]), `GameStateSnapshot` (HunterDistanceMeters, PreyLocations), `TagCandidate` (UserId, Callsign, DistanceMeters, State) — mapping from the backend DTOs.
- [ ] 1.2 Add typed result records following the `ActiveGameResult` style: `GameStatusResult` (Success/NotFound/Forbidden/Completed/Unauthorized/Error), `GameStateResult` (Success/NotFound/Unauthorized/Error), `TagCandidatesResult` (Success(candidates, rangeMeters)/Forbidden/NotFound/Unauthorized/Error), `TagPlayerResult` (Success/Forbidden/NotFound/Conflict/Unauthorized/Error).
- [ ] 1.3 Extend `IGameApiClient` with `GetGameStatusAsync`, `GetGameStateAsync`, `GetTagCandidatesAsync`, `TagPlayerAsync`.
- [ ] 1.4 Implement the four methods in `GameApiClient`: Bearer header, routes (`GET /games/{id}/status`, `GET /games/{id}/state`, `GET /games/{id}/tag-candidates`, `POST /games/{id}/participants/{participantId}/tag`), status→result mapping (incl. `409`→Completed for status, `409`→Conflict / `403`→Forbidden / `404`→NotFound for tag), and `HttpRequestException`/`TaskCanceledException`→Error.

## 2. Geo helper

- [ ] 2.1 Add a `GeoDistance.Haversine(lat1, lon1, lat2, lon2)` helper returning metres, with unit tests against known distances.

## 3. Map/camera and tag-dialog seams

- [ ] 3.1 Add `IMapCameraController` with `void SetFollowMode(bool follow)` (consumed by the hosting map; the HUD only emits the signal).
- [ ] 3.2 Add an `ITagDialog` seam (`Task<Guid?> SelectCandidateAsync(IReadOnlyList<TagCandidate> candidates)`) abstracting the modal selection; implement it with a MAUI modal page/popup.
- [ ] 3.3 Confirm `IConfirmationDialog` is reusable for the tag confirmation prompt (reuse the existing seam).

## 4. GameHudViewModel

- [ ] 4.1 Create `ViewModels/GameHudViewModel.cs` initialized with `GameId` + `IsHunter`, depending on `IGameApiClient`, `IAccessTokenProvider`, `IGpsReader`, `IMapCameraController`, `ITagDialog`, `IConfirmationDialog`, `TimeProvider`, and `ILogger` — all behind interfaces.
- [ ] 4.2 Implement `RefreshAsync`: acquire token, fetch status (and state); map outcomes to state; `Completed` stops ticking/polling and signals the host; `Unauthorized` invalidates the token; transient failures keep the last values.
- [ ] 4.3 Implement the metrics projection: game time remaining (formatted), preys active `PreysLeft` / total (participants minus hunter), and the role-aware nearest-adversary distance (prey → `HunterDistanceMeters`; hunter → min `GeoDistance` from the device fix to `PreyLocations`; explicit unknown state otherwise).
- [ ] 4.4 Implement the local one-second tick via `TimeProvider`: decrement game-time-remaining and next-ping remaining toward zero; expose the next-ping progress fraction (`remaining / CurrentPingInterval`); re-seed from each new snapshot.
- [ ] 4.5 Implement the periodic refresh cadence anchored to `NextPingDuration` with a floor/ceiling; refresh immediately on activate and cancel on deactivate.
- [ ] 4.6 Implement `IsExpanded` with expand (tap) / collapse (button) transitions.
- [ ] 4.7 Implement the Center toggle: `IsFollowingLocation` (default on), flipping it calls `IMapCameraController.SetFollowMode`.

## 5. Tag flow orchestration

- [ ] 5.1 Implement the Tag command (enabled only when `IsHunter`): call `GetTagCandidatesAsync`; on Success with candidates open `ITagDialog`, with none show a "no preys in range" message; map Forbidden/NotFound/Unauthorized/Error.
- [ ] 5.2 On a selected candidate, call `IConfirmationDialog`; cancel aborts with no server call; confirm calls `TagPlayerAsync`.
- [ ] 5.3 Map `TagPlayerAsync` outcomes: Success (next refresh reflects reduced preys), Conflict/NotFound ("no longer in range" → allow re-open), Forbidden, Unauthorized (invalidate token), Error.

## 6. GameHudView (XAML)

- [ ] 6.1 Create `Controls/GameHudView.xaml` (+ `.xaml.cs`) bound to `GameHudViewModel`; bottom-anchored, dark semi-transparent panel.
- [ ] 6.2 Build the collapsed layout (time remaining + shrinking next-ping bar) and the expanded layout (three equal-width captioned metrics, full-width next-ping bar with timer, full-width collapse button) via VisualStateManager or bound layout on `IsExpanded`.
- [ ] 6.3 Build the right-aligned icon buttons above the HUD: the Center toggle and the hunter-only Tag button (bound visibility to `IsHunter`).
- [ ] 6.4 Use only central Colors/Styles (a semi-transparent panel color resource; no inline visual literals) and `{loc:Translate}` for every caption/label/button.

## 7. Localization & styling resources

- [ ] 7.1 Add all HUD strings (metric captions, time/distance/unknown formats, collapse, Center on/off, Tag, tag-dialog title, "no preys in range", confirmation text, and error messages) to `AppResources.resx` and the Dutch `.resx`.
- [ ] 7.2 Add HUD styles/colors (transparent panel, metric block, progress bars, icon buttons, primary/toggle states) to the central `Styles.xaml`/`Colors.xaml`.

## 8. Wiring

- [ ] 8.1 In `MauiProgram.cs`, register `GameHudViewModel`, `GameHudView`, the `ITagDialog` implementation and its modal page, and `IMapCameraController` (or document that the hosting map change provides it).

## 9. Tests

- [ ] 9.1 `GameApiClient` tests: each new method maps its status codes (incl. `409`→Completed for status, `409`→Conflict/`403`→Forbidden/`404`→NotFound for tag) and sends the Bearer header.
- [ ] 9.2 `GeoDistance` tests: known coordinate pairs return expected metres.
- [ ] 9.3 HUD metric tests: preys active/total denominator excludes the hunter; role-aware distance (prey uses server distance, hunter computes nearest from `PreyLocations`), and the unknown state when data is missing.
- [ ] 9.4 Countdown tests (`TimeProvider`): local tick decrements each second; a new snapshot re-seeds; the ping progress fraction tracks `remaining / CurrentPingInterval`.
- [ ] 9.5 Refresh tests: initial load populates; `Completed` stops ticking/polling and signals the host; `Unauthorized` invalidates the token; transient failure keeps last values.
- [ ] 9.6 Center toggle test: flipping `IsFollowingLocation` calls `IMapCameraController.SetFollowMode` with the right value.
- [ ] 9.7 Tag-flow tests (fakes): candidates → dialog selection → confirm → `TagPlayerAsync`; no-candidates path shows the message and opens no dialog; cancel makes no tag call; Conflict/NotFound allow re-open; Forbidden hidden for preys.

## 10. Build & verify

- [ ] 10.1 Build the MAUI app and run the new unit tests; ensure ≥80% coverage on the new VM/client/helper code.
- [ ] 10.2 Manually verify (once a host map view is available) collapse/expand, live countdowns, the Center toggle signal, and the hunter tag flow end to end.
