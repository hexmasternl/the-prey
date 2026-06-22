# Medium — SSE JWT passed in the query string risks token capture

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Token hygiene / information disclosure |
| **Component** | Games API — SSE stream endpoints |
| **Status** | Open |

## Summary

The SSE stream endpoints accept the full Auth0 access token as a `?token=` query-string parameter (because browser `EventSource` cannot set an `Authorization` header). Tokens in URLs are prone to capture in server logs, proxy logs, and telemetry, where they remain replayable until expiry.

## Evidence

`src/Games/HexMaster.ThePrey.Games.Api/Program.cs:23-39` — the JWT bearer `OnMessageReceived` hook reads the token from `Request.Query["token"]` for `/games/{id}/stream` and `/games/{id}/lobby/stream`:

```csharp
options.Events = new JwtBearerEvents {
  OnMessageReceived = context => {
    var token = context.Request.Query["token"];
    if (!string.IsNullOrEmpty(token)) context.Token = token;
    return Task.CompletedTask;
  }
};
```

The application itself does not log the token, and the default OpenTelemetry ASP.NET Core instrumentation does not tag query strings — but ASP.NET request logging, YARP, Azure Container Apps ingress, and many proxies routinely record full request URLs including the query string.

## Impact

A valid bearer token that lands in logs is usable by anyone with log/telemetry access until it expires (the Auth0 access-token lifetime). This is a real exposure for long-lived streaming connections whose URLs are most likely to be logged.

## Recommendation

1. **Prefer a short-lived, single-purpose stream ticket** instead of the full JWT in the URL: exchange the bearer token (sent normally) for a one-time stream token, and pass only that in the query string.
2. If the query token remains, **redact the `token` parameter** at the ingress/YARP/App Insights layers and **keep access-token lifetime short**.
3. Where clients can, prefer the Web PubSub transport (group-scoped, short-lived minted token) over SSE-with-query-token.
