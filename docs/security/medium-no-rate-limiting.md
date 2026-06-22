# Medium — No rate limiting or anti-abuse on any endpoint

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Abuse / availability |
| **Component** | Backend (all modules) + gateway |
| **Status** | Open |

## Summary

There is no rate limiting anywhere in the backend. No `AddRateLimiter` / `UseRateLimiter` / `RequireRateLimiting` is registered, and the gateway does not throttle. Every write and security-sensitive endpoint is unbounded.

## Evidence

A repository-wide search for ASP.NET Core rate-limiting APIs returns nothing. Unthrottled, write-heavy or sensitive endpoints include:

- `POST /games/{id}/locations` — each call performs a DB write (`RecordLocation` → `UpdateAsync`).
- `POST /games/{id}/join` — join-code attempts (see [low-join-code-low-entropy-no-lockout](./low-join-code-low-entropy-no-lockout.md)).
- `POST /games/{id}/participants/{participantId}/tag` — repeated tag attempts.
- `GET /games/{id}/notifications/token` — Web PubSub token minting.

## Impact

- **Write amplification / DoS:** a client can flood location posts, driving database load and (combined with broadcast forging in [high-notifications-event-endpoints-unauthenticated](./high-notifications-event-endpoints-unauthenticated.md)) message storms.
- **Brute force:** join codes can be enumerated quickly with no lockout.
- **Resource abuse:** unlimited token minting and tag spam.

## Recommendation

1. Add ASP.NET Core rate limiting with **per-user (sub claim) partitioned** fixed/sliding windows on write and sensitive endpoints, with tighter limits on `join`, `tag`, and `notifications/token`.
2. Apply complementary throttling at the gateway (per-IP) to protect anonymous/edge paths.
3. For location posts, align the accepted rate with the server-driven cadence (reject posts arriving far faster than `nextLocationIntervalSeconds`).
