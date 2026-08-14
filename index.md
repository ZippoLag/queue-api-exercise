# Queue API Exercise

A .NET 9 modular-monolith exercise: a CMS Webhook API with Basic authentication, an in-process outbox, and a shared credential store, all guarded by a 100% coverage ratchet and CI quality gates.

This site is **generated** by [DocFX](https://dotnet.github.io/docfx/) from two sources:

- **Conceptual documentation** — the hand-written Markdown under `docs/` (`architecture`, `configuration`, `dsl_glossary`, `development-style`) plus this repository's `README`.
- **API reference** — generated from the XML documentation comments in the source code (`src/**`).

The **canonical sources remain the Markdown files and the OpenSpec specs** (`openspec/specs`) — the machine-readable sources agents and humans both read. This site is a rendered view of that content, regenerated on every push to `main`, never a parallel hand-maintained copy.

## Sections

- [Architecture](docs/architecture.md) — system overview, design decisions, API and event-processing semantics
- [Configuration](docs/configuration.md) — configuration strategy
- [Domain glossary](docs/dsl_glossary.md) — domain specific language: terminology and nomenclature
- [Development style](docs/development-style.md) — development approach, AI assistance, and tooling setup

Use the **API Reference** item in the navigation bar for the generated API documentation.
