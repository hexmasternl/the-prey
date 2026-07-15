## 1. Game client model & join API method

- [ ] 1.1 Add `Services/Api/JoinGameResult.cs`: a `GameSummary(Guid Id)` projection (only if `maui-game-create-new` has not already added it — reuse the existing one) plus a `JoinGameOutcome` enum (`Success`, `InvalidCode`, `NotFound`, `Conflict`, `Unauthorized`, `Error`) and a `JoinGameResult` record with factory statics, carrying the joined `GameSummary?` on success and the backend rule `code` (string?) on `Conflict`/`InvalidCode` — mirroring `ActiveGameResult`
- [ ] 1.2 Add `JoinGameAsync(Guid gameId, string joinCode, string displayName, string accessToken, CancellationToken ct)` to `IGameApiClient`
- [ ] 1.3 Implement `JoinGameAsync` in `GameApiClient`: `POST games/{gameId}/join` with a `JoinGameRequest`-shaped JSON body (`JoinCode`, `DisplayName`, `ProfilePictureUrl = null`) and `Authorization: Bearer`; map `200`→Success (deserialize `GameDto`, project to `GameSummary(Id)`), `400`→InvalidCode (read ProblemDetails `code`), `404`→NotFound, `409`→Conflict (read ProblemDetails `code`), `401`→Unauthorized, network/timeout/unexpected→Error (catch `HttpRequestException`/`TaskCanceledException`), mirroring `GetActiveGameAsync`
- [ ] 1.4 Unit-test `GameApiClient.JoinGameAsync` mapping each status (200/400/404/409/401/network) with a mocked handler / test `HttpClient`, asserting the outgoing body carries the entered code and display name and that the `400`/`409` `code` is surfaced

## 2. Invite deep-link handling

- [ ] 2.1 Add `Services/Navigation/IInviteDeepLinkHandler.cs` + implementation: parse an incoming `Uri`, accept only `https://theprey.nl/join/{gameId}` with a valid `Guid` last segment, and on match navigate to the `join` Shell route with the game id (`GoToAsync($"join?gameId={id}")`); ignore malformed / wrong-host / wrong-path / non-guid URIs
- [ ] 2.2 Wire `App.OnAppLinkRequestReceived(Uri)` (running app) to forward the URI to `IInviteDeepLinkHandler`
- [ ] 2.3 Handle cold-start links: capture the launch `Intent` on Android (`MainActivity.OnCreate`/`OnNewIntent`) and the `NSUserActivity` continuation on iOS (`AppDelegate`), and replay them through `IInviteDeepLinkHandler` once the Shell is ready (guard on `Shell.Current`, defer if not yet available)
- [ ] 2.4 Register the Android verified App Link: add a `MainActivity` intent-filter for `android:scheme="https"`, `android:host="theprey.nl"`, `android:pathPrefix="/join"`, `android:autoVerify="true"` (VIEW + DEFAULT + BROWSABLE); add the `.well-known/assetlinks.json` content to be hosted on the domain (documented, not code)
- [ ] 2.5 Register the iOS Universal Link: add the Associated Domains entitlement `applinks:theprey.nl` to `Entitlements.plist` and the continue-user-activity wiring in `AppDelegate`; add the `apple-app-site-association` content to be hosted on the domain (documented, not code)
- [ ] 2.6 Unit-test `IInviteDeepLinkHandler` over sample URIs (valid join link; wrong host; wrong path; non-guid id; extra segments) with a mocked navigator — valid routes once with the parsed id, everything else is ignored

## 3. Join Game view model

- [ ] 3.1 Add `ViewModels/JoinGameViewModel.cs` implementing `IQueryAttributable` to receive `gameId`; hold the pending game id, the 4-digit code state, `IsSignedIn`/busy/error/signed-out states, and `CanJoin` (4-digit code AND signed in AND not busy)
- [ ] 3.2 On appear: acquire an access token via `IAccessTokenProvider`; if none, set the signed-out state (retain the game id) and expose a `LogIn` command that drives `IInteractiveLoginService.LoginAsync` → `ISessionService.TryEstablishSessionAsync`, then continues on the same page; cancelled/failed login returns to the signed-out state with the id intact
- [ ] 3.3 Enforce the 4-digit numeric code (reject non-digits, cap length at 4) in the code property/binding
- [ ] 3.4 Add a Join command (guarded by `CanJoin`): acquire an access token (none → signed-out state); source the display name via `IUserApiClient.GetCurrentUserAsync` (falling back to a default when `NotFound`, treating its `Unauthorized` like the join's); call `JoinGameAsync`; map Success→navigate to the `game` route, InvalidCode→invalid-code message (keep the code), NotFound→not-found message, Conflict→message from the rule `code`, Unauthorized→invalidate token + signed-out state, Error→error state; toggle busy around the call
- [ ] 3.5 Unit-test the VM: receives the game id; signed-out on no token; `LogIn` success continues and cancel keeps the id; `CanJoin` combinations (code length, signed-in, busy); display-name sourcing incl. `NotFound` fallback; Join maps each result correctly (Moq `IGameApiClient`/`IUserApiClient`/`IAccessTokenProvider`/login/session/navigator); no token → signed-out; `401` invalidates token

## 4. Join Game page

- [ ] 4.1 Add `Pages/JoinGamePage.xaml` (+ `.xaml.cs`): a signed-out/login-prompt region (bound to the `LogIn` command), the 4-digit numeric code field (numeric keyboard, max length 4), a busy/error region, and the `JOIN` action bound to `CanJoin` — no inline visual literals, all text localized; trigger the VM's appear/gate logic from `OnAppearing`
- [ ] 4.2 Register the `join` route → `JoinGamePage` in `AppShell.xaml.cs`; register `JoinGameViewModel`, `JoinGamePage`, and `IInviteDeepLinkHandler` in `MauiProgram`

## 5. Theme & localization resources

- [ ] 5.1 Add styles to `Resources/Styles/Styles.xaml` for the join-code field, the sign-in prompt, and the state/error regions, reusing existing `Tp*`/tactical tokens — no inline literals
- [ ] 5.2 Add localized strings (English + Dutch) for every label, prompt, and message on the page — sign-in prompt, code-field label/placeholder, JOIN caption, and the invalid-code / not-found / conflict (per rule code) / unauthorized / transient-error / no-token messages (`AppResources.resx` + per-language `.resx`); consume via `{loc:Translate}` — no hard-coded user-facing text

## 6. Verification

- [ ] 6.1 Build for Android (`dotnet build src/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj -f net10.0-android`) with 0 warnings / 0 errors; run the MAUI unit tests and confirm all pass
- [ ] 6.2 Review `JoinGamePage.xaml` for no inline color/size/border literals and no hard-coded strings (single-source-of-truth styling + localization rules); only layout properties inline
- [ ] 6.3 On device/emulator: open an `https://theprey.nl/join/{gameId}` link while the app is running and from a cold start; confirm it routes to the Join Game page for that game id, and a malformed link is ignored; when signed out, confirm the page prompts to sign in, completes login, and continues for the same game id; confirm the code field only accepts 4 digits and JOIN is disabled until 4 digits are entered; enter a valid code and confirm the join POSTs and navigates to the game/lobby route on `200`; confirm invalid-code (`400`), not-found (`404`), already-started/full (`409`), expired-session (`401`), and network paths each show their message without crashing or losing the entered code
