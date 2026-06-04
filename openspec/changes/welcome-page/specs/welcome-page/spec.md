## ADDED Requirements

### Requirement: Hero panel layout

The welcome screen SHALL occupy the full device viewport and display a hero panel that mirrors the design defined in `designs/the-prey-style-guide.html`. The panel SHALL have a dark void background (`#0C0E0C`) with a subtle diagonal green gradient overlay, a visible border, and four green corner-bracket ornaments (top-left, top-right, bottom-left, bottom-right) each rendered as two-sided right-angle lines in the signal green (`#64FF00`).

#### Scenario: Welcome page renders the hero panel

- **WHEN** the user opens the app
- **THEN** the screen shows a full-viewport dark panel with a green-tinted diagonal gradient, a border, and four green corner brackets at the corners

### Requirement: GPS coordinates display

The welcome screen SHALL display the device's current GPS coordinates in the top-right corner of the hero panel in a dim monospace style (`--text-ghost` colour). The format SHALL be `{lat}° N // {lon}° E // SIGNAL LOCK` (using the actual latitude and longitude to four decimal places). While the coordinate is being acquired, or if geolocation permission is denied, the display SHALL fall back to `-- ° N // -- ° E // NO SIGNAL`.

#### Scenario: Coordinates shown when permission granted

- **WHEN** the welcome page loads and the user has granted location permission
- **THEN** the top-right corner shows the live latitude and longitude in `{lat}° N // {lon}° E // SIGNAL LOCK` format

#### Scenario: Fallback shown when permission denied

- **WHEN** the welcome page loads and geolocation permission is denied or unavailable
- **THEN** the top-right corner shows `-- ° N // -- ° E // NO SIGNAL`

### Requirement: Title and tagline

The hero panel SHALL display the game name "THE PREY" and the tagline "STAY HIDDEN. HUNT SMART." in the Special Elite display font. "HIDDEN." and "SMART." SHALL be rendered in the signal green (`#64FF00`) with a glow effect. The title SHALL be large (matching the style-guide hero heading scale) and appear in the upper-centre area of the panel.

#### Scenario: Title block renders

- **WHEN** the welcome page is displayed
- **THEN** "THE PREY" and "STAY HIDDEN. HUNT SMART." are visible in Special Elite font with the green highlights on "HIDDEN." and "SMART."

### Requirement: Action buttons

The welcome screen SHALL display four buttons stacked vertically in the lower section of the hero panel:

1. **Play Now** — primary filled style (signal green background, dark text); navigates to the game lobby. MUST be disabled when the user is not authenticated.
2. **Playfields** — ghost style; navigates to the playfields list. MUST be disabled when the user is not authenticated.
3. **Login / Logout** — ghost style; shows "LOGIN" when unauthenticated and triggers the Auth0 login flow; shows "LOGOUT" when authenticated and triggers logout.
4. **Quit** — ghost style; closes the application.

#### Scenario: Buttons shown to unauthenticated user

- **WHEN** the user is not logged in
- **THEN** Play Now is disabled, Playfields is disabled, and the third button reads "LOGIN"

#### Scenario: Buttons shown to authenticated user

- **WHEN** the user is logged in
- **THEN** Play Now is enabled, Playfields is enabled, and the third button reads "LOGOUT"

#### Scenario: Quit button closes the app

- **WHEN** the user taps Quit
- **THEN** the application closes via the Capacitor App plugin

### Requirement: Session restoration on load

When the welcome page initialises, the app SHALL attempt to silently restore a prior Auth0 session using a stored refresh token. If restoration succeeds, the user SHALL be treated as authenticated without any visible prompt. If restoration fails (no stored token, expired, or revoked), the user SHALL be treated as unauthenticated and no error SHALL be shown.

#### Scenario: Prior session is restored silently

- **WHEN** the welcome page loads and a valid refresh token is stored
- **THEN** the user is marked as authenticated, Play Now and Playfields become enabled, and the Login button shows "LOGOUT" — all without the user taking any action

#### Scenario: No prior session available

- **WHEN** the welcome page loads and no refresh token is stored or the token is expired
- **THEN** Play Now and Playfields remain disabled and the Login button shows "LOGIN"
