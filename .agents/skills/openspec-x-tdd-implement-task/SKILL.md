---
name: openspec-x-tdd-implement-task
description: Implement one OpenSpec task test-first with small green commits, review the work independently before completion — including verifying the task's phrase-definition with openspec-x-verify-implementation-phrase — then mark the task complete. Use for a single task, alone or driven by openspec-x-tdd-implement-spec.
allowed-tools: Bash(openspec:*), Bash(git:*)
license: MIT
compatibility: Requires openspec CLI and git.
metadata:
  author: project
  version: "1.0.0"
---

Implement a single OpenSpec task test-first, committing small green WIP advancements, then perform a thorough independent review of the whole task — including invoking `openspec-x-verify-implementation-phrase` on the task's phrase-definition to hunt gaps and overlaps — before marking the task complete. This is the leaf step of the TDD apply-all loop and is fully usable standalone. It implements exactly one task; it never pushes.

**Store selection:** If the user names a store (a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on the openspec commands that read or write specs and changes (`list`, `status`, `instructions`, `validate`, `archive`, `doctor`). Treat `--store <id>` as sticky for the rest of the workflow. Without a store, commands act on the nearest local `openspec/` root.

**Input:** A change name and a task (number like `2.1` or a distinctive text match, e.g. `/openspec-x-tdd-implement-task add-auth 2.3`). Omit both to infer the change from the current branch (`change/<name>`) and take the first pending task; ask when ambiguous.

## Steps

1. **Load context** — `openspec instructions apply --change "<name>" --json`; read **every** file in `contextFiles` (proposal, specs, design, tasks) fresh from disk, even if seen earlier in the conversation (the user may have edited them). Treat the returned `context` as a required prompt-level input and `operationGuidance` as optional additive advice; never copy either into implementation files or planning artifacts.

2. **Identify the task** — locate it in `tasks.md` (a pending `- [ ]` matching the number/text). Announce it. Derive the **expected outcome**: what behavior or artifact change the task produces, from the task text plus the change's spec and design, and how you would verify it.

3. **Derive THE_PHRASE (the task's phrase-definition)** — the basis for the later verification call, in order:
   - If the task text or the change's spec names a spec anchor (e.g. "spec: <requirement or scenario name>"), THE_PHRASE is that requirement/scenario's statement from the change's delta spec or the main spec.
   - Else if the change has delta specs and the task clearly maps to one requirement/scenario, use that scenario's statement (the requirement text and its WHEN/THEN).
   - Else use the task's own text.

4. **TDD red** (code tasks only) — write the failing test first: a unit/integration test that fails today (a compile error because the API does not exist yet counts as the red state). Confirm red by running the new test scoped to it (e.g. `dotnet test <test-project> --filter <test>`). **Doc/spec/tooling tasks have no red phase** — the artifact itself (README/docs text, spec delta, script) is the deliverable; note that in the commit.

5. **Green WIP implementation — small commits, green-only**
   - Implement in small, coherent advancements. Before each commit: the project compiles and the new test passes (plus the scoped tests around the change). The red state is observed, never committed.
   - Commit each advancement with a focused, conventional message matching the repo's commit convention (including its standard footer). Never bundle unrelated changes in a commit.
   - Review each commit's own diff (`git diff` / `git diff --cached`) with fresh eyes before committing.
   - **Never push** during a task — only the implementing skill's merge step (`openspec-x-tdd-implement-spec` step 5) pushes.

6. **Thorough independent review — once, on the accumulated task diff, before completion**
   - Re-read the task, THE_PHRASE, and the relevant spec/design fresh (not from memory of writing the code).
   - Review the whole task diff (`git diff <branch-point>...`, or against the base branch) as a unit: does it do exactly what the task requires — nothing missing (**gaps**), nothing unrelated or duplicated (**overlaps**)?
   - Invoke `openspec-x-verify-implementation-phrase` with THE_PHRASE (load that skill and follow it; it is read-only — it never edits, you do the fixing). Act on its verdict:
     - `contradicted`, or `not-implemented` clauses that the task requires → fix with further green WIP commits and re-verify; if the task or spec itself is wrong, pause and surface it.
     - Gaps/overlaps the report flags → address them before proceeding (XML comments, OpenAPI metadata, and docs alignment the report flags are part of the task deliverable).
     - `aligned` / `mostly-aligned-with-noted-deviations` with only benign deviations → proceed.
   - Never mark the task complete on an unresolved or unverified finding.

7. **Gates** — build with warnings-as-errors, full test run with coverage, coverage ratchet, spec discipline (`openspec validate --all`), and E2E/smoke suites when the change touches the APIs' contract. In this repo: `dotnet build QueueApi.slnx`, `dotnet test QueueApi.slnx --collect:"XPlat Code Coverage"`, `bash scripts/check-coverage.sh`, plus `dotnet test tests/E2E/...` and `scripts/smoke-e2e.sh` for contract touches. Adapt to the project.

8. **Docs update** — update `README.md` and/or `docs/**` (and the docs index/`toc.yml` when pages are added, moved, or removed) as the task requires, per the project's docs conventions.

9. **Mark complete** — flip `- [ ]` → `- [x]` in `tasks.md` and fold the flip into the task's final commit (the last WIP commit carries it; if the final state is already committed, make one small final commit for the flip). A task is complete only when its specified behavior is fully implemented **and** verified — never when partially done or deferred.

10. **Report** — task identifier and outcome, the verify verdict, and the list of commits produced.

## Pause rule

Pause and surface the prompt — never guess — when: the task is ambiguous; implementation reveals a design issue or scope beyond the task's spec; a blocker or error occurs; or the verify verdict is `contradicted` and you cannot reconcile it. Do not silently narrow, defer, or simplify away specified behavior.

## Guardrails

- Never mark `- [ ]` → `- [x]` until the independent review, the verify-phrase verdict, and the gates pass.
- Green-only commits: red states are observed, never committed.
- Never push during a task.
- Keep commits small, scoped, and per-advancement; a commit must not bundle unrelated changes.
- The verify skill is read-only; it never edits — you do the fixing.
- Ground every review finding in evidence (file + line, or a verbatim quote); mark anything unverifiable as unverified, never as "fine".
- Doc/spec/tooling tasks have no red phase; their deliverable is the artifact itself.
