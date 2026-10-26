# Proposal

## Why

When a release breaks compatibility with older app versions (an API contract change, a realtime protocol change, a rules change), there is no way to get players off the old build: the Games API already has a `POST /games/version-checker` endpoint and a `Games:MinimumAppVersion` key, but the Kotlin app never calls it, nobody sets the key per release, and every Games infrastructure deploy resets it to the hard-coded `1.0.14`. The decision "does this release force an update?" belongs on the pull request that ships it, so it is made once, by the person merging, and acted on by CI.

## What Changes

- Two repository labels, `update:optional` and `update:required`. A check on every pull request into `main` (the `development` → `main` release PR) fails until exactly one of them is set.
- A new manual workflow, `.github/workflows/android-promote.yml`, that promotes a version already on the Play internal testing track to production. It reads the update labels of every PR merged into `main` since the last production promotion; if any carries `update:required`, it raises `Games:MinimumAppVersion` to the promoted version — after a manual approval that the production release is live in the store — and never lowers it. The internal-track publish (`workflow-kotlin-app-deployment`) does not touch the minimum, so production users are never blocked from a version they cannot install.
- **BREAKING (infra):** `Games:MinimumAppVersion` is no longer written by `server/infrastructure/games/main.bicep`; the promote workflow owns the key. The `games-app-config.bicep` module is reused by that workflow.
- The Games API (all APIs via `ServiceDefaults`) refreshes App Configuration key-values at runtime, not only feature flags, so a raised minimum takes effect within minutes without a restart.
- The Android app gets an app-wide update gate: it checks its version on start, on every return to the foreground and every 10 minutes while open. When the server answers "update required", a full-screen, non-dismissable message with an **Update** button to the Play Store replaces the app — unless the player is in a game that has not ended (lobby or running), in which case they play on and the message appears once that game is over. The gate fails open on network or server errors. English and Dutch texts.
- The old `app-version-gate` client requirement (Ionic-era: disabled buttons on the home and join screens) is replaced by the app-wide gate.

## Capabilities

### New Capabilities
- `app-update-policy`: how a release PR declares whether its app version is a required update, and how promoting an app version to Play production turns that declaration into the minimum supported app version.

### Modified Capabilities
- `app-version-gate`: the minimum version is set by the promotion workflow and refreshed at runtime; the client gate becomes app-wide, re-checks while the app is open, exempts players in an unfinished game and blocks everyone else with a Play Store redirect (replaces the home/join button gating).

## Impact

- **Workflows:** new `.github/workflows/pr-update-label.yml` and `.github/workflows/android-promote.yml`; `android-release.yml` (from `workflow-kotlin-app-deployment`, not yet implemented) reads `VERSION_CODE_OFFSET` from a repository variable so both workflows compute the same `versionCode`.
- **GitHub settings:** labels `update:optional` / `update:required`; the label check as a required status check on `main`; environment `play-production` with required reviewers; repository variable `VERSION_CODE_OFFSET`; a `development` branch (does not exist yet).
- **Infrastructure:** `server/infrastructure/games/main.bicep` drops the `minimumAppVersion` param and the `gamesAppConfig` module call; the existing key value stays in the store.
- **Backend:** `ThePrey.Aspire.ServiceDefaults` App Configuration refresh registration. No endpoint or contract change.
- **Android app:** new version-gate service and full-screen update UI wired around the nav host; strings in `values/` and `values-nl/`. No new library.
- **Depends on:** `workflow-kotlin-app-deployment` (internal-track publish, `v<semVer>` tags, Play service account secret).
