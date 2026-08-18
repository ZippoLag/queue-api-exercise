## 1. Canonical schema reference

- [x] 1.1 Read the actual EF model — `UsersDbContext` + `User`, `CmsDbContext` + event log/event and entity entities, including the visibility flag — and transcribe the exact schema (columns, types, nullability, keys, indexes)
- [x] 1.2 Write `docs/database-schema.md`: auth store (`Users`), CMS store (`cms_event_log`, `cms_entities`) with the event→entity relationship, and a store-configuration section (WAL journal mode, busy timeout, `EnsureCreated`, connection-string/base-path resolution linking to `docs/configuration.md`); the intro states the EF model is the source of truth
- [x] 1.3 No diagrams in this page (the mermaid-diagrams change owns diagram authoring)

## 2. Conceptual docs and discoverability

- [x] 2.1 `docs/architecture.md` persistence section: condense to a conceptual summary and link to `docs/database-schema.md` without duplicating column lists
- [x] 2.2 `docs/architecture.md`: add the visibility-flag design note — why `is_visible_by_admin` stays on `cms_entities`, and the conditions under which a split would be reconsidered (separate databases per API, or multi-valued visibility)
- [x] 2.3 `toc.yml`: add the new page after Architecture; `README.md`: list it in the docs index

## 3. Verification

- [x] 3.1 `docfx build` succeeds and the new page renders with working links
- [x] 3.2 Confirm no column-level detail is duplicated in `docs/architecture.md`
- [x] 3.3 `openspec validate --all`
