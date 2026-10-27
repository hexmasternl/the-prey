# Autonomous OpenSpec run — 2026-10-26

Baseline before any change: `dotnet build server/src/the-prey.slnx` green, all server test projects green (Games 363, PlayFields 81, Users 72, Notifications 6, IntegrationEvents 3, Core 1), `hugo --minify` green, `./gradlew assembleDebug lintDebug testDebugUnitTest` green (after restarting the Gradle daemon with `JAVA_HOME` = JDK 21: the running daemon used a VS Code JRE without `jlink`, an environment issue, not a repo issue). `server/src/Email/HexMaster.ThePrey.Email.Tests` is an empty folder without a project.

Dirty tree at start (not mine, left alone except as noted): `auto/` (instructions + `code.cmd`), and the untracked changes `openspec/changes/app-demand-updates/`, `workflow-centralized-versioning/`, `workflow-kotlin-app-deployment/`.

## workflow-centralized-versioning — partial
- Tasks: 7/14; needs human: 3.3; handed over (removed by `workflow-kotlin-app-deployment` task 1.2): 4.1–4.5, 6.1
- Decisions: done first because `workflow-kotlin-app-deployment` needs its create-release action. Section 4 and 6.1 were not implemented here: `workflow-kotlin-app-deployment` (design decision 7) takes them over and its task 1.2 deletes them from this change. The change folder was untracked; it is committed with this change since its `tasks.md` records the progress. The action script was verified locally against `v1.1.7` (skip, `created=false`, also with `V1.1.7`) and an empty version (exit 1); no new release was created.
- Blockers: none
- Commits: 7b847be ci: shared create-release action for all backend workflows

## workflow-kotlin-app-deployment — partial
- Tasks: 14/16; needs human: 8.1, 8.2
- Decisions: Copilot CLI pinned to `@github/copilot@1.0.98` (the version installed here, two weeks old); tools are switched off with `--available-tools=` and `--silent` gives only the answer. Code fences in Copilot's answer are stripped before `jq` validation. English fallback = commit subjects, Dutch fallback = "Verbeteringen en bugfixes." + the subjects (per design). Whatsnew files are written with `jq -j` (no trailing newline) so a 500-character text stays at 500. `gh run list` is allowed to fail (`|| true`) so a lookup error falls back to the last 30 app commits instead of failing the job. Local verification installed `jq` via winget (not present on this machine). Note: `gh run list` already finds an old successful Ionic-era `android-release.yml` run (bfeb13a), so the first notes cover the 78 app commits since then.
- Blockers: none
- Commits: 4, 30b614e..2d9b89f (30b614e, a0deea0, 196fafb, 2d9b89f; the user's own commit 604f6f1 landed in between and also contains an in-progress copy of `android-release.yml`)

### Branch note (mid-run)
- The `the-prey-git-workflow` skill appeared during the run: work must not land on `main`. The first 7 commits (7b847be..03bba2c, plus the user's 604f6f1) are on the local `main`, unpushed. History was not rewritten (the instructions forbid it): the run continues on `run/2026-10-26-openspec` in `../the-prey-worktrees/run-2026-10-26-openspec`, branched from that HEAD. A local `development` branch was created at `origin/main` (no upstream, not pushed). Nothing was pushed and no PR was opened (the instructions forbid both).

## app-demand-updates — partial
- Tasks: 13/21; needs human: 3.3 (preview render), 3.5 (device check), 4.1 (create GitHub labels), 4.2 (draft-PR check; workflow done), 5.4 (what-if against prod; job done), 6.1, 6.2, 6.3 (GitHub settings, live deploy, real promotions)
- Decisions: `GET /games/active` only returns `Started`/`InProgress` games (server `GameRepository.cs`), not lobbies, so the design's lobby exemption could not come from that call; the app exempts the `LOBBY` route as well as `GAME` (design.md updated). At app start the gate waits for the restored session before deciding, so a signed-in player is never blocked as "signed out". The version check loop runs from `MainActivity` (`lifecycleScope` + `repeatOnLifecycle(STARTED)`), the overlay lives in `ui/update/UpdateGate`, which also hides the screens underneath from accessibility services. fastlane pinned at 2.240.1 (option names checked against its source); `--track_promote_release_status completed` added. The promote workflow tags `app-production-v<version>`. Docs synced: `docs/deployment/azure-deployment.md` and a FAQ entry (en + nl).
- Blockers: none
- Commits: 17, 03bba2c..571847b

### Branch note (later)
- During the run the repo root was switched to `development` and the local `main` was reset to a single skills commit (08506c0), so the run's early commits no longer sit on `main`. All work of this run is on `run/2026-10-26-openspec`.

## static-game-info-website — partial
- Tasks: 55/60; needs human: 8.2, 8.3, 8.5 (visual reviews), 9.4, 9.5 (GitHub Pages; the site now deploys to an Azure Static Web App, so these look obsolete)
- Decisions: 8.1 checked by script over the built site (36 pages, every switcher link points at the same page in the other language). 8.4 checked in `assets/css/main.css` (headings on `--font-display`, body on `--font-body`, no other font-family, no h5/h6 in the output). 8.6: no page is untranslated (`become-a-tester` and `_index` are front-matter-driven and translated); the banner was confirmed by building with `translationPending: true` on `nl/rules` temporarily (file restored, not committed).
- Blockers: none
- Commits: 1, 9a27913

## The other 26 open changes — partial (human checks only)
- add-started-game-state, app-game-active-view, app-game-create-new, app-game-first-time-experience, app-game-hud-hunter, app-game-hud-prey, app-game-lobby-view, app-game-result-view, app-game-select-playfield, app-game-share-friends, app-landing-page, app-lobby-overview, app-location-foreground-service-warning, app-location-service, app-main-menu, app-main-menu-landscape, app-playfield-details-page, app-playfield-edit-area-allow-delete, app-playfields-create-new, app-playfields-list, app-play-now-button-behavior, app-settings-screen, app-splash-screen, deploy-github-actions-bicep, game-state-realtime-communication-protocol, website-update-to-kotlin-app
- Tasks: every open task needs a device or emulator, an Android Studio preview render, a running backend with several clients, a live Azure/GitHub run, the Play Console upload key (app-game-share-friends 1.3) or emulator screenshots (website-update-to-kotlin-app 3.2). Each line now carries `— **auto: needs human:** <reason>`. The code they cover builds and passes `assembleDebug lintDebug testDebugUnitTest`, the server build and tests, and `hugo --minify` on this branch.
- Decisions: none beyond the annotation; nothing archived (no change has every task checked).
- Blockers: none
- Commits: 26, d7f612c..60fc693

# Run 2026-10-26 (2) — branch `run/2026-10-26-openspec-2`, worktree `../the-prey-worktrees/run-2026-10-26-openspec-2`

Start: repo root on `development` (clean, = `origin/development` efb9a95). Three open changes, all at 0 tasks; order by age (proposal commit): app-setting-always-on, app-game-preys-and-hunter-hidden, server-telemetry-with-application-insights. Nothing pushed, no PR (the instructions forbid both; the git-workflow skill's push/PR steps were skipped).

Environment: the worktree has no `local.properties`; Gradle runs with `ANDROID_HOME=D:\Android\SDK`. A `local.properties` in the worktree makes `lintDebug` fail with `PropertyEscape` whatever the escaping, so it was removed again (not a repo issue).

## app-setting-always-on — partial
- Tasks: 9/12; needs human: 2.2 (preview render + TalkBack), 2.4 (persistence across restart on a device), 3.3 (30 s screen-timeout check on a device)
- Decisions: the store goes into `ThePreyNavHost`/`MainActivity` already with 2.1, so the new `SettingsViewModel` parameter keeps the build green; 2.4 is left with only its device check. The switch follows the stored value (no optimistic update), so a failed write leaves it as it was (design Decision 3). `KeepScreenOn` and `holdsScreen` live in `ui/game/ScreenHold.kt`. The app has one `GameScreen` destination (`Routes.GAME`, hosting both HUDs); "both game destinations" in 3.3 is read as that one. Website: a Display step in Get started and a FAQ entry (en + nl); the settings screenshot caption is unchanged because the screenshot does not show the Display section yet (retake with the next screenshot round). 4.2: no Play Store change — manifest, dependencies and network calls untouched (`WAKE_LOCK` predates this change, used by tracking); put this in the PR body. `docs/` has no Settings description, so nothing to sync there.
- Blockers: none
- Commits: 12, ec10680..3aa8d2e

## app-game-preys-and-hunter-hidden — partial
- Tasks: 7/12; needs human: 1.1 (diagnosis in the test environment), 4.1–4.4 (multi-device checks)
- Decisions: the helper is `rememberStartTracking(starter)` in `ui/tracking/TrackingAutoStart.kt` (consent flow, silent toast and resume-notification cancel unchanged); the decision function is `requestsTracking(ActiveGameUi)` in `ui/tracking/GameTrackingStart.kt`: `Ready`, status `Started`/`InProgress`, the player is a participant and not `Tagged`/`Out` (a `Passive` player still asks, so their reports resume). The game route collects the game view model's `uiState` a second time for the effect; the `StateFlow` is shared, so this adds no work. `app-game-active-view` was archived before this run, so 2.5 edited `openspec/changes/archive/2026-10-26-app-game-active-view/design.md`. 3.1 keeps the existing `minReplicas: 1` and adds `maxReplicas: 1`; `az bicep build` passes (the BCP081 warning in `service-access.bicep` predates this change).
- Blockers: none
- Commits: 8, 06de3c5..051cf6a

## server-telemetry-with-application-insights — partial
- Tasks: 8/10; needs human: 2.5 (local AppHost run with signed-in calls and a dashboard check), 3.1 (Application Insights after a deploy from `main`)
- Decisions: 1.1 and 1.2 are one commit, because the new required `@secure()` parameter breaks every caller until all four pass it (the design's risk note asks for that). The parameter sits with the other params at the top of `container-app.bicep`. `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6.0 (latest stable; needs OpenTelemetry ≥ 1.15, the repo has 1.16). Sampling: `TracesPerSecond = null`, `SamplingRatio = 1.0F` (the distro defaults to rate-limited sampling). `service.version` = entry assembly informational version, `service.instance.id` = `Environment.MachineName`; `service.name` is only set in code when `OTEL_SERVICE_NAME` is absent. The `*.Observability` usings in the four `Program.cs` were removed with the OTel blocks (nothing else used them); the `*ObservabilityConstants` stay. 2.3 was checked by starting the Notifications API with no connection string and with a dummy one: both reached "Application started". 2.6 also updated the `the-prey-new-backend-module` skill (it scaffolded per-module `AddOpenTelemetry()`). Build warnings unchanged (6, all pre-existing: xUnit1051, NU1902 MessagePack); all server tests pass (527).
- Blockers: none
- Commits: 7, 393df9f..c4ba153

# Run 2026-10-26 (3) — branch `run/2026-10-26-openspec-3`, worktree `../the-prey-worktrees/run-2026-10-26-openspec-3`

Start: repo root on `development` (= `origin/development` 1045148), untracked `designs/store/` left alone (not this run's). Five open changes. `server-telemetry-with-application-insights`, `app-setting-always-on` and `app-game-preys-and-hunter-hidden` only have tasks marked "needs human" from run (2): skipped, unchanged. Order of the rest by age: app-game-initial-game-state-not-updating (9beb885), then server-game-play-penalties (1045148); neither names the other as a prerequisite.

## app-game-initial-game-state-not-updating — partial
- Tasks: 8/9; needs human: 5.2 (two devices/emulators against a running backend)
- Decisions: 1.1 and 1.2 are one commit: the reducer change breaks the existing `configuration-changed` test (it changed `Lobby → Ready`), which became `reduce_ShouldApplyConfiguration_WhenStatusIsUnchanged`; an extra `Lobby → Ready` resync case was added. The status comparison is case-insensitive as the design says. 3.1 needed no `GameViewModel` change: it already reseeds on the promotion snapshot's new `serverTime`. 4.2: no website page mentions the resync cadence (`grep -rn resync website/src/content` is empty).
- Blockers: none
- Commits: 7, 11b54a41..cb10dcc2

## server-game-play-penalties — partial
- Tasks: 13/14; needs human: 5.1 (local stack + app on a device + 5-minute wait)
- Decisions: 1.1 and 1.2 are one commit, since removing the gate fails existing sweep tests. Four head-start tests record readings at (52.1, 5.1), outside the test square, so they now run without a playfield boundary (`WithoutPlayfieldBoundary()`, a < 3-point polygon), which keeps them about head-start only. `ProcessAsync_ShouldNotPenalize_WhenPreyIsTagged` now asserts no penalty for the prey and 1 penalty in total (the hunter beside it is outside too). The `GameConfigurationDto` "always true" remark was committed with 2.1, where it becomes true. `GameMappings` is internal and the test project has no `InternalsVisibleTo`, so the 2.1/2.2 mapping tests go through `GetFullGameStateQueryHandler` (`/gamestate`); participant mapping reads the wall clock, so those penalties are relative to `UtcNow`. `participant-changed` and `/gamestate` both map through `GameParticipant.ToDto(now)` (checked by reading). 3.2 needed no code change (reducer and snapshot adoption already keep `penaltyEndsAt`); two reducer tests document it. 4.2: besides the rules page, the FAQ, playfield and hunter-role pages (EN + NL) carried the same "penalties on" clause and the 10 s cadence; all now say "about every 30 seconds" / "ongeveer elke 30 seconden" (the website never uses the word "sweep"). The game-rules skill's drift note about the website's 10 s wording was removed with it. Release note: label the PR into `main` `update:optional`; Games API deploy needed, no migration, no Play Store declaration change.
- Blockers: none
- Commits: 13, 03e822e5..b6ca8c12

# Run 2026-10-27 — branch `run/2026-10-27-hud-location-nearme`, worktree `../the-prey-worktrees/run-2026-10-27-hud-location-nearme`

Start: repo root on `development` (= `origin/development` b9475938); dirty `.claude/skills/the-prey-openspec-github-sync/SKILL.md` and `auto/instructions.md` in the repo root are not this run's and were left alone. Project "The Prey" (#3) has issues #46, #47 and #53 in status Ready → eligible: `app-game-prey-hud-not-updated-correctly` (#46), `app-location-all-the-time-step` (#47), `server-playfields-near-me` (#53). Out of scope: `server-games-radar-ping` (#50), `server-games-decoy` (#51), `server-users-game-outcome-stats` (#49), `app-game-haptic-location-feedback` (#48), `app-wear-os-companion` (#54) (not Ready) and `app-game-preys-and-hunter-hidden` (no issue line). None of the eligible changes has tasks done, so order is by age: #46, #47, #53.

## app-game-prey-hud-not-updated-correctly — partial
- Tasks: 7/8; needs human: 4.2 (live game as prey on a device)
- Decisions: the own-location check is a small public `GameStateReducer.locationsInclude(envelope, userId)` helper (design decision 1's second option) so the reducer's `Reduction` type stays as is. The bar's full length is the restart interval only after an own broadcast or while penalised; a plain snapshot keeps `currentPingInterval` (0 still falls back to 30 s in `broadcastProgress`), as design decision 3 says. 1.2: the realtime skill does not list `GameSession.Live` fields, so no skill change. 3.3: website EN + NL already match.
- Blockers: none
- Commits: 7, eab1a125..d295b23c
