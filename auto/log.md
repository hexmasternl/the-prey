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
