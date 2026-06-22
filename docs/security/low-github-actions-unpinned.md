# Low — Third-party GitHub Actions pinned to mutable tags

| | |
|---|---|
| **Severity** | Low |
| **Category** | Supply chain |
| **Component** | CI — .github/workflows |
| **Status** | Open |

## Summary

Workflows reference third-party actions by floating tags (e.g. `@v1`, `@v3`) rather than immutable commit SHAs. A compromised or retagged upstream release would execute with the workflow's permissions and secrets — most concerning for the actions that touch signing material and deployment tokens.

## Evidence

- `.github/workflows/android-release.yml:131` — `r0adkll/upload-google-play@v1` handles the **Play service-account JSON and signed AAB**.
- `.github/workflows/deploy-website.yml:50,70` — `peaceiris/actions-hugo@v3`, `Azure/static-web-apps-deploy@v1`.
- `.github/workflows/android-release.yml:56`, `version.yml:105,111` — `github/copilot-release-notes@v1`, `gittools/actions/...@v4.5.0`.
- First-party actions (`actions/checkout@v6`, `azure/login@v3`) are also tag-pinned (lower risk).

## Impact

If an upstream tag is moved to malicious code, that code runs in CI with access to whatever the job exposes — for the Android job, the keystore passwords and Play service-account JSON; for others, the Azure OIDC federation (`id-token: write`) or the Static Web Apps deploy token.

## Recommendation

1. Pin third-party actions to a full commit SHA with a version comment, e.g. `uses: r0adkll/upload-google-play@<sha> # v1.x`.
2. Prioritize the actions handling secrets/deploys (`upload-google-play`, `static-web-apps-deploy`, `actions-hugo`).
3. Enable Dependabot for GitHub Actions to keep pinned SHAs updated safely.
