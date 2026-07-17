## Context

The game domain currently has three statuses: `Lobby`, `InProgress`, `Completed`. No delete operation exists yet. The backend is a modular monolith on ASP.NET Core 10; real-time updates already reach clients over Azure Web PubSub. Events published on the in-process event bus are relayed as integration events over Dapr pub/sub to the Notifications module, which calls `IWebPubSubBroadcaster.SendToGameAsync(gameId, eventType, payload)` to fan out to the game's Web PubSub group (group name equal to the game id). Status is stored as an integer in Postgres, so adding a new enum value requires no migration.

The lobby page does not exist yet — this change introduces the lobby page and reuses the existing Web PubSub connection to power game-deleted notifications.

## Goals / Non-Goals

**Goals:**
- Let the game owner soft-delete a Lobby-state game (transition to `Deleted`).
- Notify all connected lobby participants in real time by broadcasting a `game-deleted` event to the game's existing Web PubSub group when deletion happens.
- Render a thematic "game aborted" alert on the lobby page when the event arrives.
- Reuse the existing real-time path without adding any new streaming endpoint or channel infrastructure.

**Non-Goals:**
- Hard-deleting the game row from Postgres (soft-delete only; audit history is preserved).
- Web PubSub events for any lifecycle transition other than game-deleted (follow-on changes).
- Push notifications to disconnected clients (out of scope; Web PubSub only reaches active connections).
- Authorization checks on who can read the Web PubSub group beyond the existing token endpoint's membership check.

## Decisions

### Reuse the existing Web PubSub group broadcast

**Decision:** Publish the `game-deleted` event over the existing real-time path: the command handler calls `PublishAsync` on the in-process event bus, which is relayed as an integration event over Dapr pub/sub to the Notifications module, which calls `IWebPubSubBroadcaster.SendToGameAsync(gameId, "game-deleted", payload)` to fan out to the Web PubSub group `{gameId}`. No new endpoint, channel, or transport is added.

**Rationale:** Web PubSub is already the project's only real-time transport (native WebSocket, one group per game). Clients already hold a group-scoped connection for the active game, so game-deleted rides the same connection they use for every other event. There is nothing to build server-side beyond publishing one more event type, and nothing to build client-side beyond handling one more `{ type, data }` message.

**Alternative considered:** A dedicated one-off HTTP streaming endpoint with its own in-process channel for this single event — rejected. It would introduce a second real-time transport alongside Web PubSub, duplicating the connect/join/backoff logic that Web PubSub already provides and contradicting the one-connection-per-game model.

### `game-deleted` event name and payload

**Decision:** Use the event name `game-deleted` consistently across the publisher and the client handler. The payload carries the game identifier so a client can confirm the event targets the game it is viewing.

**Rationale:** Web PubSub group messages are `{ "type": <eventName>, "data": <payload> }` envelopes. Keeping the event name identical on both ends means the client's existing message dispatch can route it like any other event type.

### Lobby page consumes the existing Web PubSub connection

**Decision:** The `GameLobbyPage` component reacts to the `game-deleted` event delivered over the game's existing group-scoped Web PubSub connection (obtained via the token endpoint `GET /games/{id}/notifications/token`, native WebSocket with subprotocol `json.webpubsub.azure.v1`, `joinGroup` for group `{gameId}`). It does not open a second connection or transport.

**Rationale:** The client already establishes one Web PubSub connection per active game and reconciles missed events on reconnect via `GET /games/{id}`. The lobby page subscribes to that shared channel rather than owning its own, so game-deleted is handled uniformly with every other real-time event.

**Alternative considered:** Polling with `setInterval` — rejected because it wastes bandwidth and introduces latency compared to the push channel that already exists.

### Deleted status not surfaced as a soft-delete filter

**Decision:** The `ListGames` and `GetGame` queries continue to return `Deleted` games as-is (status field visible); no automatic filtering.

**Rationale:** Clients already observe the status field. Filtering deleted games in queries can be added later without spec changes.

## Risks / Trade-offs

- **Reconnect gap** → A client whose Web PubSub socket drops during deletion may miss the `game-deleted` event. Mitigation: on reconnect the client reconciles by fetching `GET /games/{id}` and checks for `Deleted` status as a fallback — the same reconcile-on-reconnect behavior already used for every other event.
- **No migration needed** → Adding `Deleted = 4` to the enum is additive; the EF Core snapshot stores the numeric value. Existing rows are unaffected. Rollback path: remove the enum value and the new endpoint; no data loss.

## Open Questions

- Should the lobby page also display a "Game started — transitioning" banner when a `game-started` event arrives over Web PubSub? (Out of scope for this change; left as a follow-on.)
- Should deleted games be permanently purged after N days? (Out of scope; can be an independent maintenance job.)
