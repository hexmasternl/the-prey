## Why

The Ionic Angular app currently has a blank placeholder home page — there is no branded entry point, no authentication flow, and no navigation to the game's main sections. The welcome page is the first thing every player sees and needs to establish the game's visual identity while giving logged-out users a clear path to sign in and logged-in users immediate access to Play and Playfields.

## What Changes

- **New welcome page** replaces the blank `home` page with the hero panel design from the style guide: dark background, green corner brackets, dim GPS coordinates sourced from the device, the "THE PREY / STAY HIDDEN. HUNT SMART." title block, and four action buttons.
- **New Auth0 service** wires up PKCE login/logout via the Auth0 Capacitor SDK and silently restores session from a stored refresh token on page open.
- **Button state logic**: Play Now and Playfields are disabled when the user is not authenticated; Login becomes Logout when authenticated.
- **Dark / light theme variants** for the hero background, honouring the device system preference.
- **GPS coordinate display** in the top-right corner, reading the live device location; falls back to a placeholder when permission is denied or on first load.

## Capabilities

### New Capabilities

- `welcome-page`: The branded entry screen — hero panel layout, corner brackets, GPS coords, title, and four contextual action buttons with auth-gate logic.
- `app-auth`: Auth0 PKCE authentication service — login, logout, token storage, and silent refresh-token restoration on app launch.
- `app-theme`: Dark / light surface variants for the hero background driven by the device's preferred colour scheme.

### Modified Capabilities

<!-- none — this is a fresh UI addition with no existing spec to delta -->

## Impact

- `src/ThePrey/src/app/home/` — replaced with the branded welcome page (HTML, SCSS, TypeScript).
- `src/ThePrey/src/app/` — new `auth/auth.service.ts` and `auth/auth.guard.ts`; registered in `main.ts`.
- `src/ThePrey/src/theme/variables.scss` — new CSS custom properties mapping the style-guide tokens for dark and light modes.
- `src/ThePrey/package.json` — adds `@auth0/auth0-angular` (or the Capacitor-compatible OAuth2 package).
- No backend changes required.
