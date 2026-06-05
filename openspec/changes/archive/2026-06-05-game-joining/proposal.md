## Why

Players currently have no way to invite others into a game they've created. A shareable deep link containing the game identifier lets the owner send a WhatsApp (or any channel) message; recipients tap it, authenticate, and are guided to enter an 8-digit join code that proves they were invited — preventing random users from stumbling into private games.

## What Changes

- Games are assigned an 8-digit numeric join code at creation time.
- A new `POST /games/{gameId}/join` endpoint accepts the join code and adds the authenticated caller to the lobby.
- The deep-link URL scheme (`theprey://join?gameId=…`) is documented as the canonical shareable format; the backend exposes the join endpoint that the client navigates to after resolving the link.
- The existing "Lobby management" requirement is extended: joining now requires supplying the correct join code in addition to being authenticated.

## Capabilities

### New Capabilities

- `game-join-code`: An 8-digit numeric code assigned to every newly created game, stored alongside the game, and validated when a player attempts to join the lobby.
- `game-deep-link`: The shareable URL scheme (`theprey://join?gameId=<id>`) and the corresponding backend endpoint that the client calls after the user authenticates and enters the join code.

### Modified Capabilities

- `games`: Lobby management requirement changes — joining the lobby now requires a valid join code (`joinCode`, 8 digits) in addition to authentication. Game creation assigns and persists the join code.

## Impact

- **Games domain** — `Game` aggregate gains a `JoinCode` property (8-digit string); `CreateGameCommand` generates and stores the code; `JoinGameCommand` validates it.
- **Games API** — `POST /games/{gameId}/join` endpoint receives `{ joinCode }` in the request body.
- **Games data adapter** — `JoinCode` column added to the games table via a new EF Core migration.
- **No breaking changes to existing endpoints** — game creation and retrieval shapes are extended (join code returned in game DTO), not replaced.
