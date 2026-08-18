## Purpose

The documentation rendered by the DocFX site SHALL include a canonical SQL schema reference for the two stores and SHALL record the design rationale for the persistence model, including why the administrator visibility flag lives on the entity table.

## ADDED Requirements

### Requirement: Database schema is documented canonically

The SQL schema of both stores — the auth store (`Users`) and the CMS store (`cms_event_log`, `cms_entities`) — SHALL be documented in a single canonical reference page (`docs/database-schema.md`) covering every table's columns, types, nullability, keys, and indexes, plus the SQLite journaling configuration. Conceptual documentation SHALL link to this reference instead of duplicating column-level detail.

#### Scenario: Every persisted table is listed

- **WHEN** a reader opens `docs/database-schema.md`
- **THEN** every persisted table (`Users`, `cms_event_log`, `cms_entities`) is listed with its columns, types, nullability, keys, and indexes

#### Scenario: Conceptual docs link to the schema reference

- **WHEN** a reader consults the persistence section of `docs/architecture.md`
- **THEN** it links to `docs/database-schema.md` rather than restating the full column lists

#### Scenario: The schema page is discoverable from the docs site

- **WHEN** a reader browses the DocFX site
- **THEN** `docs/database-schema.md` appears in the navigation (`toc.yml`) and the README docs index

### Requirement: Entity visibility flag placement is documented

The documentation SHALL state that the administrator visibility flag (`is_visible_by_admin`) lives on the `cms_entities` table and SHALL record the rationale: at one-node SQLite scale a separate visibility table adds joins and transactions for no measurable gain, and a split becomes justified only when the two APIs get separate databases or visibility becomes multi-valued.

#### Scenario: Reader learns why the flag is not a separate table

- **WHEN** a reader consults the persistence design notes in `docs/architecture.md`
- **THEN** the note explains that the flag intentionally stays on `cms_entities` and names the conditions under which a split would be reconsidered
