# Medium — Container Apps environment ships logs to Log Analytics with a shared key

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Secret management |
| **Component** | Infra — container-apps-environment.bicep |
| **Status** | Open (may be platform-constrained) |

## Summary

The Container Apps environment authenticates to Log Analytics using the workspace `customerId` + **primary shared key**, a long-lived secret, rather than managed identity.

## Evidence

`infra/landing-zone/modules/container-apps-environment.bicep:12-18` configures `appLogsConfiguration` with `customerId` and `sharedKey`, where the shared key is surfaced from `infra/landing-zone/modules/log-analytics.bicep:21` via `listKeys().primarySharedKey`.

## Impact

The workspace primary shared key is a powerful credential — it permits log ingestion (and, depending on configuration, can aid log tampering/spoofing). It is long-lived and not scoped per workload. If exposed, it cannot be narrowly revoked without rotation.

## Recommendation

1. Where the region/SKU supports it, prefer `destination: 'azure-monitor'` with diagnostic settings and managed identity instead of the shared-key `appLogsConfiguration` model.
2. If the shared-key model is currently required by the platform, document the dependency and ensure the workspace key is rotated if ever exposed and that access to the deployment outputs/state is restricted.
