## Context

The solution currently uses Aspire service discovery with HttpClient for all inter-service communication. No Dapr infrastructure exists anywhere in the codebase. Other domain modules that need to enrich data with user display names (e.g., Games showing the hunter's callsign) have no reliable, performant way to fetch that information without coupling directly to the Users API's HTTP surface or going through the public YARP gateway — neither of which is appropriate for internal service-to-service calls.

This change introduces Dapr as the inter-service communication and caching layer for user identity resolution, establishing the pattern for future internal integrations.

## Goals / Non-Goals

**Goals:**

- Provide a strongly-typed `IUserResolver` that any domain module can inject and call
- Cache resolved users in the Dapr state store with a configurable TTL to avoid redundant cross-service calls
- Expose the Users API's internal resolution endpoint only to authenticated Dapr callers (Dapr App API Token)
- Keep the internal route invisible to external clients by omitting it from YARP

**Non-Goals:**

- Replacing the existing public Users API endpoints or YARP routes
- Implementing Dapr pub/sub or actor patterns
- Cache invalidation on user profile updates (out of scope for v1 — TTL expiry is the eviction mechanism)
- Full Dapr mTLS certificate management (handled automatically by Dapr runtime)

## Decisions

### Dapr as the inter-service transport (vs direct HttpClient)

**Decision**: Use `DaprClient.InvokeMethodAsync` for the integration library's fallback call to the Users service.

**Rationale**: Dapr service invocation provides built-in retries, mTLS, observability, and name-based addressing without requiring the caller to know the target's URL or port. This is the designated internal communication mechanism once Dapr is introduced; HttpClient with service discovery remains for public-facing calls only.

**Alternatives considered**: `HttpClient` with Aspire service discovery — works today but bypasses the security layer (Dapr API token) and adds another URL-coupling point. Rejected.

### Dapr state store for user caching (vs IDistributedCache / Redis)

**Decision**: Store resolved `UserDto` objects in the Dapr state store component (`statestore`) with a metadata TTL of 5 minutes.

**Rationale**: The Dapr state store abstraction decouples the storage backend (in-process, Redis, Azure Table Storage, etc.) from the consumer. The Aspire Dapr component can be swapped without code changes. `IDistributedCache` would require adding Redis separately and wiring another dependency; reusing the Dapr infrastructure avoids this.

**State key schema**: `user-subject:{subjectId}` — prefixed to avoid key collisions with other state store consumers. The subjectId is the Auth0 `sub` claim (e.g., `auth0|abc123`).

**TTL**: 5 minutes default, overridable via `UserResolverOptions` bound to `appsettings.json`. This balances freshness with call reduction.

### Dapr App API Token for internal endpoint security

**Decision**: The `GET /internal/users/{subjectId}` endpoint in Users.Api is protected by a custom `DaprApiTokenAuthorizationFilter` (or middleware) that validates the `dapr-api-token` HTTP header against the value of the `DAPR_APP_API_TOKEN` environment variable.

**Rationale**: Dapr's App API Token feature is the idiomatic way to require that calls to an app come exclusively through the Dapr sidecar. Any direct HTTP call without the token header receives `401 Unauthorized`. This is simpler than JWT validation (the caller is a service, not a user) and simpler than mTLS cert management.

**Alternatives considered**: Require JWT bearer token for internal calls — overly complex; internal service callers do not naturally hold user JWTs. Network policy / service mesh ACL — not available in local dev; rejected for local parity.

### `IUserResolver` lives in `Users.Integration`, not `Users.Abstractions`

**Decision**: `IUserResolver` and `UserResolver` are defined in the new `HexMaster.ThePrey.Users.Integration` class library, not in `HexMaster.ThePrey.Users.Abstractions`.

**Rationale**: Per ADR 0002, the `Abstractions` project holds DTOs and cross-module service ports (data contracts). `IUserResolver` requires a `DaprClient` dependency, which is infrastructure — it should not pollute the Abstractions project. Consumers reference `Users.Integration` directly to get both the interface and the DI registration extension.

### Internal route prefix `/internal/users/`

**Decision**: The new route group is `MapGroup("/internal/users")`, yielding the endpoint `GET /internal/users/{subjectId}`.

**Rationale**: The `/internal/` prefix signals clearly that these routes are not meant for external consumption, and its absence from the YARP route table enforces this at the gateway level. The pattern is extensible for future internal endpoints in other modules.

### `UserResolver` error handling — propagate, do not swallow

**Decision**: If both the state store read and the service invocation fail, `ResolveUser` throws. Callers are responsible for deciding whether to degrade gracefully.

**Rationale**: Silently returning `null` would hide errors and cause subtle runtime failures (NullReferenceException in callers). Explicit failure surfaces the problem immediately.

### Aspire Dapr wiring — `WithDaprSidecar()` only on Users.Api for v1

**Decision**: For the initial change, only `Users.Api` is wired with a Dapr sidecar in the AppHost. Other services that consume `IUserResolver` will have their own sidecar added in a follow-up change.

**Rationale**: Incremental adoption; avoids changing all services at once. The `Users.Integration` library uses `DaprClient` which is registered independently in each consuming service.

## Risks / Trade-offs

- **Dapr is new to the project** → All developers must install the Dapr CLI and runtime locally. Mitigation: document the `dapr init` prerequisite in the README; Aspire will surface errors clearly if the sidecar is missing.
- **State store TTL is not invalidated on user profile update** → A user who changes their display name may see stale data for up to 5 minutes. Mitigation: acceptable for v1; a Dapr pub/sub invalidation event can be added later.
- **`DAPR_APP_API_TOKEN` must be kept in sync** → The secret must match between the sidecar config and the app env var. Mitigation: manage via a single Aspire secret / environment variable reference so the value is set once.
- **DaprClient JSON serialization** → The `UserDto` record must be JSON-serializable (it already is as a `sealed record`). Mitigation: no custom serializer needed; verify with a round-trip test.
- **Local Dapr dev dependency** → Developers who don't have Dapr installed will fail at Aspire startup. Mitigation: Aspire's `AddDapr()` gracefully defers sidecar errors in development if configured with `RunAsEmulator`-style options.

## Open Questions

- Should the state store TTL be expressed in seconds or minutes in `appsettings.json`? Recommendation: seconds (`CacheTtlSeconds: 300`) for precision, matching Dapr's state store metadata convention.
- Should `UserResolver` be registered as `Scoped` or `Singleton`? DaprClient is singleton-safe; recommend `Singleton` for `UserResolver` to avoid allocation overhead per request.
