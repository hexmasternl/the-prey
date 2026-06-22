# Security Assessment — The Prey

**Date:** 2026-06-22 · **Scope:** backend (`src/**`), mobile client (`src/ThePrey`), infrastructure (`infra/**`), and CI/CD (`.github/workflows/**`) · **Method:** source-level review against the current `main`/working tree.

This folder contains one file per finding. File names follow `{severity}-{short-description}.md`. Each file has a full description, concrete evidence (file:line), impact, and a remediation. This is a point-in-time assessment — re-run it as the code evolves.

> **Most urgent:** rotate the leaked Auth0 M2M secret ([critical-auth0-m2m-secret-in-git-history](./critical-auth0-m2m-secret-in-git-history.md)) and lock down Postgres networking ([critical-postgres-network-exposure](./critical-postgres-network-exposure.md)).

## Findings

### Critical
| Finding | Area |
|---|---|
| [critical-auth0-m2m-secret-in-git-history](./critical-auth0-m2m-secret-in-git-history.md) | Secrets |
| [critical-postgres-network-exposure](./critical-postgres-network-exposure.md) | Infra / network |

### High
| Finding | Area |
|---|---|
| [high-notifications-event-endpoints-unauthenticated](./high-notifications-event-endpoints-unauthenticated.md) | Backend / authz |
| [high-games-internal-membership-endpoint-unauthenticated](./high-games-internal-membership-endpoint-unauthenticated.md) | Backend / authz |
| [high-postgres-password-only-auth](./high-postgres-password-only-auth.md) | Infra / identity |
| [high-storage-shared-key-access-enabled](./high-storage-shared-key-access-enabled.md) | Infra / identity |
| [high-webpubsub-service-owner-overprivileged](./high-webpubsub-service-owner-overprivileged.md) | Infra / RBAC |

### Medium
| Finding | Area |
|---|---|
| [medium-no-rate-limiting](./medium-no-rate-limiting.md) | Backend / abuse |
| [medium-client-location-spoofing-trusted](./medium-client-location-spoofing-trusted.md) | Backend / game integrity |
| [medium-sse-jwt-in-query-string](./medium-sse-jwt-in-query-string.md) | Backend / tokens |
| [medium-auth-tokens-in-localstorage](./medium-auth-tokens-in-localstorage.md) | Client / tokens |
| [medium-android-allowbackup-enabled](./medium-android-allowbackup-enabled.md) | Client / data-at-rest |
| [medium-data-plane-local-auth-enabled](./medium-data-plane-local-auth-enabled.md) | Infra / identity |
| [medium-public-network-access-everywhere](./medium-public-network-access-everywhere.md) | Infra / network |
| [medium-loganalytics-shared-key](./medium-loganalytics-shared-key.md) | Infra / secrets |
| [medium-keyvault-no-purge-protection](./medium-keyvault-no-purge-protection.md) | Infra / resilience |

### Low
| Finding | Area |
|---|---|
| [low-join-code-low-entropy-no-lockout](./low-join-code-low-entropy-no-lockout.md) | Backend / abuse |
| [low-dapr-token-filter-fails-open](./low-dapr-token-filter-fails-open.md) | Backend / authz |
| [low-no-global-exception-handler](./low-no-global-exception-handler.md) | Backend / hardening |
| [low-design-time-postgres-credentials](./low-design-time-postgres-credentials.md) | Backend / secrets |
| [low-android-cleartext-traffic-not-disabled](./low-android-cleartext-traffic-not-disabled.md) | Client / transport |
| [low-github-actions-unpinned](./low-github-actions-unpinned.md) | CI / supply chain |
| [low-android-release-workflow-broad-permissions](./low-android-release-workflow-broad-permissions.md) | CI / least privilege |

## Verified OK (not findings)

These were checked and found sound: JWT validation (issuer/audience/signature/lifetime, `MapInboundClaims=false`, no `RequireHttpsMetadata=false`); CORS is an explicit allow-list **without** `AllowCredentials`/`AllowAnyOrigin`; ownership/role checks (owner-only, hunter-only, participant-only) are enforced server-side in domain logic, not trusted from client input; no raw/concatenated SQL or Table Storage filter injection (parameterized commands + typed LINQ); game/join codes use a cryptographic RNG; the signing keystore (`theprey-release.jks`) and `key.properties` are git-ignored and were never committed; the client token interceptor attaches the bearer token only to API-origin requests and never logs it; no XSS sinks (`innerHTML`/`bypassSecurityTrust`/`eval`) in the client; deep-link `gameId` is regex-sanitized; CI uses Azure OIDC federation (no long-lived cloud secret) and no `pull_request_target`; Container Apps ingress enforces HTTPS (`allowInsecure:false`) and storage/Redis/Service Bus/Postgres all enforce TLS 1.2+; Key Vault uses RBAC with scoped read-only roles; the ACR-pull identity is image-pull-only and separate from each app's system-assigned data-plane identity.

## Severity guide

| Severity | Meaning |
|---|---|
| **Critical** | Direct path to credential compromise or data exposure; fix immediately |
| **High** | Significant exposure or missing control; fix soon |
| **Medium** | Meaningful weakness, usually needing a precondition or raising blast radius |
| **Low** | Hardening / defense-in-depth; fix opportunistically |
