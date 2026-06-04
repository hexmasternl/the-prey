## 1. Install Dependencies

- [x] 1.1 Run `npm install @auth0/auth0-angular @capacitor/browser @capacitor/app` in `src/ThePrey`
- [x] 1.2 Verify installed packages appear in `src/ThePrey/package.json` with correct versions

## 2. Environment Configuration

- [x] 2.1 Add `auth0: { domain: string; clientId: string }` to `src/ThePrey/src/environments/environment.ts` with development tenant values
- [x] 2.2 Add matching `auth0` block to `src/ThePrey/src/environments/environment.prod.ts` with production tenant values
- [x] 2.3 Confirm `capacitor.config.ts` has an `appId` set (e.g., `nl.hexmaster.theprey`); set one if missing

## 3. Auth0 SDK Bootstrap

- [x] 3.1 Import `provideAuth0` from `@auth0/auth0-angular` in `src/ThePrey/src/main.ts`
- [x] 3.2 Import `CapacitorConfig` from `capacitor.config.ts` or hard-code the `appId` constant in `main.ts`
- [x] 3.3 Add `provideAuth0({ domain, clientId, useRefreshTokens: true, useRefreshTokensFallback: false, authorizationParams: { redirect_uri: '{appId}://{domain}/capacitor/{appId}/callback' } })` to the `providers` array
- [x] 3.4 Add `provideHttpClient(withInterceptors([authHttpInterceptorFn]))` to `providers` for future API auth header injection

## 4. AppComponent Deep-Link Handler

- [x] 4.1 Inject `AuthService`, `NgZone` into `AppComponent` and import `App` from `@capacitor/app` and `Browser` from `@capacitor/browser`
- [x] 4.2 Implement `ngOnInit`: call `App.getLaunchUrl()` and process it if it starts with `callbackUri` (cold-start Android case)
- [x] 4.3 Register `App.addListener('appUrlOpen', ({ url }) => this.ngZone.run(() => { ... }))` in `ngOnInit` to handle foreground deep links
- [x] 4.4 In the listener body: if URL starts with `callbackUri`, pipe `authService.handleRedirectCallback(url)` into `mergeMap(() => Browser.close())` and subscribe

## 5. Routing — Login Page & Auth Guard

- [x] 5.1 Create `src/ThePrey/src/app/login/` directory with `login.page.ts`, `login.page.html`, and `login.page.scss`
- [x] 5.2 Implement `LoginPage` as a standalone component; inject `AuthService` and `Router`; add `login()` method calling `loginWithRedirect` with `Browser.open` override
- [x] 5.3 Add a redirect in `LoginPage.ngOnInit`: if `isAuthenticated$` is `true`, navigate to `/home`
- [x] 5.4 Add `/login` route to `app.routes.ts` (lazy-loaded, no guard)
- [x] 5.5 Add `canActivate: [authGuardFn]` to the `/home` route in `app.routes.ts`
- [x] 5.6 Update the root redirect (`path: ''`) to point to `/login` instead of `/home`

## 6. Login Page UI

- [x] 6.1 Design `login.page.html`: full-screen dark background, "THE PREY" headline in `Special Elite` font with signal-green colour, and a primary `IonButton` labelled "START HUNT"
- [x] 6.2 Style `login.page.scss` to use `--ion-color-primary` for the headline and button, ensure layout fills the viewport, and add the signal-green glow effect on the button from the style guide

## 7. Logout Action

- [x] 7.1 Add a logout button or menu item to `home.page.html` (e.g., toolbar end-slot `IonButton`)
- [x] 7.2 Implement `logout()` in `home.page.ts`: call `authService.logout({ logoutParams: { returnTo: callbackUri }, openUrl: async (url) => Browser.open({ url, windowName: '_self' }) })`

## 8. Native Platform Configuration

- [ ] 8.1 Add the custom URL scheme (`appId`) to `ios/App/App/Info.plist` under `CFBundleURLTypes` → `CFBundleURLSchemes`
- [ ] 8.2 Add an `<intent-filter>` with `android:scheme="{appId}"` to the main Activity in `android/app/src/main/AndroidManifest.xml`
- [x] 8.3 Run `ionic build && npx cap sync` to propagate web assets to native projects

## 9. Auth0 Dashboard Configuration

- [ ] 9.1 In the Auth0 dashboard, create or identify the Native application for The Prey
- [ ] 9.2 Set **Allowed Callback URLs**: `{appId}://{domain}/capacitor/{appId}/callback`
- [ ] 9.3 Set **Allowed Logout URLs**: `{appId}://{domain}/capacitor/{appId}/callback`
- [ ] 9.4 Set **Allowed Origins (CORS)**: `capacitor://localhost, http://localhost`

## 10. Verification

- [ ] 10.1 Run `npx cap run ios` (or Android): confirm the Login page appears on first launch
- [ ] 10.2 Tap "START HUNT": confirm the native browser opens the Auth0 Universal Login page
- [ ] 10.3 Complete login: confirm the app closes the browser, returns to Home, and `AuthService.isAuthenticated$` emits `true`
- [ ] 10.4 Tap logout: confirm Auth0 session is revoked and the app returns to the Login page
- [ ] 10.5 Kill the app, relaunch: confirm the refresh token silently restores the session and the user lands on Home without re-authenticating
- [ ] 10.6 Force-clear app storage: confirm the user is redirected to Login on next launch
