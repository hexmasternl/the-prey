## 1. Prerequisites & configuration

- [x] 1.1 Provision a native/mobile Auth0 application in the `theprey.eu.auth0.com` tenant; record its client ID and register the custom-scheme callback URL (e.g. `theprey://callback`) as an Allowed Callback URL (and enable `offline_access` / refresh-token grant with rotation)
- [x] 1.2 Add a `ThePreyClientOptions` config type (Auth0 domain `https://theprey.eu.auth0.com/`, client ID, audience `https://api.theprey.nl`, redirect URI, backend base URL `https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io`) and populate it from an embedded `appsettings.json` MauiAsset (no secrets committed)

## 2. Project setup & dependencies

- [x] 2.1 Add the `Microsoft.Extensions.Http` package reference to `HexMaster.ThePrey.Maui.App.csproj`
- [x] 2.2 Add tactical monospace fonts (Special Elite / PT Mono equivalents) to `Resources/Fonts` and reference them via `MauiFont`
- [x] 2.3 Register fonts, `ThePreyClientOptions`, `HttpClient`, and the new services/pages/view models in `MauiProgram.CreateMauiApp`

## 3. Tactical theme resources

- [x] 3.1 Add The Prey palette tokens to `Resources/Styles/Colors.xaml` (e.g. `TpBgVoid #0c0e0c`, `TpBgBase #181b17`, `TpBgSurface #23271f`, `TpLine #39402f`, `TpSignal #64ff00`, `TpText #dcf6d2`, `TpTextSoft #8c9a83`, `TpHunter #ff2f1f`, `TpCaution #ffb300`)
- [x] 3.2 Add reusable styles in `Resources/Styles/Styles.xaml` for tactical labels (uppercase, letter-spaced PT Mono body; Special Elite display/headings) and page background

## 4. Token & session infrastructure

- [x] 4.1 Implement `ITokenStore` + `SecureStorageTokenStore` (read/write/clear refresh token via `SecureStorage`, graceful failure handling)
- [x] 4.2 Implement `IAuth0TokenClient` with a `RefreshAsync(refreshToken)` method: POST `oauth/token` `grant_type=refresh_token` with client ID + audience; parse access token and rotated refresh token; distinguish transient failure from Auth0 rejection (`invalid_grant`)
- [x] 4.3 Add `ExchangeCodeAsync(code, verifier)` to `IAuth0TokenClient` for the PKCE code-for-token exchange used by interactive login
- [x] 4.4 Implement `IGameApiClient.GetActiveGameAsync(accessToken)` (typed `HttpClient`) calling `GET /games/active`; map `200`→active (deserialize `GameStatusDto`), `404`→none, `401`→unauthenticated; apply an HTTP timeout
- [x] 4.5 Implement `ISessionService.TryEstablishSessionAsync()` returning a discriminated result (`ActiveGame` / `NoActiveGame` / `Unauthenticated`): no refresh token → unauthenticated; refresh → persist rotated token, clear on rejection; on access token, call the game API and map the result

## 5. Welcome screen

- [x] 5.1 Create `WelcomePage` (XAML) styled to the tactical theme — dark base, logo, `Special Elite` title, uppercase PT Mono status label, corner-bracket chrome + signal glow, and an `ActivityIndicator`
- [x] 5.2 Implement `WelcomeViewModel` running the bootstrap on appearing: show progress + status, call `ISessionService`, then navigate to game / home / login by result
- [x] 5.3 Replace the template `MainPage` with `WelcomePage` as the app's start page (`AppShell` route) and delete the template counter content
- [x] 5.4 Add placeholder `HomePage` (main menu) and `GamePage` (active-game landing) navigation stubs as routing destinations

## 6. Interactive login

- [x] 6.1 Create `LoginPage` (XAML, tactical theme) with a "LOG IN" call-to-action and demanding-login messaging, plus busy/error states
- [x] 6.2 Implement `LoginViewModel`: build the Auth0 `/authorize` URL (PKCE `code_challenge`, `scope=openid profile offline_access`, audience, redirect URI), launch `WebAuthenticator.AuthenticateAsync`, exchange the returned code via `IAuth0TokenClient.ExchangeCodeAsync`, store the refresh token, then re-run the bootstrap; handle cancel/failure by staying on the page with retry
- [x] 6.3 Register the WebAuthenticator callback per platform: Android `WebAuthenticatorCallbackActivity` intent filter for the custom scheme; iOS `CFBundleURLTypes` in `Info.plist` (and MacCatalyst); confirm the round-trip on Android

## 7. Tests

- [x] 7.1 Add a unit-test project (xUnit + Moq) for the app's testable services
- [x] 7.2 Test `ISessionService` routing: no token → unauthenticated; refresh success + `200` → active game; refresh success + `404` → no game; refresh `invalid_grant` → unauthenticated + token cleared; backend `401` → unauthenticated
- [x] 7.3 Test `IAuth0TokenClient.RefreshAsync` parsing (access token + rotated refresh token) and transient-vs-rejection handling with a mocked `HttpMessageHandler`
- [x] 7.4 Test `IGameApiClient` status-code mapping (`200`/`404`/`401`) with a mocked `HttpMessageHandler`

## 8. Verification

- [x] 8.1 Build the MAUI app for Android (`dotnet build -f net10.0-android`) and run the unit tests
- [ ] 8.2 Manually verify the three startup paths: stored valid refresh token → routes to home/game; no/expired refresh token → routes to login; complete interactive login → refresh token stored and app proceeds to home/game *(requires a device/emulator with live Auth0 sign-in — run locally)*
