# Low — Android release workflow grants broad token permissions

| | |
|---|---|
| **Severity** | Low |
| **Category** | Least privilege (CI) |
| **Component** | CI — android-release.yml |
| **Status** | Open |

## Summary

The Android release workflow sets `permissions: contents: write` at the top level (workflow-wide), and the same job that builds also handles the keystore and the Play service-account JSON. The backend workflows, by contrast, scope `contents: write` narrowly to their release job.

## Evidence

- `.github/workflows/android-release.yml:18-19` — top-level `permissions: { contents: write }`.
- `.github/workflows/android-release.yml:90-98, 133` — the job decodes the keystore, writes `key.properties`, and passes `KEYSTORE_PASSWORD` / `KEY_PASSWORD` / `PLAY_SERVICE_ACCOUNT_JSON`.

## Impact

A broad workflow token combined with multiple high-value secrets in one job widens the blast radius if any step — or an unpinned third-party action (see [low-github-actions-unpinned](./low-github-actions-unpinned.md)) — is compromised: the attacker could both exfiltrate signing secrets and write to the repository.

## Recommendation

1. Scope `contents: write` to only the job/step that needs it; keep the build/publish job at `contents: read`.
2. Ensure no artifact upload includes the `android/` working directory (which transiently holds `key.properties` and `keystore.jks`).
3. Consider a dedicated GitHub Environment with required reviewers for production Play releases.
