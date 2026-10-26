---
name: the-prey-git-workflow
description: The Prey's git branching, worktree, commit and pull-request workflow. Use whenever you create a branch or worktree, commit, push or open a PR in this repo — and always when proposing, applying or archiving an OpenSpec change (opsx:propose, opsx:apply, opsx:archive, openspec-* skills), including autonomous runs that apply several changes in one go. main is production and is never touched; all work flows into development.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Git Workflow

## Branches

| Branch | Role | Who writes to it |
|---|---|---|
| `main` | Production. Pushes deploy and release. | **Nobody.** Never commit, merge, rebase or push to `main` from this workflow. It only changes through a PR from `development` that the user merges. |
| `development` | Integration branch where all work lands. | OpenSpec proposals and archives (direct commits), plus merged feature/run PRs. |
| `features/{change-name}` | Applying one OpenSpec change. | You, in its own worktree. PR into `development`. |
| `run/{identifier}` | Applying several OpenSpec changes autonomously. | You, in one worktree for the whole run. PR into `development`. |

Never force-push `main` or `development`. Never open a PR into `main`.

## Worktrees

- **`main` and `development` always live in the repo root** (`D:\projects\github.com\hexmasternl\the-prey`). Switch between them there with `git switch`; never create a separate worktree for either of them.
- **Feature and run branches always get their own worktree** in the sibling folder `the-prey-worktrees`, outside the repo root:

```
D:\projects\github.com\hexmasternl\
├── the-prey\                          # repo root: main / development only
└── the-prey-worktrees\
    ├── {change-name}\                 # features/{change-name}
    └── run-{identifier}\              # run/{identifier}
```

From the repo root that folder is `../the-prey-worktrees`. Create it if it doesn't exist. Do the work of a feature or run **only** inside its worktree — don't edit files in the repo root while a change is being applied.

## Step 0 — make sure `development` exists

Run this in the repo root before any of the flows below:

```bash
git fetch origin
if git show-ref --verify --quiet refs/remotes/origin/development; then
  git switch development 2>/dev/null || git switch -c development --track origin/development
  git pull --ff-only
else
  git switch -c development origin/main   # first time only: branch from main
  git push -u origin development
fi
```

If the repo root has uncommitted changes that don't belong to the current task, stop and ask the user instead of stashing or discarding them.

## Proposing or archiving an OpenSpec change

Proposals and archives are committed **directly to `development`** in the repo root and pushed automatically — no branch, no PR.

1. Step 0 (repo root on an up-to-date `development`).
2. Run the propose or archive skill as usual.
3. Commit only the OpenSpec files it touched and push:

```bash
git add openspec/
git commit -m "docs(openspec): propose {change-name}"      # or: archive {change-name}
git push
```

For an archive, the change's implementation should already be merged into `development` (the feature or run PR). If it isn't yet, tell the user and ask before archiving — otherwise `development` claims the change is done while the code is still in an open PR.

## Applying one OpenSpec change

1. Step 0, then create the worktree and branch from `development`:

```bash
git worktree add -b features/{change-name} ../the-prey-worktrees/{change-name} development
```

   If the branch already exists (resuming earlier work), reuse it: `git worktree add ../the-prey-worktrees/{change-name} features/{change-name}`, or just continue in the existing worktree (`git worktree list`).
2. Work **inside** `../the-prey-worktrees/{change-name}` — run every read, edit, build and test there.
3. Commit very frequently, in logical steps (see *Commits* below).
4. When all tasks are done and the build/tests for what you touched pass, push and open a PR into `development`:

```bash
git push -u origin features/{change-name}
gh pr create --base development --head features/{change-name} \
  --title "feat: {change-name}" --body "<summary, tasks done, how it was tested>"
```

5. Report the PR URL. Leave the worktree in place; it is removed after the PR is merged (see *Cleanup*).

## Applying several changes autonomously

When applying multiple OpenSpec changes in one go without the user stepping in between them, use **one branch and one worktree for the whole run**:

1. Pick an identifier: `{yyyy-MM-dd}-{short-slug}`, e.g. `run/2026-10-26-game-lobby`. Step 0, then:

```bash
git worktree add -b run/{identifier} ../the-prey-worktrees/run-{identifier} development
```

2. Apply every change in that worktree, one after another. Do **not** create `features/*` branches or extra worktrees for the individual changes.
3. Commit frequently within each change, exactly as for a single change; scope commit messages to the change being applied so the history shows where each one starts and ends.
4. When the run is done, push and open **one** PR into `development`:

```bash
git push -u origin run/{identifier}
gh pr create --base development --head run/{identifier} \
  --title "feat: autonomous run {identifier}" --body "<one section per change: summary, tasks, tests; plus any change that was skipped or left unfinished and why>"
```

If a change in the run can't be completed, commit what is sound, note it in the PR body, and carry on with the rest only if they don't depend on it.

## Commits

- Commit **very frequently**: at least after every completed task in `tasks.md`, and at any other point where the tree is in a coherent state (a handler plus its tests, a screen plus its strings). Prefer many small commits over one big one.
- Each commit is one logical change and includes the matching `tasks.md` checkbox update.
- Use Conventional Commits, as in the existing history: `feat(android): …`, `fix(games): …`, `test(playfields): …`, `ci: …`, `docs(openspec): …`.
- End every commit message with the attribution trailer from the current session instructions.
- Stage specific paths (`git add <paths>`), not `git add -A`, so stray files never ride along. Never commit secrets, `key.properties` or keystores.
- Push only at the end of an apply or run (for propose/archive: right after the commit). Never skip hooks.

## Cleanup

After a feature or run PR is merged (only when the user says so or `gh pr view` shows it merged):

```bash
git worktree remove ../the-prey-worktrees/{change-name}     # or run-{identifier}
git branch -d features/{change-name}                         # or run/{identifier}
git switch development && git pull --ff-only                 # in the repo root
```

Don't remove a worktree that has uncommitted or unpushed work — ask first.

## Quick checklist

- [ ] Repo root is on `main` or `development`, never on a feature/run branch.
- [ ] Nothing was committed or pushed to `main`.
- [ ] Propose/archive → committed and pushed straight to `development`.
- [ ] Apply → `features/{change-name}` in `../the-prey-worktrees/{change-name}`, PR into `development`.
- [ ] Autonomous run → one `run/{identifier}` branch, one worktree, one PR into `development`.
- [ ] Frequent, logical, conventional commits with `tasks.md` updates included.
