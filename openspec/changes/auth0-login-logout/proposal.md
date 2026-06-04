## Why

The Prey's backend already enforces Auth0 JWT authentication on every API call, but the Ionic Angular client has no way to sign users in or out. Players cannot access any protected endpoints until the app can obtain and maintain an Auth0 session with refresh-token support on mobile.

## What Changes

- Install `@auth0/auth0-angular`, `@capacitor/browser`, and `@capacitor/app` packages.
- Register `provideAuth0(...)` in `main.ts` with the project's Auth0 domain, client ID, refresh-token strategy, and the Capacitor deep-link redirect URI.
- Extend `environment.ts` (and `environment.prod.ts`) with an `auth0` block holding `domain` and `clientId`.
- Update `AppComponent` to listen for `appUrlOpen` deep-link events and forward the Auth0 callback URL to `handleRedirectCallback`, then close the native browser.
- Add a **Login page** that triggers `loginWithRedirect` via `Browser.open` and displays the app's tactical branding.
- Add a **logout action** (menu item or toolbar button) available to authenticated users that calls `logout` with the Capacitor browser.
- Protect the `home` route (and all future authenticated routes) with `authGuardFn`; unauthenticated users are redirected to the Login page.
- Add `capacitor.config.ts` appId-based custom URL scheme to `Info.plist` (iOS) and `AndroidManifest.xml` (Android).

## Capabilities

### New Capabilities
- `auth0-session`: Auth0 SDK bootstrap (`provideAuth0`), environment config, deep-link callback handling in `AppComponent`, and refresh-token lifecycle — the invisible plumbing that keeps a user signed in.
- `auth-screens`: Login page (pre-auth entry point) and logout trigger (post-auth exit) that drive the Auth0 Universal Login flow via the native browser.

### Modified Capabilities

*(none — no existing requirement specs are affected)*

## Impact

- **`src/main.ts`**: adds `provideAuth0`, `provideHttpClient(withInterceptors([authHttpInterceptorFn]))`.
- **`src/app/app.component.ts`**: injects `AuthService`, `NgZone`, and `App`; adds `appUrlOpen` listener.
- **`src/app/app.routes.ts`**: wraps the `home` route with `canActivate: [authGuardFn]`; adds `/login` route.
- **`src/environments/environment.ts` & `environment.prod.ts`**: new `auth0: { domain, clientId }` field.
- **New files**: `src/app/login/login.page.ts`, `login.page.html`, `login.page.scss`.
- **Dependencies added**: `@auth0/auth0-angular`, `@capacitor/browser`, `@capacitor/app`.
- **Native config**: `capacitor.config.ts` appId must match the URL scheme registered in iOS/Android manifests and the Auth0 dashboard Allowed Callback/Logout URLs.
