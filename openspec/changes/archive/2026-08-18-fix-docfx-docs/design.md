## Context

See proposal.md - Why. The DocFX site renders `docs/**/*.md` plus the API reference extracted from `src/**` XML doc comments (see `docfx.json`). Two wording defects ship to readers today: the `delete` row of the Event semantics table in `docs/api-contract.md` ("removed/unpublished for good") conflates delete with `unPublish`, and three XML comments call the now-implemented Users API "deferred"/"(future)". The in-flight `sanitize-openapi-docs` change explicitly leaves this narrative content alone, so it is out of scope there and handled here.

## Goals / Non-Goals

**Goals:**
- Make the Event semantics table state that `delete` removes the entity from the store unrecoverably, distinct from `unPublish` (kept, hidden).
- Remove stale "deferred"/"(future) Users API" wording from the XML comments that render in the DocFX API reference.

**Non-Goals:**
- Not touching `docs/archived/initial_requirements.md` (archived invariant) — its "removed or unpublished" phrasing is the source requirement and must stay.
- Not adding a wording regression scan (a CI grep/script was considered and dropped as overkill for two known phrasing defects; the wording is fixed in place).
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

## Risks / Trade-offs

- [The wording defect could silently return in a future edit] → accepted: the corrected phrasing is plain and matches the surrounding domain language, and the site is rebuilt from the same sources on every `main` push; a regression scan was considered (see Non-Goals) and dropped as overkill.

## Migration Plan

No deployment steps — documentation only. Rollback is a revert; the docs site re-renders on the next `main` push.

## Open Questions

None.
