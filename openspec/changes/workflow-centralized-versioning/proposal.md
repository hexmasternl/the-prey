# Proposal

## Why

The four backend workflows (`games.yml`, `notifications.yml`, `playfields.yml`, `users.yml`) each carry an identical, copy-pasted "Tag & Release" script, so any fix to the release logic has to be made four times and the copies drift. The native Kotlin app has no release workflow at all since the Ionic/MAUI clients were removed, so app builds never reach Google Play from CI and never produce a GitHub release. Now that the Kotlin app is the only client, both the backend and the app need to cut releases from one shared version line, through one shared piece of release logic.

## What Changes

- Keep `.github/workflows/version.yml` as the **single** place where GitVersion runs and derives the version from the repo state (`GitVersion.yml`). Every workflow that needs a version calls it; no workflow runs GitVersion itself.
- Add one reusable composite action, `.github/actions/create-release/action.yml`, that takes a version number as input, checks whether a GitHub release for that version already exists, and creates the tag + release only when it does not. It is idempotent and safe under concurrent runs.
- Replace the inline "Create tag and GitHub release" script in `games.yml`, `notifications.yml`, `playfields.yml` and `users.yml` with a call to the new action, after the deploy succeeds.
- The Android app release workflow (`.github/workflows/android-release.yml`) is owned by `workflow-kotlin-app-deployment`, not by this change.

## Capabilities

### New Capabilities
- `ci-release-versioning`: how CI derives a version from the repo (one GitVersion definition) and how backend and app pipelines turn a successful deployment/publish into an idempotent GitHub tag + release.

### Modified Capabilities
<!-- None: no existing spec covers CI/release behaviour. -->

## Impact

- **Files:** `.github/workflows/{games,notifications,playfields,users}.yml` (release job simplified), `.github/workflows/version.yml` (unchanged contract, documentation only), new `.github/actions/create-release/action.yml`.
- **Version line:** backend services and the app share one version line (`v<semVer>`). Whichever pipeline finishes first on a commit creates the release; the others no-op.
- No runtime code, API or infrastructure changes.
