---
name: openspec-x-tdd-select-next-spec
description: Triage the pending OpenSpec changes, pick the next one to implement — the currently-active change by default (resuming its change/<name> branch when one exists), otherwise the scored least-friction / highest-pay-off candidate — and land on its branch. Use to choose the next change, alone or as step 1 of the openspec-x-tdd-apply-all loop.
allowed-tools: Bash(openspec:*), Bash(git:*)
license: MIT
compatibility: Requires openspec CLI and git.
metadata:
  author: project
  version: "1.0.0"
---

Triage the pending OpenSpec changes, choose the next one to implement, and land on its branch. This is the selection step of the TDD apply-all loop and is fully usable standalone. **It only selects — it never implements, archives, syncs, or merges.** It leaves you on `change/<name>` for the selected change and announces the base branch, the branch, and whether the change was resumed or started fresh.

**Store selection:** If the user names a store (a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the openspec commands that read or write specs and changes (`list`, `status`, `instructions`, `validate`, `archive`, `doctor`). Treat `--store <id>` as sticky for the rest of the workflow. Without a store, commands act on the nearest local `openspec/` root.

**Input:** Optionally a change name to pick (it becomes the active change). Optionally a base branch (apply-all passes its preflight base; standalone you derive it — step 1). Optionally already-deferred names (apply-all passes the run's deferred set; you must not re-prompt for those).

## Steps

1. **Preflight**
   - **Base branch** — use the passed base; otherwise detect it: if the current branch is a spec branch (`change/...`), derive the nearest ancestor branch (`git branch --merged <current>`, preferring the remote default's local counterpart) and confirm with the user; on detached HEAD, fall back to the local counterpart of `git symbolic-ref refs/remotes/origin/HEAD` and confirm; otherwise the current branch is the base. Announce it.
   - **Working tree** — `git status`; if dirty and a branch switch is needed, reconcile with the user (**commit**, **stash**, or **discard**) before switching. Never switch branches with uncommitted work whose fate the user has not chosen.

2. **Triage: audit pending changes before selecting** — never silently archive, implement, or skip a flagged change.
   - **Inventory** — `openspec list --json`; for each non-archived change not in the deferred input list, run `openspec status --change "<name>" --json` and skim its `tasks.md` (plus `proposal.md`/`design.md` when present).
   - **Flag changes** in these categories:
     - **No tasks** (`totalTasks: 0`, status `no-tasks`): nothing to implement — it needs flesh-out or archiving.
     - **Incomplete planning** — any planning artifact not `done`/`skipped` (from status JSON), a `tasks.md` whose tasks reference behavior not described in the change's specs/design, or tasks with no verifiable outcome.
     - **Stale / drifted** — the change was last modified before commits landed on files it touches: run `git log --oneline -- <files named in the change's tasks/design>` and compare against the change's `lastModified` (from `openspec list --json`); also run `openspec doctor --json` and investigate any issues it reports for the change. If a referenced file moved on while the change sat still, its tasks may no longer match the codebase.
   - **Prompt the user per flagged change** — name the change, the category (no-tasks / incomplete / stale-drifted), and the evidence; offer:
     - **Flesh out now** — plan or refresh the change before selecting it (use the `openspec-update-change` skill to revise its tasks/design/specs; use `openspec-propose` or explore mode for a change that is only a name or an empty proposal).
     - **Defer** — exclude from this run; report it as deferred and (when apply-all drives) add it to the deferred set so later iterations never re-prompt.
     - **Archive as obsolete** — only when the change is genuinely stale or irrelevant; go through the `openspec-archive-change` skill (its own prompts still apply).
   - Do not implement, skip, or archive a flagged change without an explicit user answer. Triage evidence is shown per change (category + why), not batch-flagged silently.

3. **Determine the active change (when there is one)** — precedence chain, first match wins:
   1. **Explicit input** — a change name passed in the invocation; if it is not in `openspec list --json` (archived, misspelled, or not created), report and ask.
   2. **Branch-derived** — the current branch is `change/<name>`; that change is active (its unfinished tasks are the starting point).
   3. **Single in-progress change** — exactly one change in `openspec list --json` has status `in-progress`.
   4. **None** — skip to scoring (step 5).
   For the active change, realize:
   - If the **current branch matches the active change's name** (`change/<name>`) → continue working on it in place (resume).
   - Else if a **`change/<name>` branch exists** → switch to it and continue (resume). Branch names always use the `change/` prefix — "same name" means `change/<name>`, never a bare name.
   - Else → branch it fresh from the base (step 6).
   Re-verify the active change passes triage (a zero-task or stale active change goes back to the step 2 prompt rather than being resumed silently).

4. **Auto-defer external-resource changes** — never select autonomously any change that requires external resources (AWS, secrets, credentials, infrastructure) or an unresolved human decision; defer it and report it with its reason.

5. **Score the candidates** when there is no active change. For each candidate (non-archived, non-deferred, unflagged), read its `proposal.md`, `design.md` and `tasks.md`; note `completedTasks`/`totalTasks`, the files its tasks touch, and whether it is foundational. Pick the winner by this **pay-off-first** chain — the first discriminator that separates the field decides:
   1. **Foundational / unblocks others** — changes that add or modify specs, schemas, or shared infrastructure other pending changes build on, or whose completion unlocks other candidates.
   2. **Closest to done** — highest `completedTasks` / `totalTasks`.
   3. **Least merge friction** — its tasks touch files no other pending change touches (compare the file paths named in each change's tasks/docs).
   4. **Least recently touched** — oldest `lastModified`.
   State the winner and the evidence for the discriminator that decided it. Re-verify freshness before branching: if a merge earlier in the run touched files the candidate references (git log vs `lastModified`), return it to the triage prompt instead of proceeding. Zero-task changes were already routed in triage — if one somehow reaches scoring, apply the same triage prompt instead of picking it.

6. **Branch** — for the selected change:
   - `git switch -c change/<name>` from the **base branch** (never from a previous spec-branch: the loop returns to the base after each merge, so this is automatic).
   - If `change/<name>` already exists, `git switch change/<name>` to **resume** instead of recreating.
   - Announce: "Using change: <name> on branch change/<name>, based on <base-branch>."

7. **Report** — the selected change, the base branch, the branch, resumed vs new, the triage outcome (deferred / fleshed-out / to-archive names), and any auto-deferred (external-resource) changes. If no candidates remain, report that and stop.

## Guardrails

- Selection only: never implement, archive, sync, or merge here — those are `openspec-x-tdd-implement-spec` / `openspec-archive-change`'s jobs.
- Never pick, skip, or archive a flagged change without the user's explicit choice; show per-change evidence (category + why), not batch flags.
- Resume, don't recreate: an existing `change/<name>` branch or `- [x]` tasks are progress, not errors.
- Reconcile the working tree before switching branches; never switch with uncommitted work whose fate the user has not chosen.
- Deferred and auto-deferred changes are reported to the caller, never silently dropped.
- External-resource changes are deferred and reported, never implemented autonomously.
