## Context

The MAUI client (`src/HexMaster.ThePrey.Maui.App`) has no deep-link handling today — only the WebAuthenticator OAuth callback is wired. An invited player needs a way to open a shared invite link, land on a page that carries the game id, sign in if necessary, enter the game's join code, and join the lobby. This change adds that invited-player entry point.

The backend contract already exists and is authoritative. `POST /games/{id}/join` (`JoinGameByCode`, `RequireAuthorization()`) takes body `JoinGameRequest(string JoinCode, string DisplayName, string? ProfilePictureUrl = null)` and returns `200 OK` with a `GameDto`. The endpoint resolves the caller from the `sub` claim (→ `401` when absent/unresolved), returns `404` (ProblemDetails `code = "game_not_found"`) when the game does not exist, `400` (`code = "invalid_join_code"`) for a wrong code, and `409` with a stable rule `code` for any other game-state violation (e.g. the game already started or is full). The shareable `GameCode` is **exactly 4 decimal digits** (`Game.GameCodeLength = 4`, validated as 4 ASCII digits).

The client seams this builds on already exist: `IAccessTokenProvider` (bearer token + `Invalidate()`), the interactive-login flow (`IInteractiveLoginService` / `ISessionService.TryEstablishSessionAsync`) used by `MainMenuViewModel`, `IUserApiClient.GetCurrentUserAsync` (source of `DisplayName`), and `IGameApiClient` (currently only `GetActiveGameAsync`). The result-union client style (`ActiveGameResult` with an outcome enum + factory statics) is the established pattern to mirror. The `game` Shell route is the post-join destination (the lobby UI itself is owned by `maui-game-lobby-page`).

## Goals / Non-Goals

**Goals:**
- An HTTPS App Link / Universal Link `https://theprey.nl/join/{gameId}` that opens the app and routes to the Join Game page with the game id — on cold start and while already running.
- A Join Game page that gates on sign-in (driving interactive login while preserving the pending game id), accepts a 4-digit numeric join code, and joins via `POST /games/{gameId}/join`.
- Typed result mapping for `200`/`400`/`404`/`409`/`401`/error, each rendered as a distinct on-page state; navigation to the game/lobby route on success.
- The view model fully unit-testable without platform/HTTP/login (all behind interfaces / `TimeProvider` where needed).

**Non-Goals:**
- The lobby, countdown, and game-in-progress UI — this change stops at navigating to the `game` route on success (owned by `maui-game-lobby-page`).
- Generating or sharing the invite link from the owner's side; QR invites.
- Changing the join-code shape (fixed at the backend's 4 decimal digits); a game/playfield preview before joining; re-join/spectator flows.

## Decisions

### D1: Invite link is an HTTPS App Link / Universal Link `https://theprey.nl/join/{gameId}`
The invite is a normal `https://theprey.nl/join/{gameId}` URL so it is shareable and openable in a browser as a fallback. On Android it is a **verified App Link**: a `MainActivity` intent-filter for `android:scheme="https"`, `android:host="theprey.nl"`, `android:pathPrefix="/join"` with `android:autoVerify="true"`, backed by a `.well-known/assetlinks.json` published on the domain. On iOS it is a **Universal Link**: an Associated Domains entitlement `applinks:theprey.nl` plus an `apple-app-site-association` published on the domain, with the link delivered via `NSUserActivity` continuation in `AppDelegate`.

- **Why:** App/Universal Links give a clean, trusted, browser-fallback-friendly invite the user chose over a custom scheme; verification stops other apps hijacking the link.
- **Consequence:** the two association files (`assetlinks.json`, `apple-app-site-association`) must be published on `theprey.nl` — a hosting task, tracked in the proposal, outside this repo's code. Until published, links open the browser instead of the app; the flow still degrades gracefully.
- **Alternative:** a custom `theprey://` scheme — rejected by the product owner in favour of HTTPS links.

### D2: A thin deep-link handler seam parses the link and routes to the `join` Shell route
`App.OnAppLinkRequestReceived(Uri)` (and the platform cold-start activation) forward the incoming `Uri` to an `IInviteDeepLinkHandler`. The handler validates the URI shape (`/join/{guid}`), and on a valid match calls `Shell.Current.GoToAsync($"join?gameId={id}")`; malformed or non-`/join` URIs are ignored. `JoinGamePage`'s view model receives the id via `IQueryAttributable`.

- **Why:** keeps URI parsing and routing testable in one place and off the platform types; the page/VM only ever sees a `Guid`.
- **Testability:** `IInviteDeepLinkHandler` is unit-tested over sample URIs (valid, wrong host/path, non-guid) with a mocked navigator; the platform glue is a thin pass-through.

### D3: Cold-start and running-app links both resolve
A link received while the app is running arrives via `OnAppLinkRequestReceived`. A link that **launches** the app (cold start) is captured from the platform activation (Android `Intent` on `MainActivity.OnCreate`/`OnNewIntent`, iOS `NSUserActivity` in `AppDelegate`) and replayed through the same `IInviteDeepLinkHandler` once the Shell is ready. If a link arrives before the Shell/session is initialised, the handler defers the `GoToAsync` until the first navigation is possible.

- **Why:** invites are most often opened from a cold app; dropping the cold-start case would make the feature feel broken.
- **Risk:** racing the Shell initialisation → the handler guards on `Shell.Current` availability and replays the pending link after startup.

### D4: Sign-in gate lives in the Join Game view model, preserving the pending game id
When the join page appears, the VM acquires an access token via `IAccessTokenProvider`. If none is available (signed out), it sets a **signed-out** state and, on a `LogIn` command (or automatically on appear), drives the same interactive login used by the menu (`IInteractiveLoginService.LoginAsync` → `ISessionService.TryEstablishSessionAsync`). The pending `gameId` is held on the VM throughout, so after a successful login the page continues with the code entry for the same game. A cancelled/failed login returns to the signed-out state; the invite is never lost.

- **Why:** the description requires "when not logged in, the user must first log in"; gating in the VM keeps the whole flow testable and avoids a separate interstitial page.
- **Alternative:** redirect to the menu's login then back — rejected as more navigation and a lost pending id.

### D5: 4-digit numeric join-code entry; `CanJoin` is derived
The code field accepts **exactly 4 decimal digits** (numeric keyboard, max length 4, non-digits rejected), matching `Game.GameCodeLength`. `CanJoin` = the code is 4 digits **AND** the user is signed in **AND** not busy. No client-side check of the code's correctness beyond length — the server is authoritative and returns `400 invalid_join_code` for a wrong code.

- **Why:** the backend fixes the code at 4 digits; a length guard gives immediate feedback while leaving correctness to the server. (The user confirmed 4 digits over the originally-described "6 characters".)

### D6: `JoinGameAsync` mirrors the existing result-union client pattern
Add to `IGameApiClient`: `Task<JoinGameResult> JoinGameAsync(Guid gameId, string joinCode, string displayName, string accessToken, CancellationToken ct)`. `GameApiClient` POSTs a `JoinGameRequest`-shaped JSON body (`ProfilePictureUrl = null`) with a Bearer header to `games/{gameId}/join` and maps: `200`→`Success` (deserialize `GameDto`, project to `GameSummary(Id)`), `400`→`InvalidCode` (read the ProblemDetails `code`), `404`→`NotFound`, `409`→`Conflict(code)` (carry the stable rule `code` for a localized message), `401`→`Unauthorized`, network/timeout/other→`Error` (catch `HttpRequestException`/`TaskCanceledException`) — identical style to `GetActiveGameAsync`. `JoinGameResult` is an outcome enum + payload record with factory statics (`Success`, `InvalidCode`, `NotFound`, `Conflict`, `Unauthorized`, `Error`).

- **Why:** consistency with the existing seam; the VM renders each outcome as a distinct state. Carrying the `409` `code` lets the page show why the join failed (already started / full / …) without parsing prose.

### D7: Display name sourced from the current user, resolved at join time
`DisplayName` is read via `IUserApiClient.GetCurrentUserAsync` when the join command runs (not stored on the page). `NotFound` falls back to a sensible default display name so the join still succeeds; `Unauthorized` from this call is treated like the join's `Unauthorized` outcome (invalidate token, prompt sign-in).

- **Why:** the join request needs a display name the page does not otherwise collect; sourcing it at join time avoids a stale copy, matching the create-game flow.

### D8: On success, navigate to the game/lobby route
A successful join navigates to the `game` Shell route (the post-join destination; the lobby UI is owned by `maui-game-lobby-page`). This change does not render the lobby itself.

- **Why:** keeps the join change decoupled from the lobby page while satisfying "on success, redirected to the game lobby page".

## Risks / Trade-offs

- **Association files not yet hosted** → until `assetlinks.json` / `apple-app-site-association` are published on `theprey.nl`, links open the browser rather than the app. Tracked as a hosting task; the app code is ready and degrades gracefully.
- **Cold-start link race with Shell init** (D3) → guard on `Shell.Current` and replay the pending link after startup; covered by handling both `OnCreate`/`OnNewIntent` and the running-app callback.
- **Signed-out recipient loses the invite** (D4) → the `gameId` is held on the VM across the login round-trip; a cancelled login keeps the signed-out state with the id intact so the user can retry.
- **`200 OK` but the returned `GameDto` fails to deserialize** → treat as `Error`; the server did add the player, so the user can re-enter via the menu's Resume path (the active-game check will now find it). Documented, not silently dropped.
- **Wrong / stale code, or a game that already started or is full** → mapped to `InvalidCode` (`400`) and `Conflict(code)` (`409`) states with localized messages; the page stays open with the entered code so the user can correct it.
- **Malformed or hijacked links** → the handler validates the `/join/{guid}` shape and ignores anything else; App-Link verification (D1) prevents other apps claiming the URL.

## Migration Plan

Pure client addition plus two static association files to host on `theprey.nl`. No backend, schema, or contract changes. The new `join` route and page are additive; the deep-link intent-filter/entitlement only add a new way to launch the app. No rollback concerns beyond reverting the client change and removing the hosted association files.

## Open Questions

- Exact fallback display name when the profile read returns `NotFound` (e.g. a localized "Player") — cosmetic, resolved during implementation; does not change the VM contract.
- Whether, once signed in, the page should auto-attempt login on appear or wait for an explicit tap — default to prompting with an explicit `LogIn` action to avoid a surprise browser tab; revisited during implementation without changing the VM contract.
- Whether to show any game metadata (name/owner) on the join page before joining — deferred (Non-Goal); would need a pre-join read the current contract does not provide to non-members.
