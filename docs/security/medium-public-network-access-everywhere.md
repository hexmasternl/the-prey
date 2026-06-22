# Medium — Public network access enabled on every data/control-plane resource

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Network exposure |
| **Component** | Infra — landing zone + per-service |
| **Status** | Open |

## Summary

No private endpoints are used anywhere. Key Vault, App Configuration, Storage, Service Bus, Web PubSub, and Redis are all reachable over the public internet (gated only by auth), and every Container App has its own external ingress FQDN in addition to the gateway.

## Evidence

- **Key Vault** — `infra/landing-zone/modules/key-vault.bicep:16` `publicNetworkAccess: 'Enabled'`, no `networkAcls { defaultAction: 'Deny' }`.
- **App Configuration** — `app-configuration.bicep:12` `publicNetworkAccess: 'Enabled'`.
- **Storage (tables/queues)** — no `networkAcls` / public-access restriction in `storage-tables.bicep` / `storage-queues.bicep`.
- **Service Bus / Web PubSub / Redis** — no `publicNetworkAccess: 'Disabled'`.
- **Container Apps** — `infra/modules/container-app.bicep:117` `ingress.external: true` for **all** APIs, so each microservice has a public FQDN even though clients should reach them only through the managed gateway.

## Impact

A larger attack surface: every resource is internet-reachable, so a leaked key/credential is immediately exploitable from anywhere, and the per-service public ingress lets clients bypass the gateway's routing/observability. Defense relies entirely on authentication being correct everywhere.

## Recommendation

1. Where feasible, adopt **private endpoints** with a VNet-integrated Container Apps environment and set `publicNetworkAccess: 'Disabled'` on Key Vault, App Config, Storage, Service Bus, Web PubSub, and Redis.
2. Short term: add `networkAcls { defaultAction: 'Deny' }` to Key Vault and Storage, allowing only the Container Apps environment's outbound IP.
3. Set `ingress.external: false` for services that should only be reached through the managed gateway, leaving a public FQDN only where genuinely required.
