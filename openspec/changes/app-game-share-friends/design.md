# Design

## Context

- The lobby (`ui/games/lobby/LobbyScreen.kt`) already has a Share button that sends `lobby_share_message` (text plus code) through `Intent.ACTION_SEND`. The lobby UI state knows the game id and code.
- `MainActivity` handles only the resume-tracking notification intent. The manifest has no `VIEW` intent filter.
- Root navigation (`ThePreyNavHost`) is driven by `SessionState`: `Unknown` holds the system splash, `SignedOut` shows the landing page, `SignedIn` shows the signed-in graph. Each switch clears the back stack. Sign-in launches the Auth0 hosted page in a browser and returns through the `https://theprey.eu.auth0.com/android/...` App Link, so the activity can be recreated, or the process killed, during sign-in.
- Small persisted state uses Preferences DataStore behind an interface (`TrackingSessionStore`, `ProfileStore`, `LocationConsentStore`), wired in `AppContainer`.
- `LocalLocationConsent.requireLocation` must wrap every start or join game action.
- Server: `POST /games/{id}/join` takes `{ joinCode, displayName, profilePictureUrl? }` and returns the caller's `GameDto`. Errors are problem details with a `code`: `invalid_join_code` (400), `game_already_started`, `lobby_full`, `player_already_joined` (409), and `game_not_found` (404). `GET /games/active` reports only `Started`/`InProgress` games. A player may sit in several lobbies.
- Website: Azure Static Web App serving Hugo output. `static/games/join/index.html` already implements the Android `intent://` handoff with a Play Store fallback, for a one-segment path. `static/join/` is the MAUI version. `assetlinks.json` lists the current package with one fingerprint plus the MAUI package.

## Goals / Non-Goals

**Goals:**
- One tap from a chat message to the lobby, for signed-in players.
- The invitation survives the sign-in round trip and process death, and never fires twice or long after the fact.
- Links keep working while App Link verification is missing (debug builds, in-app browsers).

**Non-Goals:**
- Deferred deep linking after a Play Store install (Install Referrer). A new player taps the link again after installing.
- iOS or web join.
- Server changes, link shortening, or link revocation. Regenerating the code is not in scope.
- Showing who invited you or a game preview before joining.

## Decisions

### Code in the path: `/games/join/{gameId}/{code}`

The server's join needs both the id and the code, and the user wants to land in the lobby without typing. Putting both in the link is the only way short of a server change. Path segments over a query string: the website rewrite and the `intent://` handoff copy the path cleanly, App Link path matching works on prefixes, and some chat apps trim query strings when previewing links.

*Alternatives:* a code-only link (`/games/join/4291`) needs a server lookup by code, and 4 digits are not unique across games. A server-issued short token would need storage and an endpoint. Both are out of scope.

*Security:* the link is exactly as secret as the code that the old text already shared. The GUID makes the link unguessable, and the server still checks the code.

### Verified App Link plus the existing `intent://` page

`MainActivity` gets an `autoVerify="true"` filter for `https` / `theprey.nl` / `pathPrefix="/games/join/"`. When verification succeeds, Chrome and most apps open the app directly. The website page covers the rest: in-app browsers (WhatsApp's custom tab) never fire App Links, but they do follow `intent://theprey.nl/games/join/{id}/{code}#Intent;scheme=https;package=nl.hexmaster.theprey;S.browser_fallback_url=<Play Store>;end`. That intent targets the package explicitly, so it reaches the same filter even when verification failed (debug key). The page keeps its auto-attempt after 250 ms and the visible button.

*Alternative:* a custom `theprey://` scheme. It is not clickable in most chat apps and has no store fallback.

### Website page is static, bilingual in place

The page lives in `static/games/join/index.html`, outside Hugo's `/en/` and `/nl/` trees, because the link has to be language-neutral. It holds both texts and picks Dutch when `navigator.languages` starts with `nl`. `<html lang>` is set to match. Styling follows the design system tokens (phosphor green on near-black, Special Elite heading, PT Mono body) inline, with no external scripts. The existing `/games/join/*` → `/games/join/index.html` rewrite stays. The page parses two segments and validates them with the same rules as the app (GUID, 4 digits). On a malformed link it shows the store link only.

The `/join/*` rewrite and page, the `apple-app-site-association` file and route, and the `maui.app` statement are deleted.

### `assetlinks.json` fingerprints

One statement for `nl.hexmaster.theprey` with three fingerprints: the existing `64:D0:…:0B` (kept; the published app's signing key), the debug key `95:67:85:CC:…:03:4F:15:A8` (read the full value with `./gradlew signingReport`) and the upload key from `key.properties`. If the Play App Signing key in the Play Console differs from `64:D0…`, add it too. Wrong fingerprints only lose the direct open: the website page still hands over.

### Parsing: one pure function

`deeplink/JoinLink.kt` holds `JoinInvite(gameId, code)` and `parseJoinLink(uri: String): JoinInvite?` (host `theprey.nl`, scheme `https`, exactly `/games/join/{guid}/{4 digits}`, optional trailing slash, game id lowercased). It also has `joinLink(gameId, code): String`, which the lobby share uses so building and parsing cannot drift. Unit-tested without Android.

### Pending invite store

`PendingInviteStore` (interface + `DataStoreInviteStore`, its own Preferences DataStore file) holds one `PendingInvite(gameId, code, receivedAtMillis)`.

- `save(invite)` overwrites, so a newer link replaces an older one.
- `take(nowMillis): JoinInvite?` atomically reads and clears it in one `edit`, and returns null when it is older than one hour. This is what makes "used once" hold.
- `pending: Flow<Boolean>` lets navigation react.

It is persisted, not kept in memory or `SavedStateHandle`, because sign-in leaves the app for the browser and the process may die. Expiry uses the wall clock (`System.currentTimeMillis`), which survives a reboot, unlike `elapsedRealtime`. A clock that jumps can only make an invite expire early or late, which is harmless. The store is not cleared on sign-out. The invitation belongs to whoever signs in next, and that is the same person in practice.

`MainActivity.onCreate` (when `savedInstanceState == null`) and `onNewIntent` pass `intent.data` through `parseJoinLink` and save the result. Links that do not parse are ignored, and the app opens as usual.

### Navigation: a join destination on top of the main menu

New route `Routes.JOIN = "join"` inside the signed-in graph. In `ThePreyNavHost`, one `LaunchedEffect(state, hasPendingInvite)` runs after the session-target effect. When the state is `SignedIn` and an invite is pending, it navigates to `JOIN` with `launchSingleTop`, so the main menu stays underneath and every exit goes back to it.

- **Signed out:** nothing happens. The landing page shows as usual. After sign-in, the state flips to `SignedIn`, the session effect lands on the main menu, and the join effect opens `JOIN`. This covers both "never signed in" and "session could not be restored", because both are `SignedOut`.
- **Already signed in, cold start:** the splash holds until the session is known, the launch sequence plays, then the nav host opens `JOIN`.
- **Link while running** (`onNewIntent`): the flow emits and the effect opens `JOIN` over whatever is showing. If the player was in a lobby or game, the join step's own outcome takes them where they belong.

### Join step: `JoinGameViewModel` + `JoinGameScreen`

The screen wraps its start in `requireLocation(onDeclined = …)`, then tells the view model to start. The view model:

1. `take()`s the invite. If there is none (expired, or already used), it exits to the main menu with no message.
2. Calls `activeGame()`. `Active(sameId)` → open `Routes.game(id)`. `Active(otherId)` → main menu with `MainMenuMessage.AlreadyInGame`. `Failed` → connection error state.
3. Calls `join(gameId, code, callsign)` (callsign from `ProfileStore`, as the create flow does). Success or `AlreadyJoined` → `Routes.lobby(id)`, popping `JOIN`. `InvalidInvite` (404 or `invalid_join_code`), `AlreadyStarted` and `LobbyFull` → main menu with the matching message. `Network` → error state with Retry (repeats steps 2–3 with the invite held in the view model) and Cancel (main menu).

Pure logic (outcome → navigation target) sits in a small `joinOutcome()` mapping so it can be unit-tested alongside the view model with a fake repository.

UI: a centered progress state ("Joining game…") using the existing tactical components, and an error state with Retry (`PrimaryButton`) and Cancel (`SecondaryButton`). Back on this screen cancels.

New `MainMenuMessage` entries: `InviteInvalid`, `GameAlreadyStarted`, `LobbyFull`, `AlreadyInGame` and `LocationNeededToJoin`.

### Repository: typed join result

`GamesApi.join(@Path id, @Body JoinGameRequest)` and `GameRepository.join(gameId, code, displayName): JoinResult` with `Joined(game)`, `AlreadyJoined`, `InvalidInvite`, `AlreadyStarted`, `LobbyFull` and `Network`. Problem codes are read with the same problem-details parsing the other commands use. An unknown 4xx/5xx maps to `InvalidInvite` for 4xx and `Network` for 5xx, so the player can retry server hiccups.

### Share message

`lobby_share_message` becomes `%1$s` invitation text + newline + `%2$s` link + newline + code (`%3$s`) last, in English and Dutch. The "tap Join game" wording goes, because the link does the work. `LobbyScreen.shareGameCode` becomes `shareInvite(gameId, code)` and uses `joinLink()`.

## Risks / Trade-offs

- [App Link verification fails for a release key that is missing from `assetlinks.json`] → The website page hands over with `intent://`, so links still work with one extra hop. A release checklist task covers adding the Play App Signing fingerprint.
- [Google's verifier caches asset links (about 1 h), and verification runs at install] → Changes to `assetlinks.json` need a reinstall or `adb shell pm verify-app-links --re-verify nl.hexmaster.theprey` to test.
- [A forwarded link lets anyone with it join] → Same as sharing the code today. The owner can remove players in the lobby.
- [A new player installs from the store and the invitation is lost] → Accepted (non-goal). The page tells them to tap the link again after installing.
- [The invite fires while the player is mid-game] → The running-game check sends them to their own game screen (same game) or the menu with "already in a game". Tracking is unaffected, because `TrackingAutoStart` keys on the running game, not the screen.
- [`take()` before the join means an invite is lost if the process dies mid-join] → Acceptable: the player taps the link again. It guarantees no double join on restart.

## Migration Plan

1. Ship the website first (new page, asset links, removals). Old MAUI `/join/*` links stop working, but that app no longer exists.
2. Release the app with the intent filter. Older app versions ignore the link: on Android the website page still opens them through `intent://` without a handler and falls back to the Play Store, which offers the update.
3. Rollback: revert the website files. The app keeps working with plain codes in the lobby.

## Open Questions

- The exact upload-key and Play App Signing fingerprints. Copied from the Play Console and `signingReport` during implementation; this does not affect the design.
