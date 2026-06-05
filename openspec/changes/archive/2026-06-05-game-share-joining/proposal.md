## Why

Players who create a game lobby have no easy way to invite friends — they must manually communicate the game code out-of-band. A share button on the lobby page lets the owner (or any player already in the lobby) send a deep link that drops recipients directly onto the join page, reducing friction for recruitment.

## What Changes

- Add a share button to the game lobby page, placed next to the game code display, that invokes the native Web Share API.
- The shared message includes an invitation text ("You are invited to join a game on The Prey!"), a deep link to the in-app join page (`/games/join?gameId=<id>`), and ends with the 8-digit game code.
- Add a new `games/join` route and page in the Ionic/Angular client. The page receives the game ID via the `gameId` query parameter. The user must manually enter the 8-digit game code on this page to confirm and join the lobby.
- No new backend endpoints are required — the existing `POST /games/{id}/lobby` join endpoint handles the join, and the game code entry is validated client-side or via the existing join validation.

## Capabilities

### New Capabilities

- `game-share-invite`: A share button on the lobby page that generates and sends a native share sheet message containing an invitation, a deep link (`/games/join?gameId=<id>`), and the game code at the end.
- `game-join-by-code`: A `/games/join` page that receives a game ID via query parameter and requires the user to manually enter the 8-digit game code to join the lobby.

### Modified Capabilities

## Impact

- **Client (`src/ThePrey`)**: `game-lobby.page.html` and `game-lobby.page.ts` gain a share button; new `game-join.page.ts/html/scss` component is added; `app.routes.ts` gains the `games/join` route; i18n files (`en.json`, `nl.json`) gain new translation keys.
- **Backend (`src/Games`)**: No changes required. The existing `POST /games/{id}/lobby` endpoint is used as-is.
