# Design

## Context

- **Backend already gates.** `POST /games/version-checker` (anonymous, `GameEndpoints.cs`) runs `CheckAppVersionQueryHandler`, which reads `Games:MinimumAppVersion` from `IConfiguration`: `204` up to date, `409` update required, `400` malformed; absent key = gate dormant. OTel and unit tests exist.
- **But the key is not really runtime-configurable.** `ServiceDefaults.AddAzureAppConfiguration` only calls `UseFeatureFlags(...)`; there is no `ConfigureRefresh`, so plain key-values are loaded once at startup. And `server/infrastructure/games/main.bicep` writes the key through `modules/games-app-config.bicep` with a default of `1.0.14` on every Games deploy, overwriting any manual value.
- **The Kotlin app never checks.** `BuildConfig.VERSION_NAME` is only shown on the main menu and the launch sequence. The existing `app-version-gate` client requirement was written for the Ionic app.
- **Release flow (in flight, `workflow-kotlin-app-deployment`):** every push to `main` touching `app/src` publishes to the Play **internal** track, `versionName = semVer` (plain `x.y.z`), `versionCode = major*1_000_000 + minor*1_000 + patch + VERSION_CODE_OFFSET`, tag `v<semVer>`. Production is reached by a manual promotion — the user chose a separate promote step so the minimum is never raised for a version production users cannot install.
- **No `development` branch yet;** only `main` exists. GitVersion already knows `develop|development`.
- **"Active game"** = what `GET /games/active` returns (`GameRepository.activeGame()`: `Active(gameId)`, `None` on 404, `Failed` otherwise). The server only reports `Started` and `InProgress` games there (`GameRepository.GetActiveGameForUserAsync`), not lobbies. The user chose to exempt lobbies too, so the app exempts them by route (decision 7). `GameScreen` shows the result (`ActiveGameUi.Result`) on the `GAME` route after the game completes.
- The deploy identity (`AZURE_PROD_*` OIDC) already writes the key through ARM (the Bicep module); it has no App Configuration data-plane role.

## Goals / Non-Goals

**Goals:**
- One decision per release PR, enforced by CI, acted on at production promotion.
- An open app learns about a required update within ~15 minutes, without push infrastructure.
- Nobody is pulled out of a lobby or game.

**Non-Goals:**
- Play In-App Updates API, staged rollouts, Play review automation.
- Server-side enforcement on other endpoints (an outdated app can still call the API until it re-checks).
- Gating the backend deploy on the label, or rolling back the minimum automatically.
- A soft "update available" hint for `update:optional` — the user wants nothing shown.

## Decisions

### 1. Label check: `.github/workflows/pr-update-label.yml`
`pull_request` to `main`, types `opened, reopened, synchronize, labeled, unlabeled, edited`; one job, no checkout, reads `github.event.pull_request.labels` via `toJSON` into `env:` and counts the two labels in a shell step; fails with `::error::` naming both labels unless exactly one is present. `permissions: {}`. Made a required status check on `main` (manual repo setting). Applies to every PR into `main`, not only from `development` — a hotfix PR straight into `main` must declare its policy too, and a backend-only PR can legitimately require an app update (breaking API change). The labels are created once with `gh label create` (task), not by the workflow.

Rejected: auto-adding `update:optional` as default — a forgotten decision would silently become "optional", which is the dangerous direction.

### 2. Policy is computed at promotion, from the PRs in the range
Nothing is stored at internal-publish time: `android-release.yml` stays unaware of labels. `android-promote.yml` derives the policy from GitHub itself:
- **Range:** base = the commit of the highest `app-production-v*` tag (created by this workflow), head = the commit of tag `v<version>`. `git log --first-parent --format=%H base..head` on `main`.
- **PRs:** for each commit, `gh api repos/{repo}/commits/{sha}/pulls` → merged PRs whose base is `main`; collect their labels. No base tag (first run) → only the PR(s) of the head commit.
- **Policy:** required if any PR has `update:required`; unlabelled PRs count as optional. Summary table (PR, title, label) to `$GITHUB_STEP_SUMMARY`.

Rejected: recording the label on the GitHub release at internal publish — the shared version line means a backend workflow may have created `v<semVer>` first, and an internal release that is never promoted would need its policy carried forward anyway. Rejected: using only the promoted version's PR — would drop a `required` from an intermediate internal release (spec scenario "Required change in an earlier internal release").

**Minimum = the promoted version**, not the version of the required PR: production users all sit at or below the previous promotion, so both values force the same users to update, and testers get the promoted build anyway.

### 3. `android-promote.yml` — three jobs
`workflow_dispatch` with required input `version` (`x.y.z`). Concurrency `android-promote`, no cancel. `permissions: contents: read`.

1. **`plan`** — checkout (full history, tags); validate `version` format; require tag `v<version>`; require `version` > highest `app-production-v*` (numeric sort via `sort -V`); compute `versionCode` with the same formula and `vars.VERSION_CODE_OFFSET`; compute the policy (decision 2). Outputs `versionCode`, `required`.
2. **`promote`** — `needs: plan`, `permissions: contents: write`. Promotes with `fastlane supply` (`--track internal --track_promote_to production --version_code <code> --release_status completed --skip_upload_*`), service account from `PLAY_SERVICE_ACCOUNT_JSON` via a temp file, Ruby from `ruby/setup-ruby` with a pinned `fastlane` gem. Fastlane fails when the code is not on the internal track — that is the "not on internal track" guard. Then pushes tag `app-production-v<version>` at the `v<version>` commit.
3. **`enforce`** — `needs: [plan, promote]`, `if: needs.plan.outputs.required == 'true'`, `environment: play-production` (required reviewers). Azure OIDC login (`AZURE_PROD_*`), then `az deployment group create` of `server/infrastructure/games/modules/games-app-config.bicep` into `rg-theprey-landing-prod` with `appConfigName` and `minimumAppVersion=<version>`. Summary states the new minimum.

The environment approval is the "release is live" gate: a production release can wait in Play review for hours, and blocking users before the store serves the new build would strand them. The reviewer approves once the Play Console shows the release as available. Monotonicity follows from step 1 (promoted versions only go up) — no read of the current value needed, so the identity keeps needing ARM rights only.

Rejected: Play Developer API calls hand-rolled with `curl` + JWT (more code to maintain than a pinned fastlane); `az appconfig kv set` (needs a new data-plane role for the deploy identity); polling Play for review status (no reliable API signal).

### 4. Shared `VERSION_CODE_OFFSET`
Both workflows read `vars.VERSION_CODE_OFFSET` (repository variable, default `0` when unset). `workflow-kotlin-app-deployment` currently plans a workflow-level `env` — this change edits that change's design/tasks (or the workflow, if already applied) to use the variable, so the promote job can never compute a different code than the publish job did.

### 5. Infrastructure owns the module, not the value
Remove the `minimumAppVersion` param and the `gamesAppConfig` module call from `games/main.bicep` (and its comment). Keep `modules/games-app-config.bicep` (drop its `1.0.14` default; the param becomes required) — it is the promote workflow's write path and keeps the `existing` resource pattern. Removing the module from an incremental deployment does not delete the key, so the current `1.0.14` stays.

### 6. Backend refresh of key-values
In `ServiceDefaults.AddAzureAppConfiguration`, add `.ConfigureRefresh(r => r.RegisterAll().SetRefreshInterval(TimeSpan.FromMinutes(5)))`. `RegisterAll` watches every selected key (no sentinel key to maintain); the existing `UseAppConfigurationRefresh` middleware already triggers refreshes on requests, and the version-checker is itself a request. 5 minutes: the cost is one conditional page request per interval per replica. `CheckAppVersionQueryHandler` already reads `IConfiguration` per call, so no handler change. Feature flags keep their 15-minute interval.

Rejected: a sentinel key — requires every writer (incl. the promote workflow) to bump it.

### 7. App: `AppVersionGate` (data) + `UpdateGate` (UI)
- **`data/version/VersionApi`** (Retrofit, `POST games/version-checker`, body `{"current-version": …}`, `Response<Unit>`) on the existing Retrofit instance — the endpoint is anonymous, the auth interceptor adding a token when there is one is harmless.
- **`data/version/AppVersionGate`** in `AppContainer`, scoped to `appScope`, exposes `StateFlow<GateState>`: `Open`, `Deferred`, `Blocked`. `suspend fun check()`:
  - `204`/other/error → `Open` (fail open; also clears a previous `Blocked`).
  - `409` → if session is not `SignedIn` → `Blocked`; else `gameRepository.activeGame()`: `Active` → `Deferred`; `None` → `Blocked`; `Failed` → `Deferred` (spec: never block on a blip).
  - Pure decision function `decide(versionResult, sessionSignedIn, activeGame)` for unit tests; a `Mutex` drops overlapping checks.
- **Triggers:** in `MainActivity`'s content, `LaunchedEffect` + `repeatOnLifecycle(STARTED)` loops `check()` then `delay(10.minutes)` — covers cold start, every foreground return and the periodic tick, without `lifecycle-process`. Additionally, while `Deferred`, a navigation away from `LOBBY`/`GAME` triggers `check()` so the block follows the game's end promptly.
- **`ui/update/UpdateRequiredScreen`**: full-screen tactical panel (`PreyMark`, title, body, primary **Update** button) per the design system, colors from `ThePreyTheme`. Wrapped around the nav host (`ui/update/UpdateGate`): when `Blocked` and the current route is neither `LOBBY` nor `GAME`, it is drawn on top and consumes input (the screens underneath are hidden from accessibility services too); a player in a lobby gets `Blocked` from the server (no started game) but is exempted by the `LOBBY` route, stays exempt on `GAME` once it starts, and is blocked when leaving either; `BackHandler` finishes the activity. On `GAME` the result stays visible; leaving it (any navigation) shows the block.
- **Store link:** `BuildConfig.PLAY_STORE_URL` (`buildConfigField`, `https://play.google.com/store/apps/details?id=` + `applicationId`). Open `market://details?id=<applicationId>` first, fall back to the https URL on `ActivityNotFoundException`.
- **Debug builds** (`BuildConfig.DEBUG`) run the check but never move past `Open` — their `versionName` falls back to `0.1.0` and would always be blocked against the production backend.
- **Strings:** `update_required_title`, `update_required_body`, `update_required_action` in `values/` and `values-nl/` (informal `je`), per the multilingual skill.

Rejected: Play In-App Updates `IMMEDIATE` flow — new dependency, depends on Play's own update availability rather than our minimum, and does not work on sideloaded/internal builds the same way. Rejected: a realtime push of "update required" — players outside a game have no socket; polling every 10 minutes is enough for this signal.

## Risks / Trade-offs

- [Reviewer approves `enforce` before the store actually serves the new version] → Users see the block but the store shows "Open" on the old version. The approval prompt states "approve only when the Play Console shows the production release as Available"; rollback = set the key back via the same Bicep module (or portal), picked up within 5 minutes.
- [A lobby is kept open indefinitely to avoid updating] → Lobbies are cleaned up by the existing game cleanup service; the exemption ends with the game.
- [Outdated app keeps calling the API for up to 10 minutes + 5 minutes refresh] → Accepted; the label is about forcing updates, not hard API cut-off.
- [`RegisterAll` refresh changes behaviour for every API: any App Config key edit now applies live] → Desired, but options bound once at startup (e.g. `IOptions<T>` without `IOptionsMonitor`) still won't change; noted in the ServiceDefaults comment.
- [PR labels edited after merge] → The promote workflow reads labels at promotion time, so a late fix (e.g. relabel to `required`) is honoured. Documented as a feature.
- [Commits pushed to `main` without a PR] → No labels found → optional. Branch protection on `main` (required PR + label check) prevents this.
- [fastlane `supply` behaviour changes] → Gem version pinned.
- [App versions released before the gate never check] → They cannot be forced; the first required promotion only reaches users who already run a gated version. Ship the gate (migration step 3) well before the first `update:required`.

## Migration Plan

1. Create labels `update:optional` / `update:required`, the `development` branch, environment `play-production` with reviewers, repository variable `VERSION_CODE_OFFSET`.
2. Ship backend refresh (decision 6) and the Bicep change (decision 5) together via `games.yml`; confirm the key still holds `1.0.14` after the deploy.
3. Ship the app gate in a release; with the minimum at `1.0.14` nobody is blocked.
4. Merge `pr-update-label.yml` and mark it required on `main`; then `android-promote.yml`.
5. First promotion with an `update:optional` PR to prove promote without enforce; later a required one end-to-end.
6. Rollback: lower or clear `Games:MinimumAppVersion` (gate opens within ~15 minutes); disable the workflows; the app gate is dormant without a raised minimum.

## Open Questions

- Who are the required reviewers of the `play-production` environment — repo setting, not design.
