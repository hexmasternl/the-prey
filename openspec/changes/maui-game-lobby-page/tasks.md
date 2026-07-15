## 1. Client API seam — game reads/writes

- [ ] 1.1 Add a `GameDetails` projection record (Id, GameCode, Status, Configuration with the five values, Participants[UserId, DisplayName, IsReady, State], HunterUserId, OwnerUserId, IsOwnerPlayer, IsReadyToStart) in `Services/Api`, mapping from the backend `GameDto`.
- [ ] 1.2 Add typed result records: `GetGameResult` (Success/NotFound/Unauthorized/Error), `UpdateGameSettingsResult` (Success/Validation/Forbidden/Unauthorized/Error), `DesignateHunterResult` (Success/Forbidden/NotFound/Unauthorized/Error), `SetReadyResult` (Success/Forbidden/NotFound/Unauthorized/Error), `StartGameResult` (Success/Validation/Forbidden/Unauthorized/Error) — following the existing `ActiveGameResult` style.
- [ ] 1.3 Extend `IGameApiClient` with `GetGameAsync`, `UpdateGameSettingsAsync`, `DesignateHunterAsync`, `SetReadyAsync`, `StartGameAsync` (each taking the access token + `CancellationToken`).
- [ ] 1.4 Implement the five methods in `GameApiClient`: Bearer header, correct verb/route (`GET /games/{id}`, `PUT /games/{id}/config`, `POST /games/{id}/hunter`, `POST /games/{id}/lobby/ready`, `POST /games/{id}/start`), status→result mapping, and `HttpRequestException`/`TaskCanceledException` → `Error` (mirroring `GetActiveGameAsync`).
- [ ] 1.5 In `UpdateGameSettingsAsync`, send the three durations in minutes and the two ping intervals converted minutes→seconds (×60); map `403`→`Forbidden`, `400`→`Validation`.
- [ ] 1.6 In `StartGameAsync`, send `StartGameRequest(HunterUserId)`; map `403`→`Forbidden`, `400`→`Validation`, `404`→`NotFound`.

## 2. Live lobby stream seam

- [ ] 2.1 Add `ILobbyStreamClient` with `IAsyncEnumerable<GameDetails> Subscribe(Guid gameId, string accessToken, CancellationToken ct)` in `Services/Api`.
- [ ] 2.2 Implement it over `GET /games/{id}/lobby/stream`: parse SSE `event:`/`data:` frames, skip `heartbeat`, deserialize each real event's `data` into `GameDetails`, and reconnect on drop until cancelled.

## 3. Share seam

- [ ] 3.1 Add `IShareService` with `Task ShareTextAsync(string title, string text)` in `Services/Platform`.
- [ ] 3.2 Implement it wrapping MAUI `Share.Default.RequestAsync(new ShareTextRequest(text, title))`; treat a dismissed sheet as a no-op.
- [ ] 3.3 Add a `JoinLinkBaseUrl` option to `ThePreyClientOptions` (default `https://theprey.nl/join`) and bind it from `appsettings.json`.

## 4. Navigation seam

- [ ] 4.1 Add a gameplay hand-off method to the navigator seam (extend `IMenuNavigator` or add a dedicated lobby navigator mirroring `ShellPlayfieldNavigator`) for the post-start onward navigation, with a Shell-backed implementation.

## 5. GameLobbyViewModel

- [ ] 5.1 Create `ViewModels/GameLobbyViewModel.cs` depending on `IGameApiClient`, `ILobbyStreamClient`, `IShareService`, the navigator seam, `IAccessTokenProvider`, options, and `ILogger` — all behind interfaces for testability.
- [ ] 5.2 Implement `LoadAsync`: acquire token (no token → error state), resolve active game id via `GetActiveGameAsync`, then `GetGameAsync(id)`; map each outcome (Success/None/NotFound/Unauthorized/Error) to a distinct state; `Unauthorized` invalidates the token.
- [ ] 5.3 Expose the pass code, the five selectors seeded from `Configuration` (ping seconds→minutes ÷60), `IsOwnerPlayer`-driven editability, and a participants projection (name, role from `HunterUserId`, ready from `IsReady`).
- [ ] 5.4 Implement the owner settings-save command: build the settings parameters (durations in minutes, pings minutes→seconds), call `UpdateGameSettingsAsync`, replace state from the returned snapshot, and surface Forbidden/Validation/Unauthorized/Error.
- [ ] 5.5 Implement the owner designate-hunter command (tap a participant → `DesignateHunterAsync`), guarded so it is inert for non-owners; replace state from the snapshot.
- [ ] 5.6 Implement the non-owner SET READY command (`SetReadyAsync`), hidden/absent for the owner; replace state from the snapshot.
- [ ] 5.7 Implement the owner START OPERATION command: `CanStart` derived solely from `IsReadyToStart` (and not busy); call `StartGameAsync` with the designated `HunterUserId`; on success invoke the gameplay hand-off seam; surface rejection outcomes and let the next snapshot re-sync enablement.
- [ ] 5.8 Implement live updates: on activate, subscribe to `ILobbyStreamClient` and replace VM state from each yielded snapshot (including handing off on a started snapshot); cancel the subscription on deactivate.
- [ ] 5.9 Implement the Share command: build the localized invite text embedding the verbatim pass code and the `{JoinLinkBaseUrl}/{GameCode}` join link, then call `IShareService.ShareTextAsync`.

## 6. GameLobbyPage (XAML)

- [ ] 6.1 Create `Pages/GameLobbyPage.xaml` (+ `.xaml.cs`) bound to `GameLobbyViewModel`; wire `OnAppearing`/`OnDisappearing` to the VM activate/deactivate.
- [ ] 6.2 Build the header (pass code + Share button), the settings selectors (owner-editable / non-owner read-only), the participants list (name / role / ready with tap-to-designate for the owner), the non-owner SET READY control, the owner START OPERATION button, and busy/error regions.
- [ ] 6.3 Use only named/implicit styles + color resources from the central Colors/Styles (no inline visual literals) and `{loc:Translate}` for every user-facing string (no hard-coded text).

## 7. Localization & styling resources

- [ ] 7.1 Add all lobby strings (labels, selector captions, role/ready wording, START OPERATION, Share, error/empty states, and the invite template with code + link placeholders) to `AppResources.resx` and the Dutch `.resx`.
- [ ] 7.2 Add any lobby-specific named styles (header, selector, participant row, hunter/ready badges, primary start button) to the central `Styles.xaml`/`Colors.xaml`.

## 8. Wiring

- [ ] 8.1 In `AppShell.xaml.cs`, point the `game` route at `GameLobbyPage` and remove the placeholder `GamePage` route.
- [ ] 8.2 In `MauiProgram.cs`, register `GameLobbyPage`, `GameLobbyViewModel`, `ILobbyStreamClient` (typed HttpClient with the backend base address), `IShareService`, and the navigator seam; remove the obsolete `GamePage` registration if no longer used.

## 9. Tests

- [ ] 9.1 `GameApiClient` tests: each new method maps its status codes correctly, sends the Bearer header, and converts ping minutes→seconds in the settings request (assert 120/60 → 2/1 round-trips).
- [ ] 9.2 `GameLobbyViewModel` load tests: active→full resolution, and the None/NotFound/Unauthorized/Error states (Unauthorized invalidates the token).
- [ ] 9.3 Owner-gating tests: settings editable and designate-hunter/START only for `IsOwnerPlayer`; SET READY only for non-owners.
- [ ] 9.4 Settings-save test: outgoing pings are ×60; a returned snapshot with reset readiness disables START.
- [ ] 9.5 START tests: `CanStart` tracks `IsReadyToStart`; success invokes the hand-off seam; rejection keeps the page and re-syncs from the next snapshot.
- [ ] 9.6 Live-update tests (fake `ILobbyStreamClient`): a ready-change snapshot updates the row and START enablement; a started snapshot hands off; deactivate cancels the subscription.
- [ ] 9.7 Share test: the invite embeds the verbatim pass code and the join link, and `IShareService` is invoked; a dismissed sheet is a no-op.

## 10. Build & verify

- [ ] 10.1 Build the MAUI app and run the new unit tests; ensure ≥80% coverage on the new VM/client code.
- [ ] 10.2 Manually verify the create→lobby and Resume→lobby entry paths, owner vs non-owner behavior, ready/settings-reset flow, and native share.
