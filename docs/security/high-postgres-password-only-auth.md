# High — Postgres uses password-only auth with a static, non-vaulted credential

| | |
|---|---|
| **Severity** | High |
| **Category** | Identity / secret management |
| **Component** | Infra — Games PostgreSQL + games.yml |
| **Status** | Open |

## Summary

The Games database authenticates with a username/password only. Entra (Azure AD) authentication is not enabled. The admin password is supplied from a long-lived GitHub Actions secret, embedded into a plaintext connection-string variable, and never stored in Key Vault. This is inconsistent with the rest of the platform, where Table Storage uses managed identity.

## Evidence

- `infra/games/main.bicep:33-38` — `pgAdminLogin` is a plaintext param; `pgAdminPassword` is `@secure()` (good), but…
- `infra/games/main.bicep:43` — the connection string is built as a **non-secure `var`** embedding the password:

  ```bicep
  var pgConnectionString = 'Host=...;Username=${pgAdminLogin};Password=${pgAdminPassword};SslMode=Require'
  ```

- `infra/games/modules/games-data.bicep:24-38` — server sets `administratorLoginPassword` but has **no `authConfig`** (no `activeDirectoryAuth: 'Enabled'`), so password auth is the only method.
- `.github/workflows/games.yml:133` — the password is injected from `secrets.POSTGRES_ADMIN_PASSWORD` at deploy time; it is a static credential, not a Key Vault reference.

> Logging note: Bicep redacts `@secure()` params and Container Apps `secrets[].value`, and the GitHub secret is masked in Actions logs, so no plaintext leak into logs was observed. The issue is the **auth model and credential lifetime**, not log leakage.

## Impact

- A single static, long-lived password protects the database. If it leaks (logs, a developer's machine, a misconfigured environment), an attacker with network reach (see [critical-postgres-network-exposure](./critical-postgres-network-exposure.md)) gains full data access.
- Password rotation is manual and easy to neglect; there is no per-workload identity or automatic credential expiry.

## Recommendation

1. **Enable Entra authentication** on the Flexible Server (`authConfig: { activeDirectoryAuth: 'Enabled', passwordAuth: 'Disabled' }`), set the Games container app's managed identity as an AAD admin/role, and connect passwordlessly via Npgsql (`Authentication=Active Directory Managed Identity`). This removes the static secret entirely.
2. If password auth must remain temporarily, **store the password in Key Vault** and reference it (the games workflow already supports Key Vault rotation), and rotate on a schedule.
3. Keep the connection string out of any non-secure output.
