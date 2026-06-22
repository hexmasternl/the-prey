# Improvements — Security & Compliance

The Prey continuously collects **real-time location of real people outdoors**. That makes privacy and access control first-order concerns, not afterthoughts.

## 1. Location-data privacy & retention policy — Impact: High · Effort: M

GPS history is stored (the engine consumes readings; `analysis/*.json` shows full tracks are retained).

- Define a retention policy: how long raw location history lives after a game ends, and auto-purge beyond it.
- Decide who can ever see a player's track (post-game replay should respect this — see [product replay idea](./product-and-gameplay.md#1-post-game-replay--impact-high--effort-sm)).
- Provide data export/delete to satisfy GDPR subject requests (players are likely EU — Auth0 tenant is `theprey.eu`).
- Surface a clear in-app privacy notice about background location use (also an app-store requirement).

## 2. SSE token-in-URL handling — Impact: Med · Effort: S

SSE endpoints accept the JWT as `?token=` because `EventSource` can't set headers. Tokens in URLs can leak via logs, proxies, and referrers.

- Keep these access tokens short-lived and scoped.
- Prefer the Web PubSub path (group-scoped, short-lived minted token) for clients that can use it; treat SSE-with-query-token as the documented fallback only.
- Ensure gateway/Container Apps access logs don't persist the query string for the stream routes.

## 3. Authorization test coverage on every endpoint — Impact: High · Effort: M

Role/state rules are enforced in handlers (owner-only, hunter-only, participant-only, taggable-only). These are exactly the checks that regress silently.

- Add explicit authorization tests per endpoint: non-owner can't start/kick/config/end, non-hunter can't tag, non-participant can't stream, can't tag a `Passive`/`Tagged` target.
- Verify `/internal/...` endpoints are unreachable through the public gateway and only via Dapr.

## 4. Abuse & cheat resistance — Impact: Med · Effort: M

Location is self-reported by the client, which a determined player can spoof.

- Server-side sanity checks: implausible speed/teleport between readings, accuracy floors, timestamp drift bounds.
- Rate-limit location posts and lobby/join actions at the gateway.
- Validate boundary checks server-side (don't trust the client's "in bounds" claim) — pairs with [spatial indexing](./backend-and-architecture.md#6-spatial-indexing-for-public-playfields-impact-lowmed--effort-m).

## 5. Secret rotation & least privilege — Impact: Med · Effort: S

- Rotate the Postgres admin credential on a schedule (the games workflow supports it via Key Vault).
- Confirm each Container App's system-assigned identity has only the data-plane roles it needs (App Config / Key Vault / Web PubSub / its own store) and nothing broader.
- Audit that no live secrets are committed (a prior M2M secret incident is on record — keep CI secret-scanning on).

## 6. Dependency & supply-chain scanning — Impact: Med · Effort: S

- Enable automated dependency updates + vulnerability alerts for both the .NET solution and the npm client.
- Pin and scan container base images; fail the build on critical CVEs before pushing to ACR.

## 7. Auth0 tenant hardening — Impact: Med · Effort: S

- Review token lifetimes, refresh-token rotation, and allowed callback URLs for the native + web clients.
- Enforce that the API audience (`https://api.theprey.nl`) is required on every token and that scopes are validated where they matter.
