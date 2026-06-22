# Low — Dapr API-token filter fails open and uses non-constant-time comparison

| | |
|---|---|
| **Severity** | Low |
| **Category** | Access control / hardening |
| **Component** | PlayFields & Users — DaprApiTokenEndpointFilter |
| **Status** | Open |

## Summary

The endpoint filter that protects internal Dapr endpoints bypasses verification entirely when the expected token is unset (fail-open), and compares tokens with a non-constant-time string comparison.

## Evidence

`src/PlayFields/HexMaster.ThePrey.PlayFields.Api/Endpoints/DaprApiTokenEndpointFilter.cs:9-17` (identical in `src/Users/...`):

```csharp
var expectedToken = ...; // DAPR_APP_API_TOKEN
if (!string.IsNullOrEmpty(expectedToken))
{
    var received = context.HttpContext.Request.Headers["dapr-api-token"];
    if (received != expectedToken)   // non-constant-time, first header value only
        return Results.Unauthorized();
}
// if expectedToken is empty -> filter is skipped entirely (fail open)
```

## Impact

- **Fail-open:** in any environment where `DAPR_APP_API_TOKEN` is missing/empty, the "protected" internal endpoints become anonymous.
- **Timing side-channel:** `!=` on strings is not constant-time, a theoretical token-recovery vector.

(This filter is also the recommended fix for [high-games-internal-membership-endpoint-unauthenticated](./high-games-internal-membership-endpoint-unauthenticated.md) and [high-notifications-event-endpoints-unauthenticated](./high-notifications-event-endpoints-unauthenticated.md) — so its robustness matters.)

## Recommendation

1. **Fail closed:** if `expectedToken` is not configured outside Development, reject the request (or refuse to start), rather than skipping the check.
2. Use `CryptographicOperations.FixedTimeEquals` over the UTF-8 bytes for the comparison.
3. Apply the (hardened) filter consistently across all internal endpoints, including Games.
