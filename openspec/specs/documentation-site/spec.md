# documentation-site Specification

## Purpose
The documentation rendered by the DocFX site SHALL use Mermaid as the single diagram syntax, SHALL present the system overview as an editable Mermaid diagram, and SHALL be governed by authoring guidance in `AGENTS.md` so diagrams follow one convention.

## Requirements

### Requirement: System overview is authored in Mermaid

The system overview diagram SHALL be authored as a Mermaid code fence inside `docs/architecture.md`; the exported drawio/PNG artifacts SHALL be removed so the Mermaid source is the single source of truth.

#### Scenario: Overview renders from Mermaid source

- **WHEN** a reader views the system overview on the DocFX site
- **THEN** it renders from the Mermaid code fence in `docs/architecture.md`

#### Scenario: Exported image artifacts are gone

- **WHEN** a reader looks for the old overview artifacts
- **THEN** `docs/system_overview.drawio` and `docs/system_overview.png` no longer exist

### Requirement: Documentation diagrams are authored in Mermaid

Diagrams in the documentation SHALL be authored as Mermaid code fences rendered by DocFX, and `AGENTS.md` SHALL carry diagram authoring guidance — Mermaid as the only syntax, when a diagram adds value, and naming/location conventions — so agents and humans follow one convention.

#### Scenario: Diagram guidance exists in AGENTS.md

- **WHEN** an agent or contributor checks `AGENTS.md` for how to add a diagram
- **THEN** it finds explicit guidance: Mermaid as the only syntax, when a diagram adds value, and where diagrams live

#### Scenario: New diagrams render on the docs site

- **WHEN** a reader views a documentation page containing a diagram
- **THEN** the diagram renders from its Mermaid code fence (flowchart, sequence, or state diagram as appropriate)

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
