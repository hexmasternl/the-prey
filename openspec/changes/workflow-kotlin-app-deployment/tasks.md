# Tasks

## 1. Legacy clean-up and hand-over

- [x] 1.1 Confirm the Ionic and MAUI release workflows are gone; verify `ls .github/workflows` lists no `android-release.yml` (old Ionic), `maui-android-release.yml` or `maui-ios-release.yml`, and `grep -rnE "src/ThePrey|src/Maui|maui-(android|ios)-release|MAUI_ANDROID_" .github docs CLAUDE.md README.md` returns nothing
- [x] 1.2 Narrow `openspec/changes/workflow-centralized-versioning`: remove its tasks section 4 and task 6.1, the "Android app release after publish" and "Increasing app version code" requirements from its spec delta, design sections 4–5 and migration steps 2–3, and add a one-line pointer to `workflow-kotlin-app-deployment` in its proposal; verify `openspec validate workflow-centralized-versioning` passes and `grep -n "android-release" openspec/changes/workflow-centralized-versioning -r` only hits the pointer

## 2. Release notes in the create-release action

- [ ] 2.1 Add an optional `notes-file` input to `.github/actions/create-release/action.yml` (create the action per `workflow-centralized-versioning` task 2 first if it does not exist): non-empty file → `gh release create … --notes-file`, otherwise `--generate-notes`; existing release stays untouched; input passed via `env:`; update the usage comment; verify with `actionlint` and by running the script body locally against an existing tag (skip) and with an empty notes file (falls back to generated notes)

## 3. Workflow skeleton and verification

- [ ] 3.1 Add `.github/workflows/android-release.yml` with `push`/`pull_request` on `main` for `app/src/**` + the workflow file, `workflow_dispatch` without inputs, concurrency `android-release-${{ github.ref }}` without cancel, `permissions: contents: read`, `env: VERSION_CODE_OFFSET: "0"`, and the `version` job using `version.yml`; verify with `actionlint`
- [ ] 3.2 Add the `verify` job: JDK 17 temurin, `gradle/actions/setup-gradle`, `./gradlew lintDebug testDebugUnitTest` in `app/src`, upload lint/test reports on failure; verify the same command passes locally from `app/src`

## 4. Versioned, signed bundle

- [ ] 4.1 Add the `publish` job (`needs: [version, verify]`, only on non-PR runs of `refs/heads/main`, `permissions: contents: write, actions: read`) with a version step that fails on empty `semVer` or non-empty `preReleaseTag`, computes `versionCode = major*1000000 + minor*1000 + patch + VERSION_CODE_OFFSET` and writes both to the step summary; verify the arithmetic in a shell: `1.1.8` → `1001008`, `1.1.120` > `1.1.99`, `1.2.0` > `1.1.120`
- [ ] 4.2 Add the signing steps: fail when any of `KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`, `KEY_PASSWORD`, `KEY_ALIAS` is empty, decode the keystore to `$RUNNER_TEMP`, write `app/src/key.properties` (absolute `storeFile`), all secrets via `env:`; verify by reading the workflow that no `${{ secrets.* }}` appears inside a `run:` block
- [ ] 4.3 Run `./gradlew bundleRelease` with `ORG_GRADLE_PROJECT_versionCode`/`_versionName` and upload `app/build/outputs/bundle/release/app-release.aab` as an artifact (3-day retention, `if-no-files-found: error`); verify locally with a throwaway keystore that `bundletool dump manifest` shows the passed `versionCode`/`versionName`

## 5. Bilingual release notes

- [ ] 5.1 Add a step that resolves the commit range (base = `headSha` of the last successful `android-release.yml` run on `main` via `gh run list`, falling back to the last 30 app commits when none exists or it is not an ancestor) and writes `git log base..head -- app/src` to a file; verify locally that the command lists only commits touching `app/src`
- [ ] 5.2 Add the Copilot step: `setup-node`, pinned `@github/copilot`, non-interactive prompt with the commit log asking for JSON `{"en","nl"}` (user-facing, no hashes, informal Dutch, ≤ 500 chars each, no tools), `COPILOT_GITHUB_TOKEN` via `env:`, 3-minute timeout, `continue-on-error`; verify by running the same prompt locally with the Copilot CLI and checking the JSON with `jq`
- [ ] 5.3 Add the notes step: validate the JSON with `jq`, write `whatsnew/whatsnew-en-US`, `whatsnew/whatsnew-nl-NL` (cut to ≤ 500 chars at a line break) and `release-notes.md` (English + Dutch sections); on any failure write the fallback (commit subjects; Dutch "Verbeteringen en bugfixes." + list) and emit `::warning::`; verify locally with a valid JSON, an over-long text and garbage input that all three files are written and each whatsnew file is ≤ 500 characters

## 6. Publish and release

- [ ] 6.1 Add `r0adkll/upload-google-play@v1` with `serviceAccountJsonPlainText: PLAY_SERVICE_ACCOUNT_JSON`, `packageName: nl.hexmaster.theprey`, `releaseFiles` = the AAB, `track: internal`, `status: completed`, `whatsNewDirectory: whatsnew`; verify by reading the workflow that no other track value appears
- [ ] 6.2 Add `./.github/actions/create-release` as the last step with `version: needs.version.outputs.semVer` and `notes-file: release-notes.md`, without `if: always()`; verify with `actionlint` and that it is ordered after the Play upload

## 7. Documentation

- [ ] 7.1 Add an "App releases" section to `CLAUDE.md` (Android App part): workflow path, internal-track-only, `versionCode` formula + `VERSION_CODE_OFFSET`, Copilot bilingual notes + fallback, required secrets incl. `COPILOT_GITHUB_TOKEN`; update the `Versioning` bullet to point at it; verify the section names the same file paths and secret names as the workflow

## 8. Integration

- [ ] 8.1 Configure the secrets and Play Console (service account release rights, internal testers list), read the highest `versionCode` in Play and set `VERSION_CODE_OFFSET` if needed; open a PR with the workflow and confirm only `version` and `verify` run
- [ ] 8.2 After merge, run the workflow via `workflow_dispatch` on `main` and confirm: the bundle is on the internal testing track with the expected `versionName`/`versionCode`, the English and Dutch "What's new" texts are shown in the Play Console, and the GitHub release `v<semVer>` exists with both languages in its body (or was left as is because a backend run created it)
