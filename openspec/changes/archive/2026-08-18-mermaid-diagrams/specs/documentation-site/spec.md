## Purpose

The documentation rendered by the DocFX site SHALL use Mermaid as the single diagram syntax, SHALL present the system overview as an editable Mermaid diagram, and SHALL be governed by authoring guidance in `AGENTS.md` so diagrams follow one convention.

## ADDED Requirements

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
