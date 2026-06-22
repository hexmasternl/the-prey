# Medium — Local auth (keys/SAS) left enabled on Service Bus, Web PubSub, and App Configuration

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Identity / access control |
| **Component** | Infra — landing zone resources |
| **Status** | Open |

## Summary

Several shared resources do not enforce identity-only access: shared keys / SAS connection strings remain valid alongside managed identity. In particular, the Dapr Service Bus pub/sub component is wired with the **root** SAS connection string (Manage/Send/Listen) rather than a scoped rule or managed identity.

## Evidence

- **Service Bus** — `infra/landing-zone/modules/service-bus.bicep:25-28` sets only `minimumTlsVersion`; no `disableLocalAuth: true`. The Dapr component uses the root SAS rule: `service-bus.bicep:31-34, 49-54` reference `RootManageSharedAccessKey`'s primary connection string.
- **Web PubSub** — `infra/landing-zone/modules/web-pubsub.bicep:24` has `properties: {}`; `disableLocalAuth` is not set, so access keys remain enabled.
- **App Configuration** — `infra/landing-zone/modules/app-configuration.bicep:11-12` explicitly sets `disableLocalAuth: false` (and `publicNetworkAccess: 'Enabled'`).

## Impact

Anyone who obtains a key/SAS (via `listKeys`, an over-broad role, or a leak) gains data-plane access independent of RBAC and audit. The root Service Bus SAS rule is especially powerful (full manage + send + listen on the whole namespace), so its exposure would compromise all messaging.

## Recommendation

1. Set `disableLocalAuth: true` on **Service Bus**, **Web PubSub**, and **App Configuration** (the APIs already authenticate to App Config via managed identity).
2. For the Dapr Service Bus component, switch to **managed-identity** auth (workload identity metadata) instead of a connection string. If a SAS rule is unavoidable, create a **scoped Send+Listen** authorization rule rather than reusing `RootManageSharedAccessKey`.
3. Verify Redis access is identity/key-scoped and consider disabling access keys there too.
