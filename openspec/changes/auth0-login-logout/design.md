## Context

The Prey is a standalone Ionic Angular app (`bootstrapApplication`) targeting iOS and Android via Capacitor. The backend already validates Auth0 JWTs on every protected endpoint; `MapInboundClaims = false` is set server-side, so the `sub` claim is used as the user identity.

The client has no authentication at all today — `main.ts` bootstraps with only routing and Ionic providers, and the single `home` route is completely open. The Auth0 Angular SDK (`@auth0/auth0-angular`) provides reactive Auth0 integration for Angular apps and is the canonical choice for this stack.

On mobile, Auth0's Universal Login must open in the device's **native browser** (SFSafariViewController / Chrome Custom Tab) and return to the app via a **deep link** using a custom URL scheme. Iframe-based silent authentication is unavailable on mobile, so **refresh tokens** are the only viable session-renewal mechanism.

## Goals / Non-Goals

**Goals:**
- Wire `provideAuth0` into the standalone bootstrap with refresh-token configuration.
- Handle the post-login deep-link callback in `AppComponent` within Angular's zone.
- Provide a **Login page** as the unauthenticated entry point.
- Provide a **logout action** available from authenticated screens.
- Protect the `home` route (and future routes) with `authGuardFn`.
- Store Auth0 domain/clientId in `environment.ts` so they can differ between dev and prod builds.

**Non-Goals:**
- Social connection configuration (Google, Apple, etc.) — done in the Auth0 dashboard.
- User profile page or role-based access control — future changes.
- HTTP interceptor for API calls — covered separately once authenticated routes exist.
- Web/PWA support — Capacitor native is the only target for now.

## Decisions

### D1: `provideAuth0` over `AuthModule.forRoot`

The app uses standalone bootstrapping (`bootstrapApplication`). `provideAuth0` is the modern functional provider that aligns with Angular's standalone pattern and avoids an unnecessary NgModule wrapper. `AuthModule.forRoot` would require importing an NgModule into a standalone app, adding complexity with no benefit.

### D2: Refresh tokens with no iframe fallback

Mobile browsers block third-party cookies, making iframe-based silent auth unreliable. Setting `useRefreshTokens: true` and `useRefreshTokensFallback: false` is mandatory for Capacitor apps — it ensures the SDK never silently falls back to an iframe and fails with a confusing error instead of the correct behavior.

### D3: Capacitor `appId` as the custom URL scheme

The redirect URI follows the Auth0 Capacitor pattern:  
`{appId}://{auth0Domain}/capacitor/{appId}/callback`  

Using the `appId` from `capacitor.config.ts` as the URL scheme ties the redirect URI to the app's bundle identifier, which is already unique per platform and matches what must be registered in `Info.plist` / `AndroidManifest.xml` and the Auth0 dashboard.

### D4: Deep-link handling in `AppComponent`, not a route

The `App.addListener('appUrlOpen', ...)` call needs a single global listener, making `AppComponent.ngOnInit` the correct location. The handler must run inside `NgZone.run(...)` because Capacitor bridge callbacks execute outside Angular's change-detection zone. `handleRedirectCallback` is called only when the URL starts with the expected `callbackUri`; all other deep links pass through unhandled.

### D5: Login page as a lazy-loaded standalone component

A dedicated `/login` route keeps unauthenticated users away from the app shell and is consistent with the existing lazy-loading pattern in `app.routes.ts`. The login page owns the `loginWithRedirect` call. Redirecting here (rather than showing an inline modal) avoids partial-render states and gives a clear UX entry point.

## Risks / Trade-offs

- **[Risk] Auth0 dashboard misconfiguration** — if the Callback/Logout URLs don't exactly match the `appId`-based scheme, logins will fail with a generic Auth0 error. → **Mitigation**: Document the exact URL patterns in tasks; use environment variables so the URLs are derived programmatically rather than typed by hand.

- **[Risk] Deep-link not fired on cold start (Android)** — on Android, tapping the callback deep link while the app is not in memory may deliver the URL via `App.getLaunchUrl()` instead of `appUrlOpen`. → **Mitigation**: In `AppComponent.ngOnInit`, also call `App.getLaunchUrl()` and process it if it contains the callback URI.

- **[Risk] Zone wrapping missed on future Capacitor callbacks** — forgetting `NgZone.run` causes silent state-update failures that only surface as missing UI updates. → **Mitigation**: Document the zone requirement in the AppComponent comment; enforce in code review.

- **[Trade-off] `useRefreshTokensFallback: false`** — removes the silent-auth safety net, so a missing refresh token means the user must log in again. Acceptable because: (a) tokens are stored in `localStorage` via the SDK; (b) the game sessions are short-lived; (c) the alternative (iframe fallback) doesn't work reliably on mobile anyway.

## Migration Plan

1. Install packages (`@auth0/auth0-angular`, `@capacitor/browser`, `@capacitor/app`).
2. Add `auth0` block to environments — **dev credentials first**, prod credentials added before release.
3. Wire `provideAuth0` in `main.ts`; verify SDK initialises without errors.
4. Add `appUrlOpen` + `getLaunchUrl` handlers to `AppComponent`; test deep-link round-trip.
5. Create Login page; verify `loginWithRedirect` opens the native browser.
6. Apply `authGuardFn` to `home` route; verify redirect to `/login` when unauthenticated.
7. Register URL scheme in `capacitor.config.ts`, `Info.plist`, and `AndroidManifest.xml`.
8. Register Callback/Logout URLs in the Auth0 dashboard for the Native application.
9. Run `ionic build && npx cap sync && npx cap run ios` / `android` for end-to-end test.

**Rollback**: Revert `main.ts`, `app.component.ts`, and `app.routes.ts` — no database or native API changes are involved.

## Open Questions

- What is the Auth0 `domain` and `clientId` for the dev tenant? (Needed before any native test.)
- What is the Capacitor `appId` (in `capacitor.config.ts`)? (Needed to construct the redirect URI and register the URL scheme.)
