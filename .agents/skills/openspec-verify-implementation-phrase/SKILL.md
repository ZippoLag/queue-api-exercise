---
name: openspec-verify-implementation-phrase
description: Verify how a given phrase from the requirements is implemented, tested, and documented — across code, tests, XML docs, OpenAPI, and the docs folder. Diagnostic only; explore-mode report. Use with a phrase, e.g. /skill:openspec-verify-implementation-phrase <phrase or file ref>.
allowed-tools: Bash(openspec:*)
license: MIT
compatibility: Requires openspec CLI.
metadata:
  author: project
  version: "1.0.0"
---

Verify how a phrase from the requirements is implemented in code, how it is tested, and how consistently it is carried through docs and contracts. This is the explore-mode diagnostic — **read, search, report; never modify code, tests, docs, specs, or OpenSpec artifacts.**

**Input: THE_PHRASE** — passed with the invocation. Extraction rules, in order:
1. If the invocation carries a fenced or `---`-delimited block, THE_PHRASE is the text between the two markers.
2. Otherwise THE_PHRASE is everything after the skill name up to the first line starting with "How is it being tested?" (or any other standard question from the report script below).
3. If the user passes a file path, change name, or section reference instead of literal text, read that file and take the quoted phrase (first fenced/`---`-delimited block; else the section named).

**Store selection:** If the user names a store (a standalone OpenSpec repo registered on this machine) or the work lives in one, run `openspec store list --json` to discover registered store ids, then pass `--store <id>` on openspec commands that read specs or changes (`list`, `status`, `view`, `validate`). Treat `--store <id>` as sticky for the session. Without a store, commands act on the nearest local `openspec/` root.

## Steps

1. **Locate and normalize the phrase**
   - Search `docs/archived/initial_requirements.md` first (most phrases come from there), then the repo: verbatim search for a distinctive substring, then a normalized search (whitespace/case/unicode-insensitive).
   - Record the source (file + section) when found; if not found, state it is an externally-provided phrase and verify against its meaning anyway.
   - **Decompose into clauses** — one sentence or one requirement each. The whole report is clause-scoped.

2. **Map each clause to the implementation**
   - For each clause, find the implementing code (endpoints, handlers, repositories, config, workers, tooling), tests, and documentation. Use `code_search` + `read_files`; use `openspec list --json`/`openspec view` and the specs under `openspec/specs/` for spec coverage; use `orient`/`search_code` when available.
   - Classify each clause: **Implemented-as-stated** / **Implemented-with-interpretation** (quote what the code does vs what the phrase says) / **Not-implemented** / **Contradicted** / **Documentation-only**.

3. **How is it being tested?**
   - Find the tests covering each clause's behavior (search `tests/`; note the project's coverage gate). Distinguish **behavior-verified** from **line-covered-only** — a line can execute while its guarantee is never asserted (e.g. `AsNoTracking` running without proof that nothing is tracked). Explicitly list untested guarantees.

4. **Gaps, contradictions and/or conflicts: phrase vs implementation and tests**
   - Per clause: does the code do less, more, or different than the phrase promises? Does a test assert something the phrase never declares, or vice versa? Quote both sides.

5. **Assumptions not declared in the phrase**
   - What did the implementation assume that the phrase does not say (tech choices, scope interpretations, timing or durability guarantees)? Label benign vs contract-changing.

6. **Decisions that complement, modify and/or conflict with the phrase**
   - Which documented design decisions (`docs/architecture.md`, change designs, ADRs) extend, reinterpret, or contradict the phrase? Label each **complement** / **modify** / **conflict**.

7. **XML documentation alignment**
   - Check the source XML comments on the implementing classes/methods and their tests: `<summary>` explains what beyond the name; `<remarks>` cites the business rule (per the project's standards). Flag missing, stale, or rule-claiming comments the code does not back. Generated `*/bin/*.xml` is a build artifact — only spot-check it.

8. **OpenAPI documentation clarity**
   - Inspect the endpoint metadata and document transformers (`WithSummary`/`WithDescription`/`ConfigureOpenApiOperation`) and the OpenAPI contract tests. Does the served contract describe the behavior the phrase implies (status codes, acceptance semantics, error modes)? Flag under-disclosure and over-disclosure (e.g. leaking implementation details the specs forbid).

9. **Docs folder alignment**
   - Find the doc file that owns the topic (in this repo: `docs/architecture.md` owns design decisions and behavior, `docs/dsl_glossary.md` terminology, `docs/testing.md` testing, `docs/api-contract.md` the API contract — check the ownership table in `AGENTS.md`). Verify the owning file is concise and current; flag replicated facts across files (one fact, one home) and missing relative links. **Leave `docs/archived/initial_requirements.md` aside** — it is the invariant file: use it as a phrase source, never critique or edit it.

10. **Report**
    - Lead with a **per-clause verdict table**: clause | implementation class | tests | gaps/conflicts | assumptions | decisions | XML | OpenAPI | docs.
    - Follow with the notable findings (one line each, evidence-tagged) and a final overall verdict: **aligned** / **mostly-aligned-with-noted-deviations** / **contradicted**.
    - Stamp the snapshot: commit + date (e.g. `git rev-parse --short HEAD`) — findings age.
    - End with capture offers (record a decision, propose a change for a gap) — only if the user asks; never auto-capture.

## Guardrails

- Diagnostic only: never modify code, tests, docs, specs, or artifacts during verification. Report and offer; implementation happens after the user exits explore mode or approves a change.
- The archived `docs/archived/initial_requirements.md` is invariant: phrase source only, never critique or edit.
- Ground every finding in evidence (file path + line, or a verbatim quote). Mark anything unverifiable as **unverified** — never as "fine".
- If the phrase maps to nothing in the repo, say so explicitly; do not invent coverage.
- Per-clause classification is mandatory — "implemented" without a class is not a finding.
- Keep the report terminal-friendly: verdict table first, details after.
