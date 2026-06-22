# High — Table Storage allows shared-key access, bypassing the RBAC model

| | |
|---|---|
| **Severity** | High |
| **Category** | Identity / access control |
| **Component** | Infra — Users & PlayFields storage accounts |
| **Status** | Open |

## Summary

The Users and PlayFields modules are designed for passwordless, managed-identity access to Azure Table Storage. However, the storage accounts do not set `allowSharedKeyAccess: false`, so it **defaults to true** — meaning the account keys remain fully valid, privileged credentials that bypass the intended RBAC model.

## Evidence

`infra/modules/storage-tables.bicep:15-28` (and identically `infra/landing-zone/modules/storage-queues.bicep:7-20`):

```bicep
properties: {
  accessTier: 'Hot'
  allowBlobPublicAccess: false
  minimumTlsVersion: 'TLS1_2'
  supportsHttpsTrafficOnly: true
  // allowSharedKeyAccess is NOT set -> defaults to true
}
```

The application code uses `DefaultAzureCredential` against the table endpoint (no key), and the Bicep correctly assigns the **Storage Table Data Contributor** role to each app's system-assigned identity — but leaving shared-key access enabled means anyone who obtains an account key (via `listKeys`, a misconfiguration, or an over-broad role) has full data-plane access regardless of RBAC.

## Impact

Account keys are root-equivalent for the storage account: full read/write/delete of all tables (user profiles, playfield geometries). They cannot be scoped, are hard to rotate without downtime, and undermine the auditability that identity-based access provides. Their continued validity defeats the purpose of the managed-identity design.

## Recommendation

1. Set `allowSharedKeyAccess: false` on both the `storage-tables` and `storage-queues` modules. Because the apps already authenticate via managed identity to the table endpoint, this should be functionally a no-op while closing the key-based path.
2. Consider `networkAcls { defaultAction: 'Deny' }` with the Container Apps environment allowed (see [medium-public-network-access-everywhere](./medium-public-network-access-everywhere.md)).
3. Add a policy/CI check (e.g. Azure Policy "Storage accounts should disallow shared key access") to prevent regressions.
