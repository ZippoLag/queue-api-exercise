# Design: Database Schema Documentation

## Problem

The SQL schema of the two stores exists only as prose inside `docs/architecture.md`; there is no canonical reference, and the rationale for keeping the administrator visibility flag on `cms_entities` is undocumented.

## Decisions

### D1 — `docs/database-schema.md` is the single canonical home for schema facts

All column-level facts (tables, columns, types, nullability, keys, indexes, journaling configuration) live in the new page. `docs/architecture.md` keeps its conceptual summary of the persistence model and links to the reference; it does not restate column lists. This follows the project's one-fact-one-home rule — architecture.md owns design rationale, the schema page owns SQL structure.

### D2 — The page mirrors the two-store organization

Sections:
- **Auth store** — `Users` table (username, PBKDF2 hash/salt, reserved-role flags).
- **CMS store** — `cms_event_log` (outbox: id, type, entity id, payload JSON, version, timestamp, status, timestamps) and `cms_entities` (id, type, version, payload JSON, visibility flag, timestamps), with the event→entity relationship.
- **Store configuration** — SQLite WAL journal mode, busy timeout, `EnsureCreated` startup creation, and the `ConnectionStrings:*` / `Data:DbBasePath` resolution rules (linking to `docs/configuration.md` rather than duplicating them).

Each table gets a column table (name, type, nullable, key, description) plus an index/key list.

### D3 — The EF model is the source of truth; the doc is verified against it

`EnsureCreated` derives the schema from the EF entity classes, so documenting from the entity model *is* documenting the real schema. The implementation task reads the actual `DbContext`/entity classes and transcribes exactly what they declare — no invented columns, no EF-flavored naming guesses. The page states this provenance in its intro so readers know where the facts come from.

### D4 — The visibility-flag rationale lives in `architecture.md`, with a pointer from the schema page

One fact, one home: the *why* (single boolean, one-node SQLite, split only pays off at separate databases or multi-valued visibility) belongs in architecture.md's persistence design notes; `database-schema.md` documents the column itself and points to the note. The split is a deliberate non-change, so it is recorded as a documented decision rather than silently absent.

### D5 — No diagrams in this change

The mermaid-diagrams change owns diagram authoring. This change ships text + tables only, so the two changes stay reviewable and independently landable. The mermaid change may later add an ERD to this page.

### D6 — Discoverability

`toc.yml` gains the page after "Architecture", and `README.md`'s docs index lists it — matching how every other `docs/` page is surfaced.

## Affected Files

| File | Change |
|---|---|
| `docs/database-schema.md` (new) | Canonical SQL reference |
| `docs/architecture.md` | Persistence section: summary + link; visibility-flag design note |
| `toc.yml` | New page entry |
| `README.md` | Docs index entry |

## Risks

- **Drift from the EF model:** the doc can silently age as entities change. Mitigated by D3 (provenance statement naming the EF model as source of truth) — the same freshness signal the other docs rely on — and by the doc-review habit the testing pyramid change established: contract-touching changes extend the docs in the same change.
- **Duplication temptation:** reviewers may re-add column lists to architecture.md. D1 names architecture.md as the home for rationale, and the schema page as the home for structure — the task list includes a check that no column list is duplicated.

## Verification

1. Every column in the doc matches the EF entity classes (task-level check, item by item).
2. `docfx build` succeeds and the new page renders with working links.
3. `toc.yml` / README entries present.
4. `openspec validate --all`.
