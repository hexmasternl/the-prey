# Medium — Key Vault has short soft-delete retention and no purge protection

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Resilience / data protection |
| **Component** | Infra — key-vault.bicep |
| **Status** | Open |

## Summary

The Key Vault enables soft-delete with the minimum 7-day retention and does **not** enable purge protection. Without purge protection, a principal with sufficient rights (or a compromised one) can permanently purge secrets, and the recovery window is only 7 days.

## Evidence

`infra/landing-zone/modules/key-vault.bicep:13-16`:

```bicep
enableSoftDelete: true
softDeleteRetentionInDays: 7
// enablePurgeProtection NOT set -> defaults to off
```

## Impact

A malicious or mistaken purge irreversibly destroys vaulted secrets (the vault is intended to hold credentials such as the Postgres password). With purge protection off, soft-deleted secrets can be hard-deleted before the retention window helps, undermining recovery and enabling a destructive attack.

## Recommendation

1. Set `enablePurgeProtection: true`.
2. Raise `softDeleteRetentionInDays` to `90` for a production vault.
3. Confirm only the deployment identity has management rights and that data-plane access stays scoped (the read-only Secrets User / App Config Data Reader assignments are already appropriate).
