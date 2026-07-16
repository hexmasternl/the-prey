## Why

Games are private until someone is invited. The owner creates a game and gets a shareable **invite link** plus a short **join code**; they send the link to friends. Today the MAUI client has no way to receive that link: there is no deep-link handling at all (only the WebAuthenticator callback), and no page where an invited player can enter the join code and join the game. This change adds the invited-player entry point — a deep-linkable **Join Game** page that takes the shared link (carrying the game id), makes the recipient sign in if needed, lets them type the join code, and joins them into the game's lobby.

## What Changes

- **Invite deep link**: register an HTTPS **App Link / Universal Link** of the form `https://theprey.nl/join/{gameId}`. Opening it launches (or foregrounds) the app and routes to the new **Join Game** page with the game id — on a cold start as well as when the app is already running.
- **Sign-in gate**: if the recipient is **not signed in** when the join page opens, the page first drives the existing interactive login flow, preserving the pending game id, then continues on the same page. A cancelled/failed login leaves a signed-out prompt to retry; it never drops the invite.
- **Join code entry**: the page presents a **4-digit numeric** code field (the backend's `GameCode` is exactly 4 decimal digits). The **JOIN** action is enabled only when the field holds 4 digits, the user is signed in, and no request is in flight.
- **Join call**: on JOIN the app sends an authenticated `POST /games/{gameId}/join` with `JoinGameRequest(JoinCode, DisplayName, ProfilePictureUrl)`; the display name is sourced from the current user's profile (falling back to a default). On `200 OK` it navigates to the game/lobby route. `400` (invalid code), `404` (game not found), `409` (game-state rule, e.g. already started / full), `401`, and transient failures each surface as a distinct on-page state without leaving the page or losing the entered code.
- **Game client seam**: extend `IGameApiClient` with a `JoinGameAsync(...)` call that maps `200`/`400`/`404`/`409`/`401`/error to a typed result union (mirroring the existing `GetActiveGameAsync` style), including the stable machine-readable `code` the backend returns in its ProblemDetails for `400`/`409`.

## Capabilities

### New Capabilities
- `maui-game-invite-deeplink`: Registering and handling the `https://theprey.nl/join/{gameId}` App Link / Universal Link — the Android verified intent-filter, the iOS Universal Link association, extracting the game id from the link, and routing to the Join Game page both when the app is already running and on a cold start, ignoring malformed links.
- `maui-game-join`: The Join Game page reached from the invite link — the sign-in gate that drives interactive login while preserving the pending game id, the 4-digit numeric join-code entry, `JOIN` enablement (4 digits AND signed in AND not busy), sourcing the caller's display name, the authenticated `POST /games/{gameId}/join` call with its `200`/`400`/`404`/`409`/`401`/error result mapping, and navigation to the game/lobby route on success.

### Modified Capabilities
<!-- None as spec deltas. This change depends on the `main-menu-page`/`game-app-backend-service` seams (`IAccessTokenProvider`, `IInteractiveLoginService`/`ISessionService`, `IUserApiClient`, `IGameApiClient`, the `game` route) and the backend `POST /games/{id}/join` contract; those capability specs are not yet archived, so the new behaviour is captured in the new capabilities above rather than as deltas to them. -->

## Impact

- **Depends on**:
  - `main-menu-page` — provides the app's Shell, the interactive-login flow (`IInteractiveLoginService`/`ISessionService`), `IAccessTokenProvider`, and the `game` route used as the post-join destination.
  - `maui-game-create-new` / user-settings changes — provide `IUserApiClient.GetCurrentUserAsync` (source of `DisplayName`) and the result-union client style reused by `JoinGameAsync`; `GameSummary(Guid Id)` is reused if already present, otherwise added here.
  - The backend `POST /games/{id}/join` (`JoinGameByCode`) contract: `RequireAuthorization()`, body `JoinGameRequest(string JoinCode, string DisplayName, string? ProfilePictureUrl = null)`, returning `200 OK` + `GameDto`; `400` with `code = "invalid_join_code"` for a wrong code; `404` (`code = "game_not_found"`); `409` with a stable rule `code` for other game-state violations; `401` when unauthenticated. The `GameCode` is exactly **4 decimal digits** (`Game.GameCodeLength = 4`).
- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - New `Pages/JoinGamePage.xaml` (+ `.xaml.cs`): the 4-digit code field, a signed-out/login prompt region, a busy/error region, and the `JOIN` action — no inline visual literals, all text localized.
  - New `ViewModels/JoinGameViewModel.cs`: receives the game id (`IQueryAttributable`), the sign-in gate, the 4-digit code state and `CanJoin`, display-name sourcing, the join command, and result → state mapping (incl. navigation on success). Fully unit-testable (all navigation/HTTP/login/time behind interfaces).
  - `Services/Api/IGameApiClient.cs` + `GameApiClient.cs`: add `JoinGameAsync(Guid gameId, string joinCode, string displayName, string accessToken, CancellationToken ct)` → `JoinGameResult` (`Success(GameSummary)` / `InvalidCode` / `NotFound` / `Conflict(code)` / `Unauthorized` / `Error`) calling `POST /games/{id}/join`.
  - New `Services/Api/JoinGameResult.cs` (+ `GameSummary(Guid Id)` if not already added by `maui-game-create-new`).
  - New deep-link seam: `Services/Navigation/IInviteDeepLinkHandler.cs` (parse `https://theprey.nl/join/{gameId}` → route to the `join` Shell route) wired from `App.OnAppLinkRequestReceived` and platform launch/activation.
  - `Platforms/Android/AndroidManifest.xml`: a verified (`android:autoVerify="true"`) intent-filter on `MainActivity` for `https://theprey.nl/join/*`, plus the `.well-known/assetlinks.json` to be published on the domain.
  - `Platforms/iOS/Info.plist` + `Entitlements.plist`: Associated Domains (`applinks:theprey.nl`) and the `apple-app-site-association` to be published on the domain; `AppDelegate` continue-user-activity wiring.
  - `AppShell.xaml.cs`: register the `join` route → `JoinGamePage`.
  - `Resources/Styles/Styles.xaml` + `Resources/Strings/*.resx`: styles for the code field, prompts, and states, and localized strings (English + Dutch) for every label/prompt/error — no inline visual literals, no hard-coded user-facing text.
  - `MauiProgram.cs`: register `JoinGameViewModel`, `JoinGamePage`, and the deep-link handler.
- **Domain / hosting**: the `.well-known/assetlinks.json` (Android) and `apple-app-site-association` (iOS) files must be published on `theprey.nl` for link verification. Filed as a hosting task; not a code change in this repo.
- **Backend**: no changes. Reuses `POST /games/{id}/join` (`JoinGameByCode`).
- **Non-goals**: the lobby / game-in-progress UI (owned by `maui-game-lobby-page` — this change stops at navigating to the game/lobby route on success); generating or sharing the invite link from the owner's side; the numeric-only vs alphanumeric or longer join code (fixed at the backend's 4 digits); a preview of the game/playfield before joining; QR-code invites; re-join / spectator flows for a game the user already left.
