---
name: "OPSX: Apply"
description: Implement tasks from an OpenSpec change (Experimental)
category: Workflow
tags: [workflow, artifacts, experimental]
---

Implement tasks from an OpenSpec change.

**Input**: Optionally specify a change name (e.g., `/opsx:apply add-auth`). If omitted, check if it can be inferred from conversation context. If vague or ambiguous you MUST prompt for available changes.

**Steps**

1. **Select the change**

   If a name is provided, use it. Otherwise:
   - Infer from conversation context if the user mentioned a change
   - Auto-select if only one active change exists
   - If ambiguous, run `openspec list --json` to get available changes and use the **AskUserQuestion tool** to let the user select

   Always announce: "Using change: <name>" and how to override (e.g., `/opsx:apply <other>`).

1a. **Set up the branch and worktree** (git workflow, see the `the-prey-git-workflow` skill)

   Never implement in the repo root and never on `main` or `development`. Each change gets its own `features/<name>` branch in its own worktree under `../the-prey-worktrees/` (a sibling of the repo root):
   ```bash
   git fetch origin
   git switch development && git pull --ff-only          # in the repo root
   git worktree add -b features/<name> ../the-prey-worktrees/<name> development
   ```
   - If `development` does not exist yet, create it from `origin/main` and push it first.
   - If the change's proposal is not committed on `development` yet, commit and push it there first so the worktree contains it.
   - If `features/<name>` already exists (resuming), reuse it: continue in its existing worktree (`git worktree list`) or run `git worktree add ../the-prey-worktrees/<name> features/<name>`.
   - **Autonomous run** (several changes applied in one go): don't create `features/*` branches; see "Autonomous Runs" below.

   From here on, run every read, edit, build, test and `openspec` command **inside the worktree**.

2. **Check status to understand the schema**
   ```bash
   openspec status --change "<name>" --json
   ```
   Parse the JSON to understand:
   - `schemaName`: The workflow being used (e.g., "spec-driven")
   - Which artifact contains the tasks (typically "tasks" for spec-driven, check status for others)

3. **Get apply instructions**

   ```bash
   openspec instructions apply --change "<name>" --json
   ```

   This returns:
   - Context file paths (varies by schema)
   - Progress (total, complete, remaining)
   - Task list with status
   - Dynamic instruction based on current state

   **Handle states:**
   - If `state: "blocked"` (missing artifacts): show message, suggest using `/opsx:continue`
   - If `state: "all_done"`: congratulate, suggest archive
   - Otherwise: proceed to implementation

4. **Read context files**

   Read the files listed in `contextFiles` from the apply instructions output.
   The files depend on the schema being used:
   - **spec-driven**: proposal, specs, design, tasks
   - Other schemas: follow the contextFiles from CLI output

5. **Show current progress**

   Display:
   - Schema being used
   - Progress: "N/M tasks complete"
   - Remaining tasks overview
   - Dynamic instruction from CLI

6. **Implement tasks (loop until done or blocked)**

   For each pending task:
   - Show which task is being worked on
   - Make the code changes required
   - Keep changes minimal and focused
   - Mark task complete in the tasks file: `- [ ]` → `- [x]`
   - **Commit** the task's changes together with its checkbox update: one logical Conventional Commit (e.g. `feat(games): add join endpoint`), staging specific paths, never `git add -A`. Commit more than once per task when a task has several coherent steps. Don't push yet.
   - Continue to next task

   **Pause if:**
   - Task is unclear → ask for clarification
   - Implementation reveals a design issue → suggest updating artifacts
   - Error or blocker encountered → report and wait for guidance
   - User interrupts

7. **On completion or pause, show status**

   Display:
   - Tasks completed this session
   - Overall progress: "N/M tasks complete"
   - If all done: suggest archive
   - If paused: explain why and wait for guidance

8. **On completion: push and open a PR into `development`**

   When all tasks are done and the build/tests for what you touched pass:
   ```bash
   git push -u origin features/<name>
   gh pr create --base development --head features/<name> --title "feat: <name>" --body "<summary, tasks completed, how it was tested>"
   ```
   Never open a PR into `main`. Report the PR URL. Leave the worktree in place until the PR is merged; archiving happens after the merge, from the repo root on `development`.

   On a pause, commit what is coherent but don't push or open a PR unless the user asks.

**Output During Implementation**

```
## Implementing: <change-name> (schema: <schema-name>)

Working on task 3/7: <task description>
[...implementation happening...]
✓ Task complete

Working on task 4/7: <task description>
[...implementation happening...]
✓ Task complete
```

**Output On Completion**

```
## Implementation Complete

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 7/7 tasks complete ✓
**Branch:** features/<change-name> → PR <url> into `development`

### Completed This Session
- [x] Task 1
- [x] Task 2
...

All tasks complete! You can archive this change with `/opsx:archive`.
```

**Output On Pause (Issue Encountered)**

```
## Implementation Paused

**Change:** <change-name>
**Schema:** <schema-name>
**Progress:** 4/7 tasks complete

### Issue Encountered
<description of the issue>

**Options:**
1. <option 1>
2. <option 2>
3. Other approach

What would you like to do?
```

**Guardrails**
- Keep going through tasks until done or blocked
- Always read context files before starting (from the apply instructions output)
- If task is ambiguous, pause and ask before implementing
- If implementation reveals issues, pause and suggest artifact updates
- Keep code changes minimal and scoped to each task
- Update task checkbox immediately after completing each task
- Pause on errors, blockers, or unclear requirements - don't guess
- Use contextFiles from CLI output, don't assume specific file names
- Never implement in the repo root or on `main`/`development`; work in the change's worktree
- Commit after every completed task (small, logical, Conventional Commits); push and open a PR into `development` only when the change is complete

**Fluid Workflow Integration**

This skill supports the "actions on a change" model:

- **Can be invoked anytime**: Before all artifacts are done (if tasks exist), after partial implementation, interleaved with other actions
- **Allows artifact updates**: If implementation reveals design issues, suggest updating artifacts - not phase-locked, work fluidly

**Autonomous Runs**

When applying several changes in one go without the user stepping in between them (see the `the-prey-git-workflow` skill):

- Use **one** branch and **one** worktree for the whole run, created from an up-to-date `development`:
  ```bash
  git worktree add -b run/<yyyy-MM-dd>-<short-slug> ../the-prey-worktrees/run-<yyyy-MM-dd>-<short-slug> development
  ```
- Apply every change in that worktree, one after another, following steps 2–7 for each. No `features/*` branches, no extra worktrees.
- Commit frequently within each change; scope the commit messages to the change being applied.
- If a change can't be completed, commit what is sound, note it, and continue only with changes that don't depend on it.
- When the run is done: `git push -u origin run/<identifier>` and open **one** PR into `development` with a section per change (summary, tasks, tests, anything skipped or unfinished).
