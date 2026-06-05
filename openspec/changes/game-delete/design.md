## Context

The game domain currently has three statuses: `Lobby`, `InProgress`, `Completed`. No delete operation or real-time notification channel exists. The backend is a modular monolith on ASP.NET Core 10; the Ionic/Angular client communicates over HTTP. Status is stored as an integer in Postgres, so adding a new enum value requires no migration.

The lobby page does not exist yet — this change introduces both the lobby page and the SSE subscription that powers game-deleted notifications.

## Goals / Non-Goals

**Goals:**
- Let the game owner soft-delete a Lobby-state game (transition to `Deleted`).
- Notify all connected lobby participants in real time via SSE when deletion happens.
- Render a thematic "game aborted" alert on the lobby page when the event arrives.
- Keep the SSE infrastructure simple enough that it can be reused for future events (game started, player joined, etc.) without over-engineering now.

**Non-Goals:**
- Hard-deleting the game row from Postgres (soft-delete only; audit history is preserved).
- SSE events for any lifecycle transition other than game-deleted (follow-on changes).
- Push notifications to disconnected clients (out of scope; SSE only reaches active connections).
- Authorization checks on who can read the SSE stream beyond requiring authentication.

## Decisions

### SSE vs. SignalR / WebSockets

**Decision:** Use plain ASP.NET Core Server-Sent Events (HTTP streaming, `text/event-stream`).

**Rationale:** SSE is unidirectional server→client, which is exactly what game notifications need: the server pushes events and clients only read. SignalR adds bidirectional transport negotiation and a hub abstraction layer that is unnecessary here. SSE works natively with the browser `EventSource` API and Capacitor's HTTP stack, requires no extra package, and is trivial to implement with a streaming `IResult`. The tradeoff is that reconnection on mobile network changes is the client's responsibility.

**Alternative considered:** SignalR — rejected because of added complexity (hub registration, transport negotiation, JS client package) for a feature that only needs server→client push.

### In-process event channel: `Channel<T>` per game

**Decision:** Maintain a `ConcurrentDictionary<Guid, Channel<GameEvent>>` inside a singleton `GameEventChannel` service. The SSE endpoint reads from the channel; the command handler writes to it.

**Rationale:** The game API is a single process (Aspire-hosted modular monolith), so in-process channels are sufficient. A `Channel<GameEvent>` with bounded capacity (1 event buffer) is enough for deletion events — there is at most one deletion per game. Channels are low-allocation and naturally handle backpressure.

**Alternative considered:** A pub/sub via Redis — rejected as overkill for a single-process deployment. Can be swapped in later if the API scales horizontally.

### `IGameEventChannel` interface in the domain module

**Decision:** Define `IGameEventChannel` in `HexMaster.ThePrey.Games/Notifications/` and implement it in the same project (no data adapter required). Register as singleton.

**Rationale:** SSE channel state is purely in-memory and has no persistence requirement. Keeping it in the domain module means the command handler can depend on `IGameEventChannel` without crossing module boundaries.

### Lobby page SSE subscription via Angular `EventSource`

**Decision:** Use the browser-native `EventSource` API inside the `GameLobbyPage` component. Subscribe on `ionViewWillEnter`, close on `ngOnDestroy`.

**Rationale:** `EventSource` is available in all modern browsers and in Capacitor's webview. It handles automatic reconnection on transient failures. No Angular wrapper library is needed. The token is passed as a query parameter (`?token=<jwt>`) because `EventSource` does not support custom request headers.

**Alternative considered:** Polling with `setInterval` — rejected because it wastes bandwidth and introduces latency compared to a push channel.

### Token transport for the SSE endpoint

**Decision:** Accept a bearer token as a `token` query parameter on `GET /games/{id}/events`, in addition to the standard `Authorization` header. The endpoint validates whichever is present.

**Rationale:** `EventSource` in browsers cannot set custom headers. Passing the token as a query parameter is the standard workaround. The server must explicitly support this pattern. The token is short-lived (Auth0 access token lifetime) and the channel is read-only, so the risk is acceptable.

### Deleted status not surfaced as a soft-delete filter

**Decision:** The `ListGames` and `GetGame` queries continue to return `Deleted` games as-is (status field visible); no automatic filtering.

**Rationale:** Clients already observe the status field. Filtering deleted games in queries can be added later without spec changes.

## Risks / Trade-offs

- **SSE connections and server resources** → Each connected client holds an open HTTP connection. For a game with 16 players all in the lobby, that's 16 persistent connections per game instance. Mitigation: the singleton `GameEventChannel` completes (closes) its channel after broadcasting the delete event, which terminates all SSE streams cleanly.
- **Token in query string logged by proxies** → Query parameters are often captured in access logs. Mitigation: the token is short-lived; document that log retention for the SSE endpoint should be minimal in the deployment runbook.
- **Mobile network handoff drops EventSource** → The client's `EventSource` reconnects automatically, but may miss events sent during the gap. Mitigation: on reconnect, the lobby page immediately fetches game state via `GET /games/{id}` and checks for `Deleted` status as a fallback.
- **No migration needed** → Adding `Deleted = 4` to the enum is additive; the EF Core snapshot stores the numeric value. Existing rows are unaffected. Rollback path: remove the enum value and the new endpoint; no data loss.

## Open Questions

- Should the lobby page also display a "Game started — transitioning" banner when a `game-started` SSE event arrives? (Out of scope for this change; left as a follow-on.)
- Should deleted games be permanently purged after N days? (Out of scope; can be an independent maintenance job.)
