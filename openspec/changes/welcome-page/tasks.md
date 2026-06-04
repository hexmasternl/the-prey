## 1. Dependencies & Configuration

- [x] 1.1 Install `@auth0/auth0-capacitor` and `@capacitor/preferences` and `@capacitor/geolocation` via npm in `src/ThePrey`
- [x] 1.2 Add Auth0 credentials to `src/ThePrey/src/environments/environment.ts` (domain, clientId, redirectUri using the app's custom URL scheme)
- [x] 1.3 Add the custom URL scheme callback to `capacitor.config.ts` so the Auth0 in-app browser can return to the app
- [x] 1.4 Add the `@capacitor/geolocation` and `@capacitor/preferences` plugins to Android (`AndroidManifest.xml` — `ACCESS_FINE_LOCATION` permission) and iOS (`Info.plist` — `NSLocationWhenInUseUsageDescription`)

## 2. Theme Tokens

- [x] 2.1 Add all style-guide CSS custom properties to `src/ThePrey/src/theme/variables.scss` under `:root` (dark defaults): `--tp-bg-void`, `--tp-bg-base`, `--tp-bg-surface`, `--tp-line`, `--tp-signal`, `--tp-signal-dim`, `--tp-signal-deep`, `--tp-signal-glow`, `--tp-text`, `--tp-text-soft`, `--tp-text-ghost`, `--tp-head`, `--tp-body`
- [x] 2.2 Add `@media (prefers-color-scheme: light)` block overriding the surface and text tokens with light-mode values
- [x] 2.3 Set `--ion-background-color: var(--tp-bg-base)` and `--ion-text-color: var(--tp-text)` so Ionic components inherit the theme

## 3. Auth Service

- [x] 3.1 Create `src/ThePrey/src/app/auth/auth.service.ts` — injectable singleton exposing `isAuthenticated$: BehaviorSubject<boolean>`, `login()`, `logout()`, and `restoreSession()`
- [x] 3.2 Implement `login()` using `Auth0Client.loginWithBrowser()` from `@auth0/auth0-capacitor`; on success store the refresh token via `@capacitor/preferences` and emit `true` on `isAuthenticated$`
- [x] 3.3 Implement `logout()` to call Auth0 logout, remove the stored refresh token, and emit `false`
- [x] 3.4 Implement `restoreSession()` to call `getTokenSilently()` with the stored refresh token; on success emit `true`; on `login_required` / `invalid_grant` clear the stored token and remain unauthenticated
- [x] 3.5 Register `AuthService` as a provided-in-root service and add it to `main.ts` bootstrap providers if needed

## 4. Welcome Page — Structure

- [x] 4.1 Replace `src/ThePrey/src/app/home/home.page.html` with the hero panel template: wrapping `div.hero` with four `.br` corner brackets, `.coords` span (top-right), `.tag` label, `h1` title block, and a `.buttons` section for the four action buttons
- [x] 4.2 Replace `src/ThePrey/src/app/home/home.page.scss` with styles that reproduce the style-guide hero: full-viewport height, dark background with diagonal green gradient, border, corner bracket absolute positioning, Special Elite + PT Mono fonts (loaded via `index.html` or local assets)
- [x] 4.3 Update `src/ThePrey/src/app/home/home.page.ts` to inject `AuthService` and `@capacitor/geolocation`, call `restoreSession()` in `ngOnInit`, subscribe to `isAuthenticated$`, and start a `watchPosition` for the coordinates display

## 5. Welcome Page — Buttons

- [x] 5.1 Implement Play Now button: `[disabled]="!(authService.isAuthenticated$ | async)"`, primary style, routes to `/play` (stub route)
- [x] 5.2 Implement Playfields button: `[disabled]="!(authService.isAuthenticated$ | async)"`, ghost style, routes to `/playfields` (stub route)
- [x] 5.3 Implement Login/Logout button: label and action driven by `isAuthenticated$` — shows "LOGIN" and calls `authService.login()` when false, shows "LOGOUT" and calls `authService.logout()` when true
- [x] 5.4 Implement Quit button: calls `App.exitApp()` from `@capacitor/app`

## 6. Routing Stubs

- [x] 6.1 Add stub routes `/play` and `/playfields` to `app.routes.ts` (can point to the home page or a placeholder component) so navigation from the welcome page does not throw a 404

## 7. Verify

- [x] 7.1 Run `npm run build` in `src/ThePrey` and confirm zero build errors
- [ ] 7.2 Run `npm start` and visually verify the welcome page renders with the hero panel, corner brackets, GPS coordinates (or fallback), title, and four buttons in a browser
- [ ] 7.3 Confirm Play Now and Playfields are disabled in the logged-out state and the Login button triggers the Auth0 flow
