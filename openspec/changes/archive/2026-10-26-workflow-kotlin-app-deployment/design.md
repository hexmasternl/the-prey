# Design

## Context

- The Ionic and MAUI workflows were deleted in `f9df432`. The Ionic `android-release.yml` from before that commit is the reference shape: keystore from `KEYSTORE_BASE64` into `key.properties`, `ORG_GRADLE_PROJECT_versionCode/_versionName`, `r0adkll/upload-google-play@v1` with `PLAY_SERVICE_ACCOUNT_JSON`, and a `github/copilot-release-notes@v1` step whose output was never passed to Play and was English only.
- `.github/workflows/version.yml` is the reusable GitVersion workflow (`semVer`, `majorMinorPatch`, `major`/`minor`/`patch`, `preReleaseTag`, …). `GitVersion.yml` uses `ContinuousDeployment` with an empty label on `main`, so `main` always yields a plain `x.y.z`.
- `workflow-centralized-versioning` (not yet applied) plans `.github/actions/create-release` — a composite action that creates `v<semVer>` tag + release idempotently — and also sketches an app workflow (its design section 4/5). This change takes over the app workflow and keeps that change's `versionCode` formula.
- `app/src/app/build.gradle.kts` reads `versionCode`/`versionName` from Gradle properties (fallback `1`/`0.1.0`), signs only when `app/src/key.properties` exists, and compiles with Java 17 targets.
- The `app-version-gate` spec compares the client version as numeric `major.minor.patch`, so `versionName` must never carry a pre-release suffix.
- The app ships English and Dutch (`values/`, `values-nl/`); the Play listing uses the `en-US` and `nl-NL` locales.

## Goals / Non-Goals

**Goals:**
- One app workflow: verify → version → sign → internal track → release notes → GitHub release.
- Release notes that a tester can read in their own language, without anyone writing them by hand.

**Non-Goals:**
- Promotion to closed/open/production tracks (manual in the Play Console).
- Staged rollouts, Play listing/screenshot updates, App Bundle attachment to the GitHub release.
- Changing `version.yml`, `GitVersion.yml` or the `versionCode` formula.
- iOS or any other client.

## Decisions

### 1. One workflow, three jobs
`.github/workflows/android-release.yml`, triggers: `push` to `main` and `pull_request` to `main`, both with paths `app/src/**` and the workflow file; `workflow_dispatch` without inputs. Concurrency `android-release-${{ github.ref }}`, `cancel-in-progress: false` (never cancel a half-finished upload). Top-level `permissions: contents: read`.

- `version` — `uses: ./.github/workflows/version.yml`.
- `verify` — JDK 17 (temurin) + `gradle/actions/setup-gradle`, `./gradlew lintDebug testDebugUnitTest` in `app/src`; uploads lint/test reports on failure.
- `publish` — `needs: [version, verify]`, `if: github.event_name != 'pull_request' && github.ref == 'refs/heads/main'`, `permissions: contents: write, actions: read`. Steps in order: version guard → versionCode → keystore → `bundleRelease` → artifact → release notes → Play upload → create-release. The GitHub release is the last step and has no `if: always()`, so it only runs after a successful upload.

Splitting notes or release into their own jobs was rejected: they need the same commit range and the AAB/notes files, and the release must follow the upload in the same run without extra artifact hand-offs.

### 2. Version guard and versionCode
`versionName = needs.version.outputs.semVer`; the step fails with `::error::` when it is empty or `preReleaseTag` is non-empty (protects the numeric version gate, and blocks manual runs from a feature branch even though the job is limited to `main`). `versionCode = major*1_000_000 + minor*1_000 + patch + VERSION_CODE_OFFSET` (repository variable `vars.VERSION_CODE_OFFSET`, default `0` when unset, shared with `android-promote.yml` from `app-demand-updates`), as decided in `workflow-centralized-versioning`. Both go to the step summary. `run_number` was rejected for the same reasons as there.

### 3. Private release = internal testing track, `status: completed`
`r0adkll/upload-google-play@v1` with `packageName: nl.hexmaster.theprey`, `track: internal`, `status: completed`, `whatsNewDirectory` pointing at the generated notes. Internal testing is the Play track restricted to an invited testers list, available within minutes and without review — the "private release". `status: draft` was rejected: a draft never reaches testers and needs a manual click every time. A `track` input was rejected: the user wants private releases only; promotion is a Play Console action on an existing release, not a new build.

### 4. Release notes via the Copilot CLI
A step installs `@github/copilot` (pinned version) with `setup-node` and runs it non-interactively (`copilot -p … `), authenticated with `COPILOT_GITHUB_TOKEN`. The prompt includes the commit subjects and bodies and asks for JSON `{"en": "...", "nl": "..."}`: short, user-facing bullet points, no commit hashes or internal jargon, Dutch written in the app's informal tone (`je`), each ≤ 500 characters. No tools are allowed, so Copilot only transforms the given text.

- **Commit range:** head = `GITHUB_SHA`; base = `headSha` of the last successful run of this workflow on `main` (`gh run list --workflow android-release.yml --branch main --status success --limit 1`). If none exists or it is not an ancestor, base = the last 30 app commits. `git log base..head -- app/src` keeps only app commits — the shared tag line is useless as a base because backend releases tag it too.
- **Output:** `jq` validates the JSON and writes `whatsnew/whatsnew-en-US` and `whatsnew/whatsnew-nl-NL`, each cut to 500 characters at the last line break that fits; plus `release-notes.md` with an English and a Dutch section for the GitHub release.
- **Fallback:** any failure (missing token, CLI error, timeout of 3 minutes, invalid JSON, empty text) writes the commit subjects as the English notes and "Verbeteringen en bugfixes." + the same list as the Dutch notes, and emits `::warning::`. The step never fails the job.

Rejected: `github/copilot-release-notes@v1` (used by the Ionic workflow) — output is a single English GitHub release body with no control over language or length; generating per language in two calls — twice the cost and the two texts drift apart.

### 5. Release notes into the shared create-release action
`.github/actions/create-release` gains an optional `notes-file` input. When set and the file is non-empty, it calls `gh release create … --notes-file`; otherwise it keeps `--generate-notes`. When the release already exists it is left untouched (no edit), so a backend run that created the version first wins. This is additive; the backend callers do not change.

### 6. Signing and secrets
`KEYSTORE_BASE64` is decoded to `$RUNNER_TEMP/theprey-upload.jks`; `app/src/key.properties` is written with `storeFile` as that absolute path, `KEY_ALIAS`, `KEYSTORE_PASSWORD`, `KEY_PASSWORD`. Secrets reach scripts via `env:`, never interpolated into `run:`. A guard fails the job when any of them is empty, before Gradle runs. The AAB is uploaded as an artifact (3-day retention, `if-no-files-found: error`) before the Play step so a failed upload can still be inspected.

### 7. Hand-over from `workflow-centralized-versioning`
That change keeps its sections 1–3 (version.yml comment, create-release action, backend workflows). Its section 4 tasks, its "Android app release after publish" and "Increasing app version code" requirements, its design sections 4/5 and migration steps 2–3 are removed and point here. Both changes can be applied in either order as long as the create-release action exists before this workflow's first `main` run.

## Risks / Trade-offs

- [Play already holds a `versionCode` above the formula, from the Ionic era's `run_number` codes] → Read the highest code in the Play Console before the first run and set `vars.VERSION_CODE_OFFSET`; the guard step logs the code that will be used.
- [Upload key in `KEYSTORE_BASE64` is not the key Play App Signing expects] → Upload fails, nothing is released; replace the secret or reset the upload key in the Play Console.
- [Copilot CLI flags or auth change between versions] → The CLI version is pinned; the fallback notes keep releases flowing; the warning makes the breakage visible.
- [Copilot hallucinates features or writes poor Dutch] → It only sees commit messages, is told not to invent anything, and the release goes to internal testers first, where the notes can be corrected in the Play Console before promotion.
- [Same version, two commits: a backend release took `v1.4.2` on an earlier commit] → The app bundle also carries `1.4.2`; if that `versionCode` is already in Play the upload is rejected and the next commit moves the version. Accepted, as in `workflow-centralized-versioning`.
- [`COPILOT_GITHUB_TOKEN` is a personal fine-grained PAT] → It only needs the Copilot Requests permission; it expires and must be rotated, the fallback covers the gap.

## Migration Plan

1. Narrow `workflow-centralized-versioning` (decision 7) and add the `notes-file` input to its create-release action.
2. Configure secrets and Play Console (service account with release rights on the app, internal testers list); set the repository variable `vars.VERSION_CODE_OFFSET` if needed.
3. Merge the workflow through a pull request — the PR run proves `verify` only.
4. First publish via `workflow_dispatch` on `main`; confirm the internal release, both "What's new" texts and the GitHub release.
5. Rollback: delete or disable the workflow; Play releases already on the internal track can be halted in the Play Console.

## Open Questions

- Highest `versionCode` currently in the Play Console for `nl.hexmaster.theprey` — sets `vars.VERSION_CODE_OFFSET`, not the design.
