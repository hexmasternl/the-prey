# Critical — Live Auth0 M2M client secret committed in git history

| | |
|---|---|
| **Severity** | Critical |
| **Category** | Secret exposure |
| **Component** | Mobile client / Auth0 tenant |
| **Status** | Open — requires secret rotation |

## Summary

An Auth0 **machine-to-machine (M2M) client secret** (and its client ID) was committed to the repository in `src/ThePrey/src/environments/environment.prod.ts`. The value has since been removed from the working tree, but it remains permanently retrievable from git history on commits that are ancestors of `main`. Deleting a secret from `HEAD` does **not** revoke it — the only effective remediation is rotation in Auth0.

## Evidence

The current `environment.prod.ts` is clean and explicitly notes that no M2M credentials are stored in the app. However, the secret is reachable from history:

```
# commit 12a11ce (and a5c4a51), both ancestors of main
src/ThePrey/src/environments/environment.prod.ts:
  auth0ClientId:     'uHNkakrYJwnsPoI7LgP1VK89WdAhg8VQ'
  auth0ClientSecret: 'uBuuiDOxJoOaTC8sA8JN5rVm1-0690vFYmjW_QG9Jnzx3i8UHFC1FaUy8fbmJ_vZ'
```

`git merge-base --is-ancestor 12a11ce main` confirms the commit is reachable from `main`; `git show 12a11ce:src/ThePrey/src/environments/environment.prod.ts` returns the secret. This matches a previously recorded note that an M2M secret was committed and removed from code but not yet rotated.

> Note: the *current* SPA `clientId` (`tJrm2nPrAX4kES7XEnjUsL38cqbAbraJ`) and Auth0 domain in `environment.ts` are **public** identifiers and are not a finding. This finding concerns the **M2M client secret**, which is a true credential.

## Impact

An Auth0 M2M client secret allows non-interactive issuance of access tokens for the tenant `theprey.eu.auth0.com` via the client-credentials grant. Anyone who has cloned the repository or viewed it on GitHub can extract the secret and:

- mint tokens and call any API that trusts that M2M application's grants/scopes;
- act with whatever permissions the M2M application was granted in Auth0 (potentially management-level depending on its grants).

Because the public mobile client uses the **user's interactive session** (not M2M), this secret should not have shipped in the client at all.

## Recommendation

1. **Rotate immediately.** In the Auth0 dashboard, rotate the secret for the affected M2M application (client ID `uHNkakrYJwnsPoI7LgP1VK89WdAhg8VQ`). If the application is unused, **delete it**.
2. **Review its grants/scopes** — if it had Management API access, audit recent token activity in the Auth0 logs for misuse.
3. **(Secondary) Purge from history** with `git filter-repo` or BFG and force-push, after coordinating with the team. This reduces future copy-paste risk but is *not* a substitute for rotation.
4. **Prevent recurrence:** enable push-protection / secret scanning on the repository and add a pre-commit secret scan; keep all real secrets out of `environment*.ts` (they are bundled into the shipped app).
