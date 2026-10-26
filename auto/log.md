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
