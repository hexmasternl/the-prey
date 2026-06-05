## Why

Players who create a game lobby have no easy way to invite friends — they must manually communicate the game code out-of-band. A share button on the lobby page lets the owner (or any player already in the lobby) send a deep link that drops recipients directly onto the join page, reducing friction for recruitment.

## What Changes

- Add a share button to the game lobby page, placed next to the game code display, that invokes the native Web Share API.
- The shared message includes an invitation text ("You are invited to join a game on The Prey!"), a deep link to the in-app join page (`/games/join/<code>`), and ends with the game code.
- Add a new `games/join/:code` route and page in the Ionic/Angular client that resolves the game code to a game ID and navigates the user into the lobby (joining if not already a member).
- Add a new backend query endpoint `GET /games/code/{code}` to resolve a game code to the full game details, enabling the join-by-code flow.

## Capabilities

### New Capabilities

- `game-share-invite`: A share button on the lobby page that generates and sends a native share sheet message containing an invitation, a deep link, and the game code.
- `game-join-by-code`: A `/games/join/:code` page that accepts a game code (from a deep link), resolves it to a game via the backend, and joins or navigates the user to the lobby.
- `game-lookup-by-code`: A backend endpoint (`GET /games/code/{code}`) that returns game details for a given alphanumeric game code.

### Modified Capabilities

## Impact

- **Client (`src/ThePrey`)**: `game-lobby.page.html` and `game-lobby.page.ts` gain a share button; new `game-join.page.ts/html/scss` component is added; `app.routes.ts` gains the `games/join/:code` route; i18n files (`en.json`, `nl.json`) gain new translation keys; `games.service.ts` gains a `getGameByCode()` method.
- **Backend (`src/Games`)**: New `GetGameByCode` feature slice (query + handler) in `HexMaster.ThePrey.Games`; new endpoint registered in `GameEndpoints.cs`; data adapter gains a lookup-by-code repository method; no new domain model changes.
