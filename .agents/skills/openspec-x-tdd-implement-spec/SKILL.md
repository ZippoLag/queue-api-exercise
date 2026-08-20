---
name: openspec-x-tdd-implement-spec
description: Implement all pending tasks of one OpenSpec change test-first via openspec-x-tdd-implement-task, then re-run the gates, archive, merge, and push the branch. Use to drive one change to completion, alone or as step 2 of the openspec-x-tdd-apply-all loop.
allowed-tools: Bash(openspec:*), Bash(git:*)
license: MIT
compatibility: Requires openspec CLI and git.
metadata:
  author: project
  version: "1.0.0"
---

Implement every pending task of one OpenSpec change test-first, then finish the change (archive, merge, push). This is the per-change step of the TDD apply-all loop and is fully usable standalone. Task implementation is delegated to `openspec-x-tdd-implement-task` — this skill never implements tasks itself.

**Store selection:** If the user names a store (a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the openspec commands that read or write specs and changes (`list`, `status`, `instructions`, `validate`, `archive`, `doctor`). Treat `--store <id>` as sticky for the rest of the workflow. Without a store, commands act on the nearest local `openspec/` root.

**Input:** A change name (e.g. `/openspec-x-tdd-implement-spec add-auth`), or omit it to infer the change from the current branch (`change/<name>`) — ask when neither applies. Optional "push pre-authorized" (apply-all grants it once in preflight; standalone you confirm before pushing in step 5).

## Steps

1. **Resolve the change and ensure the branch**
   - `openspec list --json` / `openspec status --change "<name>" --json`; announce the change.
   - **Planning check** — if planning is incomplete (`isPlanningComplete: false`, or any artifact in `applyRequires` not `done`/`skipped`), report and stop: plan first (`openspec-propose` / `openspec-update-change`). Never implement from incomplete planning.
   - **Branch** — if the current branch is `change/<name>`, proceed in place. Else if `change/<name>` exists, `git switch change/<name>` (resume). Else create it: derive the base branch (nearest ancestor of the current branch via `git branch --merged`, confirming with the user when derived; on detached HEAD use the local counterpart of `git symbolic-ref refs/remotes/origin/HEAD`) and `git switch -c change/<name>` from it.

2. **Per-task loop**
   - List the pending tasks: `openspec instructions apply --change "<name>" --json` (or read `tasks.md` plus `completedTasks`/`totalTasks` from status JSON).
   - For each task still marked `- [ ]` (skip `- [x]` — already done): invoke `openspec-x-tdd-implement-task` for **exactly that task** — load that skill and follow it for the current task only, then return control here. Re-check progress after each task (CLI counts, never memory).
   - **Pause rule** — if `openspec-x-tdd-implement-task` pauses (ambiguity, scope growth, blocker, or an unresolvable verify verdict), stop the whole loop, surface the prompt, and wait. Never guess.

3. **When all tasks are `- [x]`** — re-run the full gate set once more and confirm docs alignment:
   - Build with warnings-as-errors, full test run with coverage, coverage ratchet, spec discipline (`openspec validate --all`), and E2E/smoke suites when the change touches the APIs' contract — the exact commands are the gate set in `openspec-x-tdd-implement-task` step 7.
   - Confirm `README.md` and `docs/**` (and `toc.yml`/docs index when pages moved) reflect the change, per the project's docs conventions.

4. **Archive** — load the `openspec-archive-change` skill and follow it: it checks artifact/task completion, assesses delta-spec sync (running the inline `openspec-sync-specs` merge when needed), and archives `openspec/changes/<name>` → `openspec/changes/archive/YYYY-MM-DD-<name>`. Honor its sync prompts; never hand-move or `rm` a change directory.

5. **Merge, push, clean up**
   - Refresh against the base branch: `git fetch origin`, then `git switch <base-branch>` and update it only as far as the user's own state allows (`git merge --ff-only origin/<base-branch>` — if the local base is ahead of origin, as is common, this is a no-op; do not force-pull or rebase the user's base). Then `git switch change/<name> && git rebase <base-branch>` (or merge it in). Resolve any conflicts; re-run the gates if the refresh changed anything.
   - Merge back into the base branch: `git switch <base-branch> && git merge --no-ff change/<name>` (fast-forward is fine when clean).
   - Push: `git push origin <base-branch>` — **confirm with the user first** unless the invocation says push is pre-authorized. Push only when the gates are green and the working tree is clean. Never force-push.
   - Delete the branch: `git branch -d change/<name>`; push the deletion only if the branch was pushed.

6. **Report** — per-change summary: tasks implemented, archived location, merged into `<base>`, pushed (or not, and why).

## Guardrails

- A task is done only when `openspec-x-tdd-implement-task` marked it complete after its review and gates — never when partially done or deferred.
- Never push without a green gate run and a clean tree; never force-push; confirm push unless pre-authorized.
- Use `openspec-archive-change` for archiving — never hand-edit or hand-move change directories.
- Pause the loop on any sub-skill prompt, conflict, or blocker; surface it; wait.
- Resume, don't recreate: existing `change/<name>` branch or `- [x]` tasks are progress, not errors.
- Never implement from incomplete planning; stop and route to planning first.
