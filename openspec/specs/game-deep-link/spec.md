# game-deep-link Specification

## Purpose
TBD - created by archiving change game-joining. Update Purpose after archive.

## Requirements

### Requirement: Public invitation link

An invitation to a game SHALL be the public URL `https://theprey.nl/games/join/{gameId}/{code}`, where `{gameId}` is the game's GUID and `{code}` its 4-digit game code. The client builds the link. The backend SHALL NOT generate, store or redirect it. A link is valid only when both segments are present, `{gameId}` is a GUID and `{code}` is exactly four digits. Anything else SHALL be treated as not an invitation.

#### Scenario: Link built from the game

- **WHEN** a game with id `3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01` and code `4291` is shared
- **THEN** the link is `https://theprey.nl/games/join/3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01/4291`

#### Scenario: Link without a code is not an invitation

- **WHEN** the app or the website receives `https://theprey.nl/games/join/3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01`
- **THEN** it is not treated as an invitation and no join is attempted

#### Scenario: Malformed segments are not an invitation

- **WHEN** the link's game id is not a GUID or its code is not exactly four digits
- **THEN** it is not treated as an invitation and no join is attempted

### Requirement: App opens invitation links

The Android app SHALL be the verified handler of `https://theprey.nl/games/join/*`, so tapping an invitation link on a device with the app installed opens the app instead of the browser. The app SHALL accept the link both on a cold start and while it is already running. A link handed over by the website landing page SHALL be handled in the same way.

#### Scenario: Link tapped with the app installed

- **WHEN** a player with the app installed taps an invitation link in a chat app or browser that opens verified App Links
- **THEN** the app opens and handles the invitation

#### Scenario: Link received while the app is running

- **WHEN** the app is already open and the player taps an invitation link
- **THEN** the running app handles the new invitation without being restarted

### Requirement: Website invitation page

The website SHALL serve a page for every `/games/join/*` path that is not opened by the app. On Android it SHALL try to open the invitation in the app at once, and fall back to the Play Store listing of `nl.hexmaster.theprey` when the app is not installed. It SHALL also show an "Open in app" button and a "Get it on Google Play" link. On other platforms it SHALL show only the Play Store link. The page text SHALL be in Dutch when the browser prefers Dutch and in English otherwise. The page SHALL NOT be indexed by search engines.

#### Scenario: Android with the app installed, link opened in an in-app browser

- **WHEN** an Android user opens an invitation link in a browser that does not open App Links (for example a chat app's built-in browser) and the app is installed
- **THEN** the page opens the app with the same invitation

#### Scenario: Android without the app

- **WHEN** an Android user without the app opens an invitation link
- **THEN** they are sent to the Play Store listing of `nl.hexmaster.theprey`

#### Scenario: Automatic open blocked

- **WHEN** the browser blocks the automatic open
- **THEN** the "Open in app" button opens the app, or the Play Store when the app is not installed

#### Scenario: Not an Android device

- **WHEN** someone opens an invitation link on a desktop or iOS browser
- **THEN** the page explains that The Prey is an Android app and links to its Play Store listing

#### Scenario: Dutch browser

- **WHEN** the browser's preferred language is Dutch
- **THEN** the page text is Dutch

### Requirement: Website app association

The website SHALL serve `/.well-known/assetlinks.json` as `application/json` to anonymous callers, associating `theprey.nl` with the package `nl.hexmaster.theprey` and the SHA-256 fingerprints of its Play App Signing key, its upload key and the debug key. It SHALL NOT associate any other package. The website SHALL NOT serve the legacy MAUI invitation page at `/join/*` or an `apple-app-site-association` file.

#### Scenario: Asset links served

- **WHEN** Android's App Links verifier requests `https://theprey.nl/.well-known/assetlinks.json`
- **THEN** it receives JSON with a `delegate_permission/common.handle_all_urls` statement for `nl.hexmaster.theprey` only

#### Scenario: Legacy MAUI path gone

- **WHEN** someone opens `https://theprey.nl/join/<anything>`
- **THEN** the website does not serve an invitation page for it

### Requirement: Invitation kept until sign-in

When the app receives an invitation while no player is signed in, including when the stored session could not be restored, it SHALL keep the invitation and show the landing page so the player can sign in. After a successful sign-in it SHALL continue with the join. The kept invitation SHALL survive the sign-in page and the app being closed in between. It SHALL expire one hour after it was received. A newer invitation SHALL replace an older one, and an invitation SHALL be used at most once.

#### Scenario: Signed out when the link is tapped

- **WHEN** a signed-out player taps an invitation link and then signs in successfully
- **THEN** the app joins the game from the invitation and opens its lobby

#### Scenario: Session could not be restored

- **WHEN** the app opens from an invitation link, the stored session cannot be renewed, and the player signs in again
- **THEN** the app joins the game from the invitation after the sign-in

#### Scenario: Sign-in cancelled

- **WHEN** the player cancels the sign-in page
- **THEN** they stay on the landing page and the invitation is kept until it expires

#### Scenario: Invitation expired

- **WHEN** the player signs in more than one hour after tapping the link
- **THEN** no join is attempted and the player lands on the main menu

#### Scenario: Newer invitation replaces older

- **WHEN** a signed-out player taps two different invitation links and then signs in
- **THEN** only the most recent invitation is used

#### Scenario: Invitation used once

- **WHEN** an invitation has been handled (joined or failed)
- **THEN** a later sign-in or app start does not handle it again
