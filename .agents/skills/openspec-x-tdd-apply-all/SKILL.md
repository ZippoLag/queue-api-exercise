---
name: openspec-x-tdd-apply-all
description: Drive every pending OpenSpec change to completion in one pass — orchestrating openspec-x-tdd-select-next-spec (triage, selection, branching) and openspec-x-tdd-implement-spec (test-first implementation, archive, merge, push) until none remain. Use when the user wants all pending changes implemented, not a single one.
allowed-tools: Bash(openspec:*), Bash(git:*)
license: MIT
compatibility: Requires openspec CLI and git.
metadata:
  author: project
  version: "2.0.0"
---

Drive every pending OpenSpec change to completion, one change per branch, until none remain. This skill orchestrates the `openspec-x-tdd-*` family; it does not implement tasks itself. Each sub-skill is independently runnable — a user may invoke any of them directly:

- `openspec-x-tdd-select-next-spec` — triage the pending changes, pick the next one (currently-active by default, otherwise scored), and land on its branch.
- `openspec-x-tdd-implement-spec` — implement all tasks of one change test-first, then archive, merge, and push.
- `openspec-x-tdd-implement-task` — implement a single task with small green commits, an independent review (including `openspec-x-verify-implementation-phrase`), and the checkbox flip.

**Store selection:** If the user names a store (a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the openspec commands that read or write specs and changes (`list`, `status`, `instructions`, `validate`, `archive`). Treat `--store <id>` as sticky for the rest of the loop. Without a store, commands act on the nearest local `openspec/` root.

**Input:** Optionally a change name to start with (it becomes the first active change). Optionally already-deferred names to skip (resuming a previous interrupted run). If omitted, the loop selects changes itself.

## Preflight (once)

1. **Detect the base branch and whether this run resumes an in-progress change** — `git branch --show-current`:
   - **On a spec branch** (name starts with `change/`, or matches an active change in `openspec list --json`): this is a **resume** — a previous run left that change mid-flight. Its commits and `- [x]` tasks are progress, not errors; make it the first change to finish, on its existing branch. Derive the base branch as the nearest ancestor branch of the current branch (`git branch --merged <current>` lists ancestor branches; pick the one whose tip is closest to the current tip, preferring the remote default's local counterpart when names differ). **Confirm the derived base with the user** — they may be accumulating work on a non-default branch (e.g. `extras`) — then announce the base branch and the resumed change.
   - **On a detached HEAD**: fall back to `git symbolic-ref refs/remotes/origin/HEAD` (its local counterpart) and confirm with the user.
   - **Otherwise** the current branch is the base branch; announce it.
   The base branch is what every spec-branch starts from and merges back into **for this run**. Re-detect at each invocation; never assume a fixed name.
2. **Confirm push authorization** — the loop ends each change with a push; get a one-time go-ahead that names the remote target (the base branch, or another integration branch the user is accumulating work on). This authorization is carried into every `openspec-x-tdd-implement-spec` invocation of the run (told "push pre-authorized").
3. **Reconcile the working tree** (`git status`) instead of blindly requiring it clean:
   - Clean: proceed.
   - Dirty: list the dirty files and ask the user how to resolve them before the loop starts — **commit** (as part of the resumed change when resuming, or on the base otherwise), **stash**, or **discard** (`git restore`). When resuming and the dirty files clearly belong to the resumed change's remaining tasks, suggest commit as the default — but still wait for the user's answer; never guess.

## Main loop

While `openspec list --json` returns at least one non-archived change that is not in this run's deferred set (the list only contains non-archived changes):

### 1. Select the next change

Invoke `openspec-x-tdd-select-next-spec` for this run, passing:
- the **base branch** from preflight,
- the run's **deferred names** (start with the input list; grow it as triage defers changes).

It triages the pending changes (prompting the user per flagged change — record the outcome), picks the currently-active change when there is one, otherwise scores the candidates, and lands on the change's branch. **Record the triage outcomes** into the run's state and the final summary:
- **Deferred** — add the name to the run's deferred set; the loop skips it and never re-prompts (select is told the names each iteration).
- **Fleshed out now** — the change re-enters the candidate set; select proceeds with it normally.
- **Archived as obsolete** — the change disappears from `openspec list` on its own.
- Changes **auto-deferred for external resources** (AWS, secrets, credentials, infrastructure) are reported, never implemented.

### 2. Implement the change

Invoke `openspec-x-tdd-implement-spec` for the selected change, with **push pre-authorized**. It implements every pending task via `openspec-x-tdd-implement-task`, re-runs the gates, archives the change (via `openspec-archive-change`), merges into the base branch, pushes, and deletes the branch.

**Pause rule** — if any sub-skill pauses for user input (ambiguity, scope growth, blocker, triage prompt, archive-sync prompt), stop the whole loop, surface the prompt, and wait. Never guess.

### 3. Loop

Re-run `openspec list --json`. Repeat from step 1 while any non-archived, non-deferred change remains; otherwise exit with a per-change summary: implemented + archived, merged + pushed, fleshed out at triage, deferred with its reason, or archived as obsolete.

## Guardrails

- Never implement changes needing external credentials, secrets, AWS, or infrastructure without explicit user go-ahead — the select step auto-defers and reports them.
- Never push without a green gate run and a clean tree; never force-push. Push authorization is confirmed once in preflight and carried as "push pre-authorized" into each implement-spec invocation.
- Use the `openspec` CLI for change metadata and the `openspec-x-tdd-*` / `openspec-archive-change` skills for their respective steps — never hand-edit or hand-move change directories.
- A task is done only when `openspec-x-tdd-implement-task` marked it complete after verification — never when partially done or deferred.
- Pause the loop on any sub-skill prompt, conflict, or blocker; surface it; wait.
- Resume, don't recreate: existing `change/<name>` branch or `- [x]` tasks are progress, not errors.
- The loop's exit condition is machine-checked: no non-archived, non-deferred changes in `openspec list --json`.
- Triage evidence is shown per change (category + why); do not batch-flag silently.
