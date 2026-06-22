# Critical — Postgres exposed to all Azure tenants with password-only auth

| | |
|---|---|
| **Severity** | Critical |
| **Category** | Network exposure / identity |
| **Component** | Infra — Games PostgreSQL Flexible Server |
| **Status** | Open |

## Summary

The Games PostgreSQL Flexible Server has a firewall rule that allows **all Azure services** (`0.0.0.0`), public network access is enabled (the default, not disabled), and the server uses **password authentication only**. The combination means the database is reachable at the network layer from any workload in *any* Azure tenant, with a single static password as the only barrier.

## Evidence

`infra/games/modules/games-data.bicep:44-51`:

```bicep
resource allowAzureServices ... firewallRules = {
  name: 'AllowAllAzureServicesAndResourcesWithinAzure'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}
```

The Flexible Server (`games-data.bicep:24-38`) sets no `network: { publicNetworkAccess: 'Disabled' }`, so public access defaults to **Enabled**, and no `authConfig` enabling Entra auth — it is password-auth only. The `0.0.0.0` rule is the Azure "Allow public access from any Azure service" rule: it is **not** limited to your subscription; it admits other tenants' VMs/Functions/containers too.

## Impact

- Any Azure-hosted workload globally can open a TCP connection to the server. The only remaining control is the admin password (`thepreyadmin`).
- A leaked, brute-forced, or reused password leads to full read/write of all game data (which includes player identifiers and historical GPS tracks — sensitive personal location data).
- There is no network-level isolation (no VNet/private endpoint) to contain a credential compromise.

## Recommendation

1. **Remove the `0.0.0.0` firewall rule.** Replace public access with **VNet integration / private endpoint**: place the Container Apps environment on a VNet, set `network.delegatedSubnetResourceId` and `network.privateDnsZoneArmResourceId`, and set `publicNetworkAccess: 'Disabled'`.
2. **If private networking is not immediately feasible,** restrict the firewall to the Container Apps environment's static outbound IP only — never `0.0.0.0`.
3. **Adopt Entra (managed-identity) authentication** for Postgres and disable password auth — see [high-postgres-password-only-auth](./high-postgres-password-only-auth.md).
4. Confirm `require_secure_transport`/`SslMode=Require` stays enforced (the connection string already requests `SslMode=Require`).
