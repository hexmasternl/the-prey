# Tasks

## 1. Shared versioning

- [x] 1.1 Update the header comment of `.github/workflows/version.yml` to state it is the only place GitVersion runs and that `major`/`minor`/`patch` feed the app `versionCode`; verify `grep -rl "gitversion/execute" .github` lists only `version.yml`

## 2. Create-release action

- [x] 2.1 Add `.github/actions/create-release/action.yml` (composite, `shell: bash`) with inputs `version` (required), `target` (default `${{ github.sha }}`), `token` (default `${{ github.token }}`) and outputs `tag`, `created`; inputs reach the script only via `env:`; verify `actionlint` (or `npx @action-validator/cli`) reports no errors
- [x] 2.2 Implement the script: strip a leading `v`/`V`, fail with `::error::` on an empty version, `gh release view` → skip with `created=false`, else `gh release create "$TAG" --target --title "$TAG" --generate-notes`, on failure re-check and treat an existing release as success, otherwise exit 1; verify locally by running the script body with `GH_TOKEN` against the repo using an existing tag (`1.1.7` → skip, `created=false`) and an empty version (→ exit 1)
- [x] 2.3 Add a short usage comment at the top of `action.yml` (checkout required, job needs `contents: write`, example call); verify it matches how the workflows call it

## 3. Backend workflows

- [x] 3.1 In `games.yml`, replace the "Create tag and GitHub release" script with `uses: ./.github/actions/create-release` and `version: ${{ needs.version.outputs.semVer }}`; drop `fetch-depth: 0` from that job's checkout; keep `needs: [version, deploy]`, the `push` condition and `contents: write`; verify with `actionlint`
- [x] 3.2 Apply the same change to `notifications.yml`, `playfields.yml` and `users.yml`; verify `grep -n "gh release" .github/workflows/*.yml` returns nothing
- [ ] 3.3 Verify on a real run: push a change to one backend module on `main` and confirm the Tag & Release job logs the tag and `created=true` (new version) or the skip message (existing version) — **auto: needs human:** needs a real push to `main` and a GitHub Actions run (pushing is out of scope here)

## 4. Android app release workflow

- [ ] 4.1 Add `.github/workflows/android-release.yml` with triggers (`push` to `main` on `app/src/**` + the workflow file, `pull_request` on the same paths, `workflow_dispatch` with a `track` choice defaulting to `internal`), concurrency `android-release-${{ github.ref }}` without cancel, and a `version` job using `version.yml`; verify with `actionlint` — **auto:** owned by `workflow-kotlin-app-deployment` (its task 1.2 removes this task here)
- [ ] 4.2 Add a `test` job: JDK 17 (temurin), Gradle setup, `./gradlew lintDebug testDebugUnitTest` in `app/src`; verify it passes on the pull request run — **auto:** owned by `workflow-kotlin-app-deployment` (its task 1.2 removes this task here)
- [ ] 4.3 Add the `build-and-publish` job (skipped for `pull_request`, `contents: write`): fail when `semVer` is empty, compute `versionCode = major*1000000 + minor*1000 + patch + VERSION_CODE_OFFSET`, write both to the step summary; verify the arithmetic in a shell for `1.1.8` → `1001008`, `1.1.120` > `1.1.99`, and `1.2.0` > `1.1.120` — **auto:** owned by `workflow-kotlin-app-deployment` (its task 1.2 removes this task here)
- [ ] 4.4 In the same job: decode `KEYSTORE_BASE64` into `app/src/app/` and write `app/src/key.properties` from `KEYSTORE_PASSWORD`, `KEY_PASSWORD`, `KEY_ALIAS`; run `./gradlew bundleRelease` with `ORG_GRADLE_PROJECT_versionCode`/`_versionName`; upload the AAB as an artifact (3-day retention, `if-no-files-found: error`); verify the artifact's AAB has the expected version (`bundletool dump manifest`) — **auto:** owned by `workflow-kotlin-app-deployment` (its task 1.2 removes this task here)
- [ ] 4.5 Publish with `r0adkll/upload-google-play@v1` (`packageName: nl.hexmaster.theprey`, `track` from input or `internal`, `status: completed`), then call `./.github/actions/create-release` with the same `semVer` as the last step; verify by reading the workflow that the release step has no `if: always()` and follows the upload — **auto:** owned by `workflow-kotlin-app-deployment` (its task 1.2 removes this task here)

## 5. Documentation

- [x] 5.1 Add a "Releases & versioning" section to `CLAUDE.md` (version from `version.yml` only, releases via the create-release action only, shared `v<semVer>` line, app `versionCode` formula and required secrets); verify the section names the same file paths as the workflows

## 6. Integration

- [ ] 6.1 Configure the app secrets, check the highest `versionCode` in the Play Console and set `VERSION_CODE_OFFSET` if it exceeds the formula; run `android-release.yml` via `workflow_dispatch` on `internal` and confirm the bundle lands in Play and a release `v<semVer>` exists (or was skipped because a backend run created it) — **auto:** owned by `workflow-kotlin-app-deployment` (its task 1.2 removes this task here)
