## Why

The only diagram in the documentation is a hand-made drawio exported to PNG (`docs/system_overview.drawio` → `system_overview.png`) — a binary artifact that cannot be edited in-repo — and the rest of the docs have no diagrams at all. DocFX 2.78.5 renders Mermaid code fences natively with the modern default template. `AGENTS.md` has no diagram authoring guidance, so there is no convention for agents to follow.

## What Changes

- `AGENTS.md` gains a "Diagrams" subsection (authoring guidance): Mermaid as the only diagram syntax, when a diagram adds value, naming/location conventions, DocFX rendering.
- `docs/architecture.md`: replace the drawio/PNG system overview with a Mermaid equivalent; delete `docs/system_overview.drawio` and `docs/system_overview.png` so Mermaid becomes the single source of truth.
- Add Mermaid diagrams where they add value: the ingestion → outbox → worker sequence, the event-log state machine (Pending / Processed / Failed), the CQRS + ports-and-adapters layering, the CI pipeline flow (`docs/testing.md`), and the deploy + live-verify sequence (`docs/deployment-aws.md`).
- All diagrams authored as Mermaid code fences that DocFX renders.

## Capabilities

### New Capabilities

- `documentation-site`: documentation accuracy and structure for the DocFX-rendered site.

### Modified Capabilities

## Impact

- Docs + governance: `AGENTS.md`, `docs/architecture.md`, `docs/testing.md`, `docs/deployment-aws.md`; deleted `docs/system_overview.drawio` + `docs/system_overview.png`.
- No code changes; the DocFX build (v2.78.5) already renders Mermaid.
