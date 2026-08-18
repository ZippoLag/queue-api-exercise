## 1. Governance first

- [ ] 1.1 `AGENTS.md`: add a "Diagrams" subsection to the Documentation rules — Mermaid as the only diagram syntax, when a diagram adds value, diagrams illustrate (never introduce) facts already in the prose, and diagrams sit directly under the prose they illustrate

## 2. Replace the overview

- [ ] 2.1 `docs/architecture.md`: replace the PNG system overview with an equivalent Mermaid flowchart (two APIs, shared auth, outbox path, two stores)
- [ ] 2.2 Delete `docs/system_overview.drawio` and `docs/system_overview.png`; confirm no references to `system_overview.*` remain anywhere

## 3. Diagram sweep (value-add only)

- [ ] 3.1 `docs/architecture.md`: ingestion → outbox → worker sequence diagram
- [ ] 3.2 `docs/architecture.md`: event-log lifecycle state diagram (Pending / Processed / Failed)
- [ ] 3.3 `docs/architecture.md`: CQRS + ports-and-adapters layering flowchart
- [ ] 3.4 `docs/testing.md`: CI pipeline gate-order flowchart
- [ ] 3.5 `docs/deployment-aws.md`: deploy + live-verify sequence diagram
- [ ] 3.6 Pages without a natural diagram (configuration, debugging) stay text-only

## 4. Verification

- [ ] 4.1 `docfx build` succeeds and every diagram renders on the docs site
- [ ] 4.2 `openspec validate --all`
