## ADDED Requirements

### Requirement: Auth0 SDK is bootstrapped with refresh-token configuration
The application SHALL register `provideAuth0` in `main.ts` using the Auth0 domain and clientId from the environment file, with `useRefreshTokens: true`, `useRefreshTokensFallback: false`, and a Capacitor deep-link `redirect_uri` of the form `{appId}://{domain}/capacitor/{appId}/callback`.

#### Scenario: App starts without authentication
- **WHEN** the application bootstraps
- **THEN** the Auth0 SDK is initialised and `AuthService.isAuthenticated$` emits `false` without any console errors

#### Scenario: Token is refreshed silently after expiry
- **WHEN** the user's access token expires and a valid refresh token exists in storage
- **THEN** the SDK obtains a new access token automatically without requiring the user to log in again

### Requirement: Environment holds Auth0 credentials
The application SHALL read Auth0 `domain` and `clientId` from `environment.auth0.domain` and `environment.auth0.clientId`, allowing separate values for development and production builds.

#### Scenario: Dev build uses development credentials
- **WHEN** the app is built without the `--configuration production` flag
- **THEN** `environment.auth0.domain` and `environment.auth0.clientId` reflect the development tenant values

#### Scenario: Prod build uses production credentials
- **WHEN** the app is built with `--configuration production`
- **THEN** `environment.auth0.domain` and `environment.auth0.clientId` reflect the production tenant values

### Requirement: AppComponent handles the post-login deep-link callback
`AppComponent` SHALL listen for `appUrlOpen` events via `@capacitor/app` and, when the URL starts with the expected callback URI, SHALL call `AuthService.handleRedirectCallback(url)` inside `NgZone.run(...)` and then close the native browser via `Browser.close()`.

#### Scenario: Deep link arrives while app is in foreground
- **WHEN** Auth0 Universal Login completes and the OS delivers the callback URL to the running app
- **THEN** `handleRedirectCallback` is invoked, the session is established, and the native browser is closed

#### Scenario: Deep link arrives on cold start (Android)
- **WHEN** the app is launched by tapping the Auth0 callback deep link while the app was not in memory
- **THEN** `App.getLaunchUrl()` is checked in `ngOnInit`, the callback URL is processed, and the native browser is closed

#### Scenario: Unrelated deep link is ignored
- **WHEN** a deep link URL arrives that does NOT start with the callback URI
- **THEN** `handleRedirectCallback` is NOT called and normal app navigation proceeds

### Requirement: Capacitor custom URL scheme is registered on native platforms
The app's `appId` (from `capacitor.config.ts`) SHALL be registered as a custom URL scheme in `ios/App/App/Info.plist` (CFBundleURLSchemes) and in `android/app/src/main/AndroidManifest.xml` (intent-filter with the scheme), so the OS can route the Auth0 callback back to the app.

#### Scenario: iOS deep link routing
- **WHEN** Auth0 redirects to `{appId}://{domain}/capacitor/{appId}/callback` on iOS
- **THEN** the OS opens the app and fires the `appUrlOpen` event with that URL

#### Scenario: Android deep link routing
- **WHEN** Auth0 redirects to `{appId}://{domain}/capacitor/{appId}/callback` on Android
- **THEN** the OS opens the app via the intent-filter and fires the `appUrlOpen` or launch URL event

### Requirement: Auth0 dashboard is configured to allow the Capacitor callback and logout URLs
The Auth0 Native application configuration SHALL include the Capacitor callback URL and logout URL (`{appId}://{domain}/capacitor/{appId}/callback`) in the **Allowed Callback URLs** and **Allowed Logout URLs** fields, and `capacitor://localhost` and `http://localhost` in **Allowed Origins (CORS)**.

#### Scenario: Login succeeds without CORS or callback mismatch error
- **WHEN** the user completes Universal Login
- **THEN** Auth0 redirects to the app without a "callback URL mismatch" error

#### Scenario: Logout succeeds without error
- **WHEN** the user triggers logout and Auth0 redirects to the returnTo URL
- **THEN** the app receives the deep link and the session is cleared without an "invalid logout URL" error
