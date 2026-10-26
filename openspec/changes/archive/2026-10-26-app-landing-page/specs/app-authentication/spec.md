# Spec Delta

## Purpose

Defines how the Android app signs a player in, keeps that session safe and alive across launches, identifies the player against the Users API, and signs them out, so every other screen can rely on a known, authenticated player.

## ADDED Requirements

### Requirement: Sign-in uses the hosted identity provider in the system browser
The app SHALL sign players in through the hosted Auth0 login page, opened in the system browser or a Custom Tab, using the Authorization Code flow with PKCE. The app SHALL NOT collect passwords in its own UI and SHALL NOT render the login page in an embedded WebView.

#### Scenario: Player starts sign-in
- **WHEN** a signed-out player taps "Sign in" on the landing page
- **THEN** the hosted login page opens in the system browser or a Custom Tab, not inside an app screen

#### Scenario: Sign-in succeeds
- **WHEN** the player completes the hosted login and is redirected back to the app
- **THEN** the app holds an access token for the `https://api.theprey.nl` audience, an ID token and a refresh token, and proceeds to identify the player

#### Scenario: Player cancels sign-in
- **WHEN** the player closes the browser or Custom Tab without completing login
- **THEN** the app returns to the landing page with the "Sign in" action enabled and no error dialog

#### Scenario: Sign-in fails
- **WHEN** the hosted login returns an error or the network is unavailable during the token exchange
- **THEN** the landing page shows a short, translated error message and the player can retry

### Requirement: Credentials are stored encrypted on the device
The app SHALL store the session (access, ID and refresh tokens and their expiry) only in storage encrypted with a key held in the Android Keystore. Tokens SHALL NOT be written to logs, unencrypted preferences, files, or device backups.

#### Scenario: App data is inspected
- **WHEN** the app's private storage is read outside the app
- **THEN** no token is readable in plain text

#### Scenario: Device backup
- **WHEN** the device creates a cloud or device-to-device backup of the app
- **THEN** the stored credentials are excluded and a restored install starts signed out

### Requirement: Access tokens are renewed silently
Before calling the backend, the app SHALL use a non-expired access token, and when it has expired SHALL obtain a new one with the stored refresh token without user interaction. Concurrent requests SHALL share a single renewal.

#### Scenario: Access token expired, refresh token valid
- **WHEN** the access token has expired and the app makes an API call
- **THEN** the app renews the token silently and the call is sent with the new token, without showing the login page

#### Scenario: Several calls need a renewal at once
- **WHEN** multiple API calls start while the access token is expired
- **THEN** only one renewal request is made and all calls use its result

### Requirement: Unrecoverable sessions end in a clean sign-out
When the stored session cannot be renewed because the refresh token is revoked, expired or rejected, the app SHALL clear all stored credentials and the cached profile and return the player to the landing page. Such a failure SHALL NOT be reported to the player as a connection problem.

#### Scenario: Refresh token revoked
- **WHEN** the identity provider rejects the refresh token
- **THEN** the stored session and cached profile are removed and the landing page is shown

#### Scenario: Backend rejects the token
- **WHEN** an API call returns `401 Unauthorized` and a silent renewal does not produce an accepted token
- **THEN** the app signs the player out locally and shows the landing page

#### Scenario: Renewal fails because the device is offline
- **WHEN** the access token has expired and renewal fails because there is no network
- **THEN** the session is kept, the player stays signed in, and the failure is reported as a connection problem

### Requirement: Signed-in player is identified against the Users API
After a sign-in, the app SHALL register or refresh the player's record with `POST /users` using the email address, verification status, names and device language from the ID token, then use the returned player profile (user id, display name, callsign, email, preferred language) as the identity of the signed-in player. The profile SHALL be cached on the device.

#### Scenario: First sign-in of a new player
- **WHEN** a player signs in for the first time
- **THEN** `POST /users` creates their record and the app caches the returned profile

#### Scenario: Returning player signs in again
- **WHEN** a player who already has a record signs in
- **THEN** `POST /users` returns the existing record and the app caches it without creating a duplicate

#### Scenario: Identity provider returns no email address
- **WHEN** the ID token contains no email address
- **THEN** the sign-in is not completed, local credentials are cleared, and the landing page explains that an account with an email address is required

#### Scenario: Identification fails because of the network
- **WHEN** sign-in succeeds but `POST /users` cannot be reached
- **THEN** the landing page shows a retryable connection error and the player is not taken to the main menu with an unknown identity

### Requirement: Session is restored on later launches
When the app starts and stored credentials exist whose refresh token has not been rejected, the app SHALL treat the player as signed in without showing the login page, using the cached profile immediately. It SHALL refresh the profile from `GET /users/me` in the background and update the cache when it succeeds.

#### Scenario: Returning to the app later
- **WHEN** a signed-in player closes the app and opens it again days later
- **THEN** the player is signed in without interaction and identified by the cached profile

#### Scenario: Restore while offline
- **WHEN** a signed-in player opens the app without network access
- **THEN** the player is still treated as signed in with the cached profile

#### Scenario: Player record missing on the backend
- **WHEN** the background `GET /users/me` returns `404 Not Found`
- **THEN** the app re-registers the player with `POST /users` and caches the returned profile

#### Scenario: Stored credentials but no cached profile
- **WHEN** credentials exist but no cached profile is present
- **THEN** the app identifies the player via the Users API before treating the session as restored

### Requirement: Player can sign out
A signed-in player SHALL be able to sign out. Signing out SHALL remove the stored credentials and cached profile from the device and end the hosted login session in the browser, so the next sign-in asks for credentials again. If the browser session cannot be ended, the local sign-out SHALL still complete.

#### Scenario: Player signs out
- **WHEN** the player taps "Sign out" on the main menu
- **THEN** the local session and profile are removed, the hosted session is ended, and the landing page is shown

#### Scenario: Sign-out while offline
- **WHEN** the player signs out without network access
- **THEN** local credentials and profile are still removed and the landing page is shown

#### Scenario: Next launch after sign-out
- **WHEN** the app is started after the player signed out
- **THEN** the landing page is shown, not the main menu
