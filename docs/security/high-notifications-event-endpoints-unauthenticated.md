# High — Notifications event endpoints are anonymous and publicly routable

| | |
|---|---|
| **Severity** | High |
| **Category** | Broken access control / message forgery |
| **Component** | Notifications API + gateway |
| **Status** | Open |

## Summary

The Notifications module's Dapr pub/sub delivery endpoints (`POST /notifications/events/*`) are mapped with `.AllowAnonymous()`, have **no Dapr API-token verification**, and the gateway routes the entire `/notifications/*` prefix to the Notifications API. As a result, these "internal" endpoints are reachable from the public internet, and they broadcast whatever `gameId` and payload the caller supplies to that game's Web PubSub group.

## Evidence

`src/Notifications/HexMaster.ThePrey.Notifications.Api/Endpoints/NotificationSubscriptionEndpoints.cs:32-44, 57-71` — each topic handler is mapped with `.AllowAnonymous()` and no endpoint filter:

```csharp
group.MapPost("/player-location-updated", ...).AllowAnonymous().WithTopic(...);
// ...handler reads integrationEvent.GameId from the body and calls
// broadcaster.SendToGameAsync(gameId, eventType, payload, ct);
```

`src/Notifications/HexMaster.ThePrey.Notifications.Api/Program.cs:49-53` registers `UseCloudEvents()` + `MapSubscribeHandler()` but **no** `DaprApiTokenEndpointFilter` (which the PlayFields and Users internal endpoints do use).

`src/Aspire/ThePrey.Aspire.AppHost/AppHost.cs:97` routes the prefix publicly:

```csharp
yarp.AddRoute("/notifications/{**catch-all}", notificationsApi);
```

A code comment asserts these are "internal (sidecar → app) calls, so they are anonymous" — but the gateway exposure makes that assumption false.

## Impact

An unauthenticated attacker can `POST` a crafted event for **any** `gameId` and have it broadcast to that game's real players:

- spoof a prey's GPS location shown to the hunter (mislead the hunt);
- inject fake `player-status-changed` / `game-ended` / `player-penalized` events, corrupting or prematurely ending live games;
- harass or denial-of-service ongoing games at will.

No game membership or authentication is required.

## Recommendation

1. **Require Dapr app-API-token verification** on these endpoints (add `DaprApiTokenEndpointFilter`/`AppApiToken` check), matching the PlayFields/Users internal pattern, and fail closed if the token is unset (see [low-dapr-token-filter-fails-open](./low-dapr-token-filter-fails-open.md)).
2. **Do not route `/notifications/*` through the public gateway.** Remove that YARP route so the endpoints are only reachable by the in-cluster Dapr sidecar; expose only what clients legitimately need (clients consume real-time via Web PubSub, not via these endpoints).
3. Defense-in-depth: validate the event envelope (known topic, well-formed `gameId`) and confirm the originating service before broadcasting.
