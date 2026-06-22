# Improvements — Operations & Observability

Production-readiness for the Azure deployment. See [deployment/azure-deployment.md](../deployment/azure-deployment.md).

## 1. Right-size real-time / state / messaging tiers — Impact: High · Effort: S

Several landing-zone resources are provisioned at development tiers that will throttle or cap real games:

| Resource | Current tier | Concern |
|---|---|---|
| Web PubSub | `Free_F1` | Hard caps on concurrent connections and daily messages — a handful of real games will exhaust it |
| Redis (Dapr state) | `Balanced_B0` | Dev-grade; size for the state read/write rate of concurrent sweeps |
| Service Bus | `Standard` | Fine to start; consider Premium for predictable throughput/isolation as volume grows |

- Make tiers environment-parameterized (dev vs. prod) so prod can scale without touching dev cost.

## 2. End-to-end real-time synthetic monitoring — Impact: High · Effort: M

The critical path is REST → sweep → Dapr → Notifications → Web PubSub → client. A break anywhere shows up as "the game feels dead," which is hard to diagnose reactively.

- A synthetic probe that posts a location and asserts the corresponding Web PubSub event arrives within an SLO, alerting on latency/loss.
- Dashboards for sweep duration, event publish/deliver counts, and Web PubSub connection counts (metrics already emitted via OTel).

## 3. Backup & disaster recovery — Impact: High · Effort: M

- Verify and document PostgreSQL Flexible Server backup retention + point-in-time restore (Games is the only stateful relational store).
- Confirm Table Storage redundancy (Users, PlayFields) and a restore runbook.
- Document the recovery story for the landing zone (it's the single point all services depend on via `existing` references).

## 4. Health checks & readiness gating — Impact: Med · Effort: S

- Ensure each Container App exposes liveness/readiness probes (ServiceDefaults provides health checks) and that ACA revisions gate traffic on readiness — especially the Games API, whose startup loads App Configuration via managed identity (a known 403 failure mode if identity is misconfigured).

## 5. Load & soak testing — Impact: Med · Effort: M

Before a public launch, validate the parts that only break under concurrency.

- Many concurrent games to test the single-leader sweep throughput (and inform the [sharding idea](./backend-and-architecture.md#4-shard-the-leaders-workload-impact-med--effort-l)).
- Web PubSub fan-out under realistic connection counts.
- Sustained location posting at production cadence to size Postgres and Redis.

## 6. Cost visibility — Impact: Med · Effort: S

- Tag resources by service/environment and stand up a cost dashboard; the per-service resource-group layout already makes attribution clean.
- Alert on anomalies (e.g. a Web PubSub message spike, runaway sweep).

## 7. Deployment safety rails — Impact: Med · Effort: S

- Adopt the staged-rollout / required-reviewer guidance from the [Android CI guide](../deployment/android-ci-deployment.md#8-recommended-hardening-once-the-basics-work) for backend services too (e.g. canary revision + manual promote on Container Apps).
- Smoke-test each service revision (a few authenticated calls through the gateway) before shifting 100% traffic.

## 8. Centralized config & secret hygiene — Impact: Med · Effort: S

- Keep all tunables in App Configuration (already the pattern for Web PubSub/Service Bus endpoints) with feature flags for risky changes.
- Audit Key Vault access and rotate the Postgres admin credential on a schedule (the games workflow already supports rotation via the Key Vault secret).
