## 1. Game client model & create API method

- [ ] 1.1 Add `Services/Api/CreateGameResult.cs`: a `GameSummary(Guid Id)` projection plus a `CreateGameResult` union — `Success(GameSummary)` / `Validation` / `Unauthorized` / `Error` (mirroring `ActiveGameResult`)
- [ ] 1.2 Add a `CreateGameParameters` record (playfield id + the five config values in their VM units) or pass the values directly to the client method — pick one and keep it consistent with `IGameApiClient`
- [ ] 1.3 Add `CreateGameAsync(...)` to `IGameApiClient` taking the selected playfield id, display name, the three duration minutes, and the two ping intervals **already in seconds**, plus the access token and `CancellationToken`
- [ ] 1.4 Implement `CreateGameAsync` in `GameApiClient`: `POST /games` with a `CreateGameRequest`-shaped JSON body (`EnablePreyBoundaryPenalties`/`EnableHunterBoundaryPenalty` = false, `ProfilePictureUrl` = null) and `Authorization: Bearer`; map `201`→Success (deserialize `GameDto`, project to `GameSummary(Id)`), `400`→Validation, `401`→Unauthorized, network/timeout/unexpected→Error (catch `HttpRequestException`/`TaskCanceledException`), mirroring `GetActiveGameAsync`
- [ ] 1.5 Unit-test `GameApiClient.CreateGameAsync` mapping each status with a mocked handler / test `HttpClient`

## 2. Navigation seam

- [ ] 2.1 Add a navigator interface (e.g. `IStartGameNavigator` / extend an existing one) to open the playfield picker and `await` the returned `PlayFieldSummary?`, and to navigate to the `game` route after a successful create — mirroring `IEditPlayfieldNavigator` / `ShellPlayfieldNavigator`
- [ ] 2.2 Implement it as a Shell-based navigator with a `TaskCompletionSource`/`IQueryAttributable` result channel; register in `MauiProgram`

## 3. Playfield picker view model

- [ ] 3.1 Add `ViewModels/SelectPlayfieldViewModel.cs` with `MinimumSearchLength = 3`, `DebounceDelay = 300 ms`, a `TimeProvider`, and a superseding `CancellationTokenSource` (reuse the `PlayFieldsListViewModel` pattern)
- [ ] 3.2 On open, load the user's own playfields via `IPlayFieldApiClient.GetMyPlayFieldsAsync` (access token from `IAccessTokenProvider`); expose loading / empty / error states
- [ ] 3.3 On query changes: `< 3` trimmed chars → show the own list; `≥ 3` chars (debounced) → run `SearchPublicPlayFieldsAsync(query)` and a local case-insensitive contains-filter of the own list concurrently, then merge and de-duplicate by `Id` (own entries win); expose no-results and error states
- [ ] 3.4 Handle `Unauthorized` from load or search by invalidating the cached access token and showing an error state
- [ ] 3.5 Add a `Select(PlayFieldSummary)` action that returns the chosen playfield to the caller (via the navigator result channel) and closes the picker; dismiss-without-select returns nothing
- [ ] 3.6 Unit-test the VM: own list loads on open; `< 3` chars keeps the own list; debounce fires one search; rapid keystrokes supersede; merge includes own-private + own-public + others' public; dedup keeps a doubly-matched field once with its badge; no-results, load-error, search-error, and `401`-invalidates-token paths; select returns the field

## 4. Game-configuration view model

- [ ] 4.1 Add `ViewModels/StartGameViewModel.cs` exposing the five option lists as constants (`DurationOptions` 30/60/90, `HeadstartOptions` 5/10/15, `EndgameOptions` 5/10/15, `PingOptions` 2/3/5, `EndgamePingOptions` 1/2/3/5) and the selected value for each, defaulted (30, 5, 10, 2, 1)
- [ ] 4.2 Hold the selected `PlayFieldSummary?`; expose `SelectedPlayfieldName` and `CanCreate` (playfield selected AND not busy)
- [ ] 4.3 Add a Select-Playfield command that opens the picker via the navigator and stores the returned playfield; leave the selection unchanged on dismiss
- [ ] 4.4 Add a Create command (guarded by `CanCreate`): acquire an access token via `IAccessTokenProvider` (none → error state); source the display name via `IUserApiClient.GetCurrentUserAsync` (falling back to a default when `NotFound`, treating its `Unauthorized` like the create's); **convert the two ping minutes to seconds (× 60)**, keep the three durations in minutes, call `CreateGameAsync`, and map Success→navigate to the `game` route, Validation→validation error (keep selections), Unauthorized→invalidate token + error, Error→error state; toggle busy around the call
- [ ] 4.5 Unit-test the VM: defaults; `CanCreate` combinations; the minutes→seconds conversion in the outgoing request (`120`/`60`); display-name sourcing incl. `NotFound` fallback; Save maps each result correctly (Moq `IGameApiClient`/`IUserApiClient`/`IAccessTokenProvider`/navigator); no token → error; `401` invalidates token

## 5. Pages

- [ ] 5.1 Replace `Pages/StartGamePage.xaml` (+ `.xaml.cs`) content with the five selectors (segmented/radio-style bound to the VM option lists + selected values), the playfield row (bound to `SelectedPlayfieldName`, opens the picker), a busy/error region, and the `Create Game` action bound to `CanCreate` — no inline visual literals, all text localized
- [ ] 5.2 Add `Pages/SelectPlayfieldPage.xaml` (+ `.xaml.cs`): a search bar and result list bound to `SelectPlayfieldViewModel`, with loading / empty / no-results / error visual states; tapping an item selects it — no inline visual literals, all text localized
- [ ] 5.3 Register `StartGameViewModel`, `SelectPlayfieldViewModel`, and `SelectPlayfieldPage` in `MauiProgram`, and register the picker's Shell route; ensure the existing `start-game` route resolves the new `StartGamePage`

## 6. Theme & localization resources

- [ ] 6.1 Add styles to `Resources/Styles/Styles.xaml` for the option selectors, the playfield row, the picker search bar, and the result items, reusing existing `Tp*`/tactical tokens — no inline literals
- [ ] 6.2 Add localized strings (English + Dutch) for every label, option caption, prompt, and error message on both pages (`AppResources.resx` + per-language `.resx`); consume via `{loc:Translate}` — no hard-coded user-facing text

## 7. Verification

- [ ] 7.1 Build for Android (`dotnet build src/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj -f net10.0-android`) with 0 warnings / 0 errors; run the MAUI unit tests and confirm all pass
- [ ] 7.2 Review `StartGamePage.xaml` and `SelectPlayfieldPage.xaml` for no inline color/size/border literals and no hard-coded strings (single-source-of-truth styling + localization rules); only layout properties inline
- [ ] 7.3 On device/emulator: from the menu (signed in, no active game) tap Start Game; confirm the five selectors show their defaults (30/5/10/2/1) and only offer the listed choices; open the picker and confirm it lists own playfields, a `< 3`-char query keeps the own list, a `≥ 3`-char query (after ~300 ms) returns merged own-private/own-public/others'-public matches with no duplicates, select returns to the config page showing the name; `Create Game` is disabled until a playfield is picked; creating a game POSTs with ping intervals in seconds and navigates to the game route on success; validation / expired-session / network paths show an error without crashing
