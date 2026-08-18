## Why

The two stores are described only as prose scattered through `docs/architecture.md`; there is no canonical SQL reference for the three tables (`Users`, `cms_event_log`, `cms_entities`) — columns, types, constraints, indexes. The DocFX site renders `docs/**`, so a dedicated schema reference is the natural home for this fact. Related: the design decision to keep the administrator visibility flag on `cms_entities` (rather than a separate table) is currently undocumented, so the rationale is invisible to future readers and agents.

## What Changes

- New `docs/database-schema.md`: canonical SQL reference for both stores — tables, columns, types, nullability, keys, indexes, the WAL + busy-timeout configuration, and the two-store split. One fact, one home: conceptual docs link to it instead of duplicating column detail.
- `docs/architecture.md` persistence section: link to the schema reference, plus a design note explaining why `is_visible_by_admin` stays on `cms_entities` and when a split would become justified (separate databases per API, or multi-valued visibility).
- `toc.yml` and the `README.md` docs index list the new page.

## Capabilities

### New Capabilities

- `documentation-site`: documentation accuracy and structure for the DocFX-rendered site.

### Modified Capabilities

## Impact

- Docs only: new `docs/database-schema.md`; edits to `docs/architecture.md`, `toc.yml`, `README.md`.
- No code changes; no behavior changes.
