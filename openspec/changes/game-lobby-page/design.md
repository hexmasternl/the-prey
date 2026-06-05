## Context

The `Game` domain model already has `GameCode`, `LobbyPlayer`, and a `SetHunter` endpoint (`POST /games/{id}/hunter`) that pre-designates the hunter before game start. The `GameDto` and `LobbyPlayerDto` are in the Abstractions project. EF Core via Postgres is the persistence layer.

Two things `LobbyPlayer` currently lacks: `IsReady` (whether this player has acknowledged the settings) and any concept of a pre-designated role. The lobby SSE stream, the remove-player endpoint, and the update-settings endpoint do not exist.

On the frontend, `GamesService` has `getActiveGame()` but no lobby methods; there is no `GameLobbyPage`.

## Goals / Non-Goals

**Goals:**
- Add `IsReady` to `LobbyPlayer` (domain + EF migration)
- New endpoint: `POST /games/{id}/lobby/ready` — non-owner marks themselves ready
- New endpoint: `DELETE /games/{id}/lobby/{userId}` — owner removes a participant
- New endpoint: `PUT /games/{id}/settings` — owner updates game configuration; resets all non-owner `IsReady` to false
- New endpoint: `GET /games/{id}/lobby/stream` — SSE stream pushing lobby events to connected clients
- `LobbyPlayerDto` gains `IsReady` and `DesignatedHunter` (bool, derived from the pre-designated hunter set via existing `POST /hunter`)
- `GameLobbyPage` in Ionic with game code, settings display/edit, player list, SSE subscription, ready button

**Non-Goals:**
- Chat or messaging within the lobby
- Lobby capacity enforcement changes (existing `MaxLobbySize = 16` stands)
- Push notifications outside the SSE stream
- Game start button on this page (the existing `/start` endpoint is called by a separate "Start Game" action — out of scope here)

## Decisions

### 1. `IsReady` lives on `LobbyPlayer`, not a separate table

`LobbyPlayer` is a value object on the `Game` aggregate, persisted as a related table. Adding a column to that table is the natural fit — no join needed, the aggregate owns all lobby state.

**Alternative:** A separate `LobbyReadyState` table keyed by (GameId, UserId). Rejected — unnecessary indirection for a single boolean.

### 2. Ready resets in the `UpdateSettings` domain method

When `Game.UpdateSettings(config, ownerUserId)` is called, the method iterates `_lobby` and replaces each non-owner `LobbyPlayer` record with one where `IsReady = false`. Because `LobbyPlayer` is an immutable record, we replace the list entries.

**Why this way:** Business rule is enforced at the aggregate root, not scattered across handlers.

### 3. SSE via `IAsyncEnumerable` + `ILobbyEventBus`

The `GET /games/{id}/lobby/stream` endpoint writes SSE frames by iterating an `IAsyncEnumerable<LobbyEvent>` produced by an `ILobbyEventBus`. The bus is an in-process `Channel<LobbyEvent>` (one per game, stored in a `ConcurrentDictionary`), injected as a singleton. On any mutating command (join, remove, settings change, ready toggle, hunter set), the handler publishes to the bus. The endpoint reads from it and writes `data: {...}\n\n` lines.

**Why not SignalR:** The proposal specifically says SSE; adding SignalR would require hubs and client library changes. SSE is lower friction for a one-way push channel.

**Why in-process channel over Redis Pub/Sub:** Single-instance deployment (Aspire hosts one process). When horizontal scaling is needed, the bus can be swapped to a Redis-backed implementation behind the same `ILobbyEventBus` interface without touching the handlers or endpoint.

### 4. Hunter designation reuses existing `POST /games/{id}/hunter`

The lobby page calls the existing endpoint when the owner taps a player row. `LobbyPlayerDto` adds a `DesignatedHunter` boolean (server-side: the game's `_lobby` entry whose `UserId` matches the stored designated-hunter field). No new endpoint needed.

The `Game` domain model needs one new field: `Guid? DesignatedHunterUserId`. It is set by the existing `SetHunter` handler and cleared when a player is removed from the lobby.

**Alternative:** Store the designated-hunter entirely on the client and send it only at game start. Rejected — other lobby participants need to see who the designated hunter is via SSE.

### 5. Settings update DTO reuses `GameConfigurationDto`

`PUT /games/{id}/settings` accepts the existing `GameConfigurationDto` request body. No new DTO needed for the request; the response is `GameDto`.

### 6. `LobbyPlayerDto` is a non-breaking extension

`LobbyPlayerDto` gains `IsReady` and `DesignatedHunter` as new positional record parameters. Because the record is part of `GameDto` which clients receive, this is additive (new fields) — existing clients that ignore extra fields are unaffected. C# record positional constructors require all callers to be updated; all call sites are in the same solution.

### 7. Frontend SSE via `EventSource` with Angular signal

The `GameLobbyPage` opens a native browser `EventSource` when entering the lobby and closes it on `ionViewWillLeave`. Incoming events update a `lobbyState = signal<GameDto | null>(null)` directly. No third-party SSE library is needed.

**Auth on SSE:** `EventSource` does not support custom headers; pass the Auth0 access token as a `?token=` query parameter. The SSE endpoint reads the token from the query string, validates it using the same JWT bearer middleware the rest of the API uses (via `IAuthenticationService`), and closes the stream on failure.

## Risks / Trade-offs

- **In-process SSE bus drops events on restart** — acceptable in a single-instance dev/prototype context; production hardening (Redis) is future work.
- **Token in query string** — slightly less secure than a header (appears in server logs). Mitigated by short-lived tokens and HTTPS-only; standard practice for SSE in SPA apps.
- **`LobbyPlayer` is a sealed record** — the `IsReady` and `DesignatedHunter` additions require updating every `Rehydrate` / `Create` call site in the data adapter. Low risk — all sites are in the same solution.
- **EF Core migration needed** — adds a column to the lobby-players table. Zero-downtime is achievable (column with a default value of `false`).
