# Design

## Context

- `.github/workflows/version.yml` already exists as a reusable workflow (`workflow_call`) that runs GitVersion 6.4 against `GitVersion.yml` and exposes `semVer`, `tag`, `majorMinorPatch`, `major`/`minor`/`patch` and more. All four backend workflows call it as their `version` job.
- Each backend workflow ends with a `release` job (`needs: [version, deploy]`, `if: github.event_name == 'push'`, `contents: write`) holding the same ~20-line `gh release` script. That script is already idempotent and race-tolerant; it is just copied four times.
- There is no workflow for `app/src`. The deleted Ionic `android-release.yml` (removed in `f9df432`) showed the shape: keystore from `KEYSTORE_BASE64`, `key.properties`, `ORG_GRADLE_PROJECT_versionCode/_versionName`, `r0adkll/upload-google-play`, `PLAY_SERVICE_ACCOUNT_JSON`.
- `app/src/app/build.gradle.kts` reads `versionCode`/`versionName` from Gradle properties (fallback `1` / `0.1.0`) and only signs when `app/src/key.properties` exists.
- GitVersion is in `ContinuousDeployment` mode with an empty label on `main`, so every commit after the last `v*` tag gets the next patch version; the tag the release step creates is what moves the line forward.

## Goals / Non-Goals

**Goals:**
- One file that runs GitVersion (`version.yml`), one file that creates releases (the composite action).
- Backend and app share one version line and one release per version.

**Non-Goals:**
- Changing `GitVersion.yml` or the versioning scheme.
- Per-component version lines (e.g. `app-v1.2.0` vs `games-v1.2.0`).
- Attaching build outputs (AAB) to the GitHub release.
- Release notes beyond GitHub's `--generate-notes`.
- Website or landing-zone releases; they stay unversioned.

## Decisions

### 1. Keep `version.yml` as a reusable workflow
It already is the single GitVersion file and every caller uses it as a job. Converting it to a composite action would let callers compute the version in-job, but it would also need its own checkout with `fetch-depth: 0` in every caller and break the existing `needs.version.outputs.*` contract. No change beyond a header comment that names it as the only place GitVersion runs.

### 2. Release step as a composite action, not a reusable workflow
`.github/actions/create-release/action.yml`, used as a step: `uses: ./.github/actions/create-release`. A step fits "after the release, this step creates the version" — in the app workflow it runs inside the same job right after the Play upload, without an extra job and runner. A reusable workflow would force a separate job per caller and a second checkout.

Inputs:
- `version` (required) — `1.1.8` or `v1.1.8`; a leading `v`/`V` is stripped and `v` re-added, so the tag is always `v<semVer>`.
- `target` (optional, default `${{ github.sha }}`) — commit to tag.
- `token` (optional, default `${{ github.token }}`) — passed to `gh` as `GH_TOKEN`.

Outputs: `tag` and `created` (`true`/`false`), so callers can log or branch on it.

Behaviour: the existing script moved verbatim — `gh release view` → skip if present; `gh release create --target --title --generate-notes`; on failure, re-check and treat "created concurrently" as success; otherwise exit 1. Empty version → `::error::` and exit 1. Inputs are passed through `env:`, never interpolated into the script, to avoid script injection.

Callers need a checkout (the action is local) and `permissions: contents: write` on the job. The backend `release` job keeps its checkout; `fetch-depth: 0` is no longer needed because `gh` creates the tag server-side.

### 3. Backend workflows: swap the script, keep the job
The `release` job in `games.yml`, `notifications.yml`, `playfields.yml` and `users.yml` keeps its `needs: [version, deploy]` and `if: github.event_name == 'push'`; its script step becomes `uses: ./.github/actions/create-release` with `version: ${{ needs.version.outputs.semVer }}`. The path filters stay as they are: a change to the action or to `version.yml` alone does not trigger a deploy, same as today.

### 4. New `android-release.yml` for the Kotlin app
- **Triggers:** `push` to `main` on `app/src/**` and the workflow file; `workflow_dispatch` with a `track` choice (`internal`, `alpha`, `beta`, `production`, default `internal`). No `pull_request` deploy; a `pull_request` trigger runs only `lintDebug testDebugUnitTest`.
- **Jobs:** `version` (uses `version.yml`) → `test` (`./gradlew lintDebug testDebugUnitTest` in `app/src`) → `build-and-publish` (JDK 17 per `build.gradle.kts`, decode keystore, write `key.properties`, `./gradlew bundleRelease`, upload artifact with 3-day retention, `r0adkll/upload-google-play@v1` with `packageName: nl.hexmaster.theprey`, then the create-release action). The release step is the last step of the publish job so it only runs after a successful upload; the job gets `contents: write`.
- **Concurrency:** `android-release-${{ github.ref }}`, `cancel-in-progress: false`.

### 5. `versionCode` from the version, not `run_number`
`versionCode = major * 1_000_000 + minor * 1_000 + patch` (e.g. `1.1.8` → `1001008`), computed in a step from `needs.version.outputs.major/minor/patch`. It is deterministic per version, survives workflow renames and re-runs, and leaves room for 999 patches per minor. Alternative `run_number` resets if the workflow file is re-created and says nothing about the version; it was rejected. Rebuilding the same version yields the same code and Play rejects it, which is the right outcome — the version must move first. A `VERSION_CODE_OFFSET` env (default `0`) is added to the result so the line can be lifted above a legacy code if Play already holds a higher one.

## Risks / Trade-offs

- [Shared version line: a backend release on commit X tags `v1.1.8`; an app release triggered later on the same commit X gets `1.1.8` too and its release step no-ops] → Intended: the release already marks that commit; the app build carries the same version.
- [Two pipelines on different commits race for the same semVer (one runs before the other's tag lands)] → The second `gh release create` fails because the tag exists on another commit; the action re-checks, finds the release, and succeeds. The second build ships with the same version number as the first. Accepted; matches today's behaviour.
- [Play already holds a `versionCode` above `1001008`] → Set `VERSION_CODE_OFFSET`; verified on the first run (see Migration).
- [Missing signing/Play secrets make the first app run fail] → Fails before upload; no release is created. Secrets listed in proposal Impact.
- [`r0adkll/upload-google-play` requires the app to have had a manual first upload] → The listing already exists (same application id as the earlier app).

## Migration Plan

1. Add the composite action and switch the four backend workflows; a push touching one backend module proves the action on a real run (expect `created=true` or `false` depending on the tag state).
2. Configure the app secrets, check the highest `versionCode` in Play Console and set `VERSION_CODE_OFFSET` if needed.
3. Add `android-release.yml`; first run via `workflow_dispatch` on the `internal` track.
4. Rollback: revert the commit; the old inline script is self-contained.

## Open Questions

- What is the highest `versionCode` currently in the Play Console for `nl.hexmaster.theprey`? Determines `VERSION_CODE_OFFSET`; does not change the design.
