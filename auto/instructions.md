# Autonomous OpenSpec implementation

You are running unattended. Nobody will answer questions or approve plans. Implement every **eligible** open OpenSpec change in this repository (see "Eligibility"), one change at a time, until each one is done or genuinely blocked. Then stop and report.

## Eligibility

Implement a change **only** when both hold:

1. Its `proposal.md` references a GitHub issue: the `> GitHub issue: [#<n>](https://github.com/hexmasternl/the-prey/issues/<n>)` line under the title (see the `the-prey-openspec-github-sync` skill).
2. That issue has the status **Ready** on the GitHub project **"The Prey"** (owner `hexmasternl`, project number 3). Check it with:

   ```
   gh project item-list 3 --owner hexmasternl --format json --limit 1000 \
     | jq -r '.items[] | select(.status == "Ready" and .content.type == "Issue" and .content.repository == "hexmasternl/the-prey") | .content.number'
   ```

   If `gh project list --owner hexmasternl` no longer shows "The Prey" as number 3, use the number it does show for that title.

Every other change is out of scope for this run: no issue line, an issue that isn't on the project, or any other status (`Backlog`, `In progress`, `In review`, `Done`). Don't implement, annotate, archive or otherwise touch it, and don't link it to an issue or change any project status yourself. If the `gh project` lookup fails, implement nothing: report the error and stop.

## Ground rules

- `CLAUDE.md` and the project skills (`the-prey-design-system`, `the-prey-multilingual`, `openspec-apply-change`) are binding, as is `docs/api/realtime.md` for anything realtime. Read `CLAUDE.md` first.
- Server work: consult the `hexmaster-coding-guidelines` MCP documents named in `CLAUDE.md` before writing code. CQRS without MediatR, Minimal APIs, OpenTelemetry in every handler, xUnit + Moq + Bogus.
- App and website text: every string lands in English **and** Dutch in the same change.
- Never ask the user anything (no `AskUserQuestion`, no plan mode). Make the reasonable decision the artifacts point to, and record it (see "Decisions").
- Follow the `the-prey-git-workflow` skill for branches: one `run/<yyyy-MM-dd>-<slug>` branch in its own worktree under `../the-prey-worktrees/`, branched from an up-to-date `development`. The only push allowed is that run branch, and the only PR allowed is one PR from it into `development` (see "Finish").
- Never push or commit to `main` or `development`, open a PR into `main`, merge any PR, deploy, publish to Play, create GitHub releases, touch Azure/Auth0/Play Console, or change secrets. Never commit `key.properties`, keystores, `.env` files or credentials.
- Never rewrite history (`reset --hard`, `rebase`, `push --force`, `checkout --` on others' work). Never use `--no-verify`.
- Stay inside the change's scope. No drive-by refactors.

## Start

1. `git status`. If the tree is dirty, commit nothing that isn't yours: note the dirty paths and leave them alone.
2. `openspec list --json` → every active change with remaining tasks (skip `archive/`).
3. Keep only the eligible changes (see "Eligibility"). If none is eligible, log that in `auto/log.md`, commit nothing else, open no PR, and stop.
4. Order: changes with the most tasks already done first (they are nearly finished), then by age, oldest first. If a change's proposal or design names another open change as a prerequisite, do that one first when it is eligible too; when it isn't, mark the dependent change blocked and name the prerequisite.

## Per change

1. `openspec status --change "<name>" --json`. If artifacts are missing (`blocked`), skip the change and note it. Do not invent missing artifacts.
2. `openspec instructions apply --change "<name>" --json`; read every file in `contextFiles` (proposal, design, specs, tasks) and the code they reference before editing.
3. For each unchecked task, in order:
   - Implement it minimally, matching the surrounding code's style, naming and comment density.
   - Run the verification the task names. When none is named, use the module's build and tests:
     - Server: `dotnet build server/src/the-prey.slnx` and `dotnet test` on the affected `*.Tests` project.
     - App (from `app/src`): `./gradlew assembleDebug lintDebug testDebugUnitTest`.
     - Website (from `website/src`): `hugo --minify` builds without errors.
     - Workflows: `actionlint` if available, otherwise check the YAML parses.
   - Only when verification passes: mark it `- [x]` in `tasks.md`.
   - If it fails, fix the cause (up to three real attempts). Never weaken, skip or delete tests to get green.
   - Commit right away (see "Commits"). Never start the next task with uncommitted work from the previous one.
4. Tasks you cannot do here — a real device or emulator check, a manual visual review, Play Console, Azure, secrets, a live deployment, anything needing a human — stay `- [ ]`. Append ` — **auto: needs human:** <one-line reason>` to that line and move on.
5. A task that reveals a design flaw or contradicts the specs: implement the smallest consistent fix, update `design.md`/specs to match, and record it under "Decisions". If the fix would change the change's intent, stop that change, leave its tasks unchecked and note why.
6. When the change's tasks are done (apart from "needs human" ones), run the full verification for every area it touched once more. Commit any fix that needed.
7. Archive (`openspec archive "<name>" --yes`) only when **every** task is `- [x]` and `openspec validate "<name>" --strict` passes. Commit the archive separately (`chore(openspec): archive <name>`).
8. If the build or tests are broken by something outside this change, do not fix it as part of this change: note it, and move on to the next change.

## Commits

Commit very often: many small, logical commits, never one big commit per change.

- **One logical step per commit.** At least one commit per task; split a task into several commits when it has separable steps (e.g. a domain type + its tests, then the handler, then the endpoint; strings in en + nl, then the screen that uses them; a refactor that prepares a feature, then the feature).
- **Every commit builds and passes the tests of the area it touches.** Don't commit a broken state just to commit often; make the step smaller instead.
- **Code, its tests and its docs go together** in the commit that introduces the behaviour. Both language files go in the same commit as the text they add.
- The `tasks.md` checkbox goes in the commit that completes that task.
- **Message:** Conventional Commits like the existing history, scope = area: `feat(app): …`, `fix(server): …`, `test(games): …`, `refactor(app): …`, `ci: …`, `docs(website): …`. Subject in the imperative, ≤ 72 chars, saying what changed. Body: the change name and task id (`openspec: <change-name> 2.3`) and, when not obvious, why.
- End every message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Stage only the files of that step (`git add <paths>`), never `git add -A` or `git add .`. Check `git diff --cached --stat` before committing.

## Decisions

Keep `auto/log.md` (create it if missing; append, never overwrite). Per change:

```
## <change-name> — <done | partial | blocked | skipped>
- Tasks: <done>/<total>; needs human: <ids>
- Decisions: <choice made where the artifacts were silent, and why>
- Blockers: <what stopped progress, with the failing command and its key error line>
- Commits: <count>, <first short sha>..<last short sha>
```

Update `auto/log.md` when a change is finished and commit it on its own (`chore(auto): log <change-name>`).

## Finish

Stop when every change is done, partial (only "needs human" tasks left) or blocked. Then:

1. Make sure the worktree is clean and every commit is on the run branch.
2. `git push -u origin run/<identifier>` (never `--force`).
3. `gh pr create --base development --head run/<identifier> --title "feat: autonomous run <identifier>"` with a body that has one section per change: summary, tasks done, tasks left for a human (and why), how it was verified, decisions worth reviewing, and any Play Store or infrastructure note. List skipped or blocked changes with the reason, and the eligible issue numbers the run started from. End the body with the attribution line from the session instructions.
4. If the push or PR fails, report the error and stop; don't retry with other flags.

End with a short summary: the PR URL, changes archived, changes left with human tasks (and which), changes blocked (and why), and the final `openspec list` output. Report failures as they are; don't call anything done that you did not verify.
