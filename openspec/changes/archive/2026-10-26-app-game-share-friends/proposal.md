# Proposal

## Why

The lobby's Share button (`app-game-lobby-view`) sends only a text with the 4-digit code and tells the recipient to "tap Join game", but the Android app has no join screen and no link handling. An invited friend has no way into the game. Inviting friends is how a game gets its players, so the invitation has to be one tap: open the link, land in the lobby.

The `game-deep-link` and `game-join-by-code` specs still describe the removed Ionic client (`theprey://join?gameId=`, a web join page with manual code entry, an 8-digit code). The website still serves `/join/*` for the deleted MAUI app (`nl.hexmaster.theprey.maui.app`) and an `apple-app-site-association` for an iOS app that does not exist.

## What Changes

- **Share link.** The lobby's Share button opens the Android share sheet with an invitation that contains a public link `https://theprey.nl/games/join/{gameId}/{code}` and still ends with the game code. The text is in the app language (English and Dutch).
- **Android App Link.** The app declares a verified `https` intent filter for `theprey.nl/games/join/*`. A tap on the link in a browser or chat app opens the app directly.
- **Website landing page** at `/games/join/{gameId}/{code}` for links that are not opened by the app (in-app browsers, verification not done yet, app not installed). On Android it hands the link to the app with an `intent://` URL whose fallback is the Play Store listing of `nl.hexmaster.theprey`. On other platforms it shows the Play Store button. The page text follows the browser language (English or Dutch).
- **Website configuration.** `assetlinks.json` lists only `nl.hexmaster.theprey` with the Play App Signing, upload and debug key fingerprints. The `/games/join/*` rewrite keeps the page reachable for every path. The MAUI leftovers (the `/join/*` page and rewrite, the `maui.app` asset link and the `apple-app-site-association` file and route) are removed.
- **Joining from a link.** When the app receives a join link and the player is signed in, it joins the game with `POST /games/{id}/join` (code and callsign) and opens the lobby. If the player is already in that game it opens the lobby (or the running game) without an error. If they are in another active game, are given a stale or wrong link, or the game is full or started, they land on the main menu with a message.
- **Link remembered across sign-in.** If the player is signed out, or the stored session could not be restored, the app keeps the link while they sign in on the landing page. After a successful sign-in it joins and opens the lobby. The remembered link survives the browser round trip of the login and expires after a while, so an old invitation never fires unexpectedly.
- New English and Dutch strings in the app and on the website.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `game-deep-link`: the custom `theprey://join` scheme is replaced by the verified `https://theprey.nl/games/join/{gameId}/{code}` App Link, the website landing page with Play Store fallback, the asset links file, and the rule that a link received while signed out is kept until sign-in.
- `game-join-by-code`: the Ionic web join page with manual code entry is replaced by the Android app joining automatically from the link's game id and code, with the outcomes (lobby, running game, error message on the main menu).
- `game-share-invite`: the share message carries the public join link in addition to the code. Builds on the requirement as rewritten by `app-game-lobby-view`.

## Impact

- **Android app** (`app/src`):
  - `AndroidManifest.xml`: `autoVerify` intent filter on `MainActivity` for `https://theprey.nl/games/join/`.
  - New `deeplink/` package: a pure parser for join links, and a small persisted pending-invite store (with expiry), wired in `AppContainer`.
  - `MainActivity` hands incoming link intents (`onCreate` and `onNewIntent`) to the pending-invite store.
  - `GamesApi` / `GameRepository` get `join(gameId, code, displayName)` with typed results for the server's error codes.
  - New join step (view model + small "joining" screen) in `ThePreyNavHost`, reached from the signed-in area whenever a pending invite exists. Main menu messages for the failure outcomes.
  - Lobby share message updated. Strings in `values/strings.xml` and `values-nl/strings.xml`, plus unit tests.
- **Website** (`website/src/static`): `games/join/index.html` rewritten for the new path and both languages, `.well-known/assetlinks.json` updated, `staticwebapp.config.json` cleaned up, `join/` and `.well-known/apple-app-site-association` removed.
- **Server**: no change. `POST /games/{id}/join` already exists and returns `invalid_join_code`, `game_already_started`, `lobby_full` and `player_already_joined`.
- **Docs**: `docs/api/endpoints.md` lists `POST /games/{id}/join` with its request body instead of the obsolete `POST /games/join`.
- **Release**: the Play App Signing key fingerprint must be copied from the Play Console into `assetlinks.json` before release, otherwise verified links fall back to the website page.
- **Depends on** `app-game-lobby-view` (lobby screen and its share button). That change must be archived first, because this change modifies its `game-share-invite` requirement.
