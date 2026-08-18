---
name: openspec-implement-all
description: Drive every pending OpenSpec change to completion in one pass — pick the next change, branch, implement tasks test-first per task, verify, commit, archive, merge and push. Use when the user wants all pending changes implemented, not a single one.
allowed-tools: Bash(openspec:*), Bash(git:*)
license: MIT
compatibility: Requires openspec CLI and git.
metadata:
  author: project
  version: "1.1.0"
---

Drive every pending OpenSpec change to completion, one change per branch, until none remain.

**Store selection:** If the user names a store (a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the openspec commands that read or write specs and changes (`list`, `status`, `instructions`, `validate`, `archive`). Treat `--store <id>` as sticky for the rest of the loop. Without a store, commands act on the nearest local `openspec/` root.

**Input:** Optionally a change name to start with. If omitted, the loop selects changes itself (step 1).

## Preflight (once)

1. **Detect the base branch and whether this run resumes an in-progress change** — `git branch --show-current`:
   - **On a spec branch** (name starts with `change/`, or matches an active change in `openspec list --json`): this is a **resume** — a previous run left that change mid-flight. Its commits and `- [x]` tasks are progress, not errors; make it the first change to finish, on its existing branch. Derive the base branch as the nearest ancestor branch of the current branch (`git branch --merged <current>` lists ancestor branches; pick the one whose tip is closest to the current tip, preferring the remote default's local counterpart when names differ). **Confirm the derived base with the user** — they may be accumulating work on a non-default branch (e.g. `extras`) — then announce the base branch and the resumed change.
   - **On a detached HEAD**: fall back to `git symbolic-ref refs/remotes/origin/HEAD` (its local counterpart) and confirm with the user.
   - **Otherwise** the current branch is the base branch; announce it.
   The base branch is what every spec-branch starts from and merges back into **for this run**. Re-detect at each invocation; never assume a fixed name.
2. **Confirm push authorization** — the loop ends each change with a push; get a one-time go-ahead that names the remote target (the base branch, or another integration branch the user is accumulating work on).
3. **Reconcile the working tree** (`git status`) instead of blindly requiring it clean:
   - Clean: proceed.
   - Dirty: list the dirty files and ask the user how to resolve them before the loop starts — **commit** (as part of the resumed change when resuming, or on the base otherwise), **stash**, or **discard** (`git restore`). When resuming and the dirty files clearly belong to the resumed change's remaining tasks, suggest commit as the default — but still wait for the user's answer; never guess.

## Triage: audit pending changes before implementing

Run once at invocation, right after preflight. Surface changes that are not ready to implement and let the user decide their fate — never silently archive, implement, or skip a flagged change.

1. **Inventory** — `openspec list --json`; for each non-archived change run `openspec status --change "<name>" --json` and skim its `tasks.md` (plus `proposal.md`/`design.md` when present).
2. **Flag changes** in these categories:
   - **No tasks** (`totalTasks: 0`, status `no-tasks`): nothing to implement — it needs flesh-out or archiving.
   - **Incomplete planning** — any planning artifact not `done`/`skipped` (from status JSON), a `tasks.md` whose tasks reference behavior not described in the change's specs/design, or tasks with no verifiable outcome.
   - **Stale / drifted** — the change was last modified before commits landed on files it touches: run `git log --oneline -- <files named in the change's tasks/design>` and compare against the change's `lastModified` (from `openspec list --json`); also run `openspec doctor --json` and investigate any issues it reports for the change. If a referenced file moved on while the change sat still, its tasks may no longer match the codebase.
3. **Prompt the user per flagged change** — name the change, the category (no-tasks / incomplete / stale-drifted), and the evidence; offer:
   - **Flesh out now** — plan or refresh the change before implementing (use the `openspec-update-change` skill to revise its tasks/design/specs; use `openspec-propose` or explore mode for a change that is only a name or an empty proposal).
   - **Defer** — exclude from this run; the loop skips it and reports it as deferred.
   - **Archive as obsolete** — only when the change is genuinely stale or irrelevant; go through the `openspec-archive-change` skill (its own prompts still apply).
   Do not implement, skip, or archive a flagged change without an explicit user answer.
4. Unflagged changes proceed to the main loop; the triage outcome (deferred / to-be-fleshed-out / to-archive) is carried into the final summary.

## Main loop

While `openspec list --json` returns at least one non-archived change that was not deferred in triage (the list only contains non-archived changes):

### 1. Select the next change

- **Resumed change first** — if preflight step 1 identified an in-progress change on the current branch, select it first and skip scoring; it is already branched, and its incomplete tasks are the run's starting point.
- Run `openspec list --json`; for each candidate read its `proposal.md`, `design.md` and `tasks.md` (and note `completedTasks`/`totalTasks`).
- Score candidates and pick the one that either needs the least friction or unblocks others:
  - Closest to done (highest `completedTasks` / `totalTasks`).
  - Tasks that touch files no other pending change touches (least merge friction — compare the file paths named in each change's tasks/docs).
  - Foundational changes first (specs, schemas, or shared infrastructure other pending changes build on).
- **Defer and report** — never implement autonomously — any change that requires external resources (AWS, secrets, credentials, infrastructure) or an unresolved human decision.
- Zero-task changes were already routed in triage (flesh out, defer, or archive) — if one somehow reaches selection, apply the same triage prompt instead of archiving or implementing it silently.
- Re-verify freshness before branching: if a merge earlier in this run touched files the change references, return that change to the triage prompt instead of proceeding.

### 2. Branch

- `git switch -c change/<name>` — branches from the **base branch detected in preflight**; always use the `change/` prefix to namespace branches and avoid collisions with the base branch or other changes. Do not branch from a previous spec-branch: the loop returns to the base after each merge, so this is automatic.
- If `change/<name>` already exists, `git switch change/<name>` to **resume** instead of recreating (and verify it is based on the current base branch before merging later).
- Announce: "Using change: <name> on branch change/<name>, based on <base-branch>."

### 3. Per-task loop

For each task in the change's `tasks.md` still marked `- [ ]` (skip `- [x]` — already done):

1. **Derive the expected outcome** from the task text plus the change's spec and design: what behavior or artifact change does this task produce, and how would you verify it?
2. **Write the failing test first (TDD)** — but only for code tasks:
   - Code task: add the unit/integration test that fails today. A compile error because the API does not exist yet counts as the red state.
   - Doc/spec/tooling task: no test — the artifact itself (README/docs text, spec delta, script) is the deliverable. Note this in the commit.
3. **Confirm red** — run the new test scoped to it (e.g. `dotnet test <test-project> --filter <test>`); observe it fail or fail to compile.
4. **Implement the task** — load the `openspec-apply-change` skill and follow it **for exactly the current task**: it supplies `openspec instructions apply --change "<name>" --json`, the context files (proposal/specs/design/tasks), and the checkbox convention. Its own step 6 iterates over tasks — implement only the current one, mark `- [ ]` → `- [x]` in `tasks.md`, then return control to this loop.
5. **Verify green** — run the new test, then the repo gates (adapt to the project):
   - Build with warnings-as-errors (`dotnet build QueueApi.slnx` in this repo)
   - Full test run with coverage (`dotnet test QueueApi.slnx --collect:"XPlat Code Coverage"`)
   - Coverage ratchet, if the project has one (`bash scripts/check-coverage.sh` here)
   - Spec discipline (`openspec validate --all`)
   - E2E/smoke suites when the change touches the APIs' contract (`dotnet test tests/E2E/...`, `scripts/smoke-e2e.sh` here)
6. **Update docs** — update `README.md` and/or `docs/**` (and the docs index/`toc.yml` when pages are added, moved, or removed) as the task requires, per the project's docs conventions.
7. **Commit the task** — a focused commit summarizing the task, matching the repo's commit message convention (including its standard footer). Never bundle unrelated changes.
8. **Pause rule** — if a sub-skill pauses for user input (ambiguity, scope growth, blocker, archive-sync prompt), stop the whole loop, surface the prompt, and wait. Never guess.

### 4. Finish the change

- When all tasks are `- [x]`: re-run the full gate set from step 3.5 once more and confirm docs alignment (README/docs reflect the change).
- Load the `openspec-archive-change` skill and follow it: it checks artifact/task completion, assesses delta-spec sync (running the inline `openspec-sync-specs` merge when needed), and archives `openspec/changes/<name>` → `openspec/changes/archive/YYYY-MM-DD-<name>`. Honor its sync prompts; never hand-move or `rm` a change directory.

### 5. Merge, push, clean up

- Refresh against the base branch: `git fetch origin`, then `git switch <base-branch>` and update it only as far as the user's own state allows (`git merge --ff-only origin/<base-branch>` — if the local base is ahead of origin, as is common, this is a no-op; do not force-pull or rebase the user's base). Then `git switch change/<name> && git rebase <base-branch>` (or merge it in). Resolve any conflicts; re-run the gates if the refresh changed anything.
- Merge back into the base branch: `git switch <base-branch> && git merge --no-ff change/<name>` (fast-forward is fine when clean), then `git push origin <base-branch>`.
- Delete the branch: `git branch -d change/<name>`; push the deletion only if the branch was pushed.
- Push only when the gates are green and the working tree is clean. Never force-push.

### 6. Loop

- Re-run `openspec list --json`. Repeat from step 1 while any non-archived, non-deferred change remains; otherwise exit with a per-change summary: implemented + archived, merged + pushed, fleshed out at triage, deferred with its reason, or archived as obsolete.

## Guardrails

- Never implement changes needing external credentials, secrets, AWS, or infrastructure without explicit user go-ahead — defer and report.
- Never push without a green gate run and a clean tree; never force-push.
- Use the `openspec` CLI for change metadata (status, instructions, validation) and `openspec-archive-change` for archiving — never hand-edit or hand-move change directories.
- A task is done only when its specified behavior is fully implemented and verified — never when partially done or deferred.
- Keep commits per-task and scoped; a task commit must not bundle unrelated changes.
- Pause the loop on any sub-skill prompt, conflict, or blocker; surface it; wait.
- Resume, don't recreate: existing `change/<name>` branch or `- [x]` tasks are progress, not errors.
- The loop's exit condition is machine-checked: no non-archived changes in `openspec list --json`.
- Never implement, skip, or archive a change flagged in triage without the user's explicit choice.
- Triage evidence is shown per change (category + why); do not batch-flag silently.
