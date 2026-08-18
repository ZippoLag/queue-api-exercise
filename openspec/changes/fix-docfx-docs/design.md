## Context

See proposal.md - Why. The DocFX site renders `docs/**/*.md` plus the API reference extracted from `src/**` XML doc comments (see `docfx.json`). Two wording defects ship to readers today: the `delete` row of the Event semantics table in `docs/api-contract.md` ("removed/unpublished for good") conflates delete with `unPublish`, and three XML comments call the now-implemented Users API "deferred"/"(future)". The in-flight `sanitize-openapi-docs` change explicitly leaves this narrative content alone, so it is out of scope there and handled here.

## Goals / Non-Goals

**Goals:**
- Make the Event semantics table state that `delete` removes the entity from the store unrecoverably, distinct from `unPublish` (kept, hidden).
- Remove stale "deferred"/"(future) Users API" wording from the XML comments that render in the DocFX API reference.
- Add a cheap regression scan so the banned wording cannot silently return.

**Non-Goals:**
- Not touching `docs/archived/initial_requirements.md` (archived invariant) — its "removed or unpublished" phrasing is the source requirement and must stay; the scan excludes `docs/archived/**`.
- Not changing the OpenAPI documents (owned by `sanitize-openapi-docs`), runtime behavior, or specs.

## Decisions

### D1: Precise wording for the delete row

The `delete` row becomes *"The entity was deleted — removed from the store unrecoverably."*, keeping the existing third column ("**Hard-deletes** the entity from the store."). The "Why delete and unPublish differ" paragraph already states the distinction and stays unchanged.

- *Alternative:* "removed for good" — rejected: still vague about permanence and recoverability; the requirement is explicit about unrecoverable removal.

### D2: Fix the three stale XML comments

- `CmsEntity` class `<remarks>`: "used by the deferred Users API" → "used by the Users API".
- `CmsEntity.IsVisibleByAdmin` `<summary>`: "from the (future) Users API" → "from the Users API".
- `CmsDbContext` `<remarks>`: "the processed state the deferred Users API will read" → "the processed state the Users API reads".

Rationale: the Users API has been implemented since `users-api-vertical`; the comments describe the current design (the visibility override and the entity store are live inputs to it), so the qualifier is factually wrong, not just stale.

### D3: Regression scan as a script run in the docs workflow

Add `scripts/check-docs-wording.sh`: a grep-based check that fails when any banned phrase appears in the DocFX sources, excluding `docs/archived/**`. Banned phrases are the exact wording this change removes: "removed/unpublished for good" (and the "for good" delete conflation generally), "deferred Users API", and "(future) Users API". The script runs in `.github/workflows/docs.yml` before `dotnet docfx build`, so wording that would ship to the site fails the docs job.

- *Why docs.yml rather than ci.yml:* the ci-quality-gates spec owns `ci.yml`; adding the gate there would modify that capability's requirements. The docs workflow is the docfx-specific surface this change owns, and the check protects exactly what it builds. (A note is added below that PR-time enforcement would mean extending to `ci.yml` — a deliberate future decision, not made here.)
- *Why a script rather than a test project:* follows the project's precedent (docs-restructure verified docs via grep), avoids a new test project and repo-root discovery from test bin dirs, and keeps the check alongside the workflow that needs it.
- *Alternative:* a unit test asserting the phrases are absent — rejected as above (placement and discovery awkwardness for a docs concern).

## Risks / Trade-offs

- [Banned-phrase grep is brittle if wording legitimately changes later] → the phrases are exact strings this change removes; when a legit rewrite happens, the scan's pattern list is updated in the same commit.
- [docs.yml runs only on main, so a regression on a PR is caught post-merge, not in review] → accepted: the docs job fails loudly on main and the site stops updating; PR-time gating would extend the scan into `ci.yml`, which changes the quality-gates spec and is deliberately out of scope here.
- [Scan accidentally flags `docs/archived/initial_requirements.md`] → the script explicitly excludes `docs/archived/**` (the archived invariant must never be modified).

## Migration Plan

No deployment steps — documentation and a CI job change only. Rollback is a revert; the docs workflow re-runs on the next `main` push.

## Open Questions

None. (Whether the wording scan should also gate PRs via `ci.yml` is a deliberate non-goal recorded in D3, not an unknown.)
