# High — Games internal membership endpoint has no Dapr token check

| | |
|---|---|
| **Severity** | High |
| **Category** | Broken access control / information disclosure |
| **Component** | Games API (internal endpoints) |
| **Status** | Open |

## Summary

The Games module's internal membership-check endpoint is mapped `.AllowAnonymous()` with **no** `DaprApiTokenEndpointFilter`, unlike the equivalent internal endpoints in PlayFields and Users. Today it is not routed through the public gateway, so it is protected only by the accidental fact that the gateway's `/games/*` prefix doesn't overlap `/internal/*` externally — not by any enforced boundary.

## Evidence

`src/Games/HexMaster.ThePrey.Games.Api/Endpoints/InternalGameEndpoints.cs:16-19`:

```csharp
group.MapGet("/{gameId:guid}/members/{userId:guid}", IsMember)
     .AllowAnonymous();   // no .AddEndpointFilter<DaprApiTokenEndpointFilter>()
```

Compare the protected equivalents:
- `src/PlayFields/HexMaster.ThePrey.PlayFields.Api/Endpoints/InternalPlayFieldEndpoints.cs:16` → `.AddEndpointFilter<DaprApiTokenEndpointFilter>()`
- `src/Users/HexMaster.ThePrey.Users.Api/Endpoints/InternalUserEndpoints.cs:18` → `.AddEndpointFilter<DaprApiTokenEndpointFilter>()`

The Games internal endpoint is the only one of the three with no token verification.

## Impact

Any party that can reach the Games API directly — another container in the environment, a future/misconfigured gateway route, or an SSRF primitive — can query membership for arbitrary `gameId`/`userId` pairs, yielding a **membership oracle** (information disclosure about who is in which game). The inconsistency with the other two modules indicates an oversight rather than an intentional exception, and the protection (prefix non-overlap) is fragile.

## Recommendation

1. Add `.AddEndpointFilter<DaprApiTokenEndpointFilter>()` to the Games internal endpoint for parity with PlayFields/Users, and ensure the filter **fails closed** when the Dapr app-API-token is not configured (see [low-dapr-token-filter-fails-open](./low-dapr-token-filter-fails-open.md)).
2. Confirm no gateway route exposes `/internal/*` for any module, and add a test asserting internal endpoints reject requests lacking the Dapr token.
