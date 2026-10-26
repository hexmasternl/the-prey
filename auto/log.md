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
