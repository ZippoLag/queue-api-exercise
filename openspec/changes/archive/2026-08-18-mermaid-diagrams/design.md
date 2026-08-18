# Design: Mermaid Diagrams

## Problem

The only diagram is a hand-made drawio exported to PNG — a binary artifact that cannot be edited in-repo — and no authoring convention exists, so agents and humans alike have no guidance for when or how to add diagrams. DocFX 2.78.5 renders Mermaid code fences natively with the modern default template (verified against the pinned tool version and the docs build workflow), so no tooling change is needed.

## Decisions

### D1 — `AGENTS.md` guidance lands first

The "Diagrams" subsection is added to AGENTS.md's Documentation section before any diagram is written. Governance precedes artifacts, so the diagrams that follow are the first application of the rule rather than an exception to it.

### D2 — Mermaid is the only diagram syntax

All diagrams are Mermaid code fences (info string `mermaid`) rendered by DocFX. drawio/PNG are not added going forward. Rationale: text-source diagrams are reviewable in diffs, editable in-repo, and render identically on the docs site and in editors.

### D3 — The overview diagram replaces drawio; the binary artifacts are deleted

`docs/architecture.md` gets a Mermaid system-overview flowchart equivalent in content to `system_overview.drawio` (the two APIs, shared auth, the outbox path, the two stores). `docs/system_overview.drawio` and `docs/system_overview.png` are deleted — the Mermaid source becomes the single source of truth, per the user's decision.

### D4 — Diagrams must earn their place

A diagram is added only where it materially improves understanding of prose on the same page. The sweep adds:

| Page | Diagram | Type |
|---|---|---|
| `docs/architecture.md` | System overview (replacing drawio) | flowchart |
| `docs/architecture.md` | Ingestion → outbox → worker flow | sequenceDiagram |
| `docs/architecture.md` | Event-log lifecycle (Pending / Processed / Failed) | stateDiagram-v2 |
| `docs/architecture.md` | CQRS + ports-and-adapters layering | flowchart |
| `docs/testing.md` | CI pipeline gate order | flowchart |
| `docs/deployment-aws.md` | Deploy + live-verify flow | sequenceDiagram |

Pages without a natural diagram (e.g. `docs/configuration.md`, `docs/debugging.md`) stay text-only — diagrams are not forced.

### D5 — Diagrams illustrate, never introduce facts

A diagram renders facts already stated in prose on the same page; it never introduces a fact absent from the text (one-fact-one-home applies to diagram content too). AGENTS.md guidance states this explicitly.

### D6 — Placement and naming conventions

Diagrams sit directly under the prose they illustrate, each inside a fenced block with the `mermaid` info string. No caption/numbering scheme — the surrounding heading provides context. File names are irrelevant (fences, not image files); only the deleted `.drawio`/`.png` are file-based.

## Affected Files

| File | Change |
|---|---|
| `AGENTS.md` | "Diagrams" subsection in Documentation rules |
| `docs/architecture.md` | Mermaid overview + sequence + state + layering diagrams |
| `docs/testing.md` | CI pipeline diagram |
| `docs/deployment-aws.md` | Deploy + verify sequence diagram |
| `docs/system_overview.drawio` (deleted) | Removed |
| `docs/system_overview.png` (deleted) | Removed |

## Risks

- **Mermaid syntax errors breaking the docs build:** mitigated by running `docfx build` locally as the verification step and by keeping diagrams simple (flowchart/sequence/state only, no exotic features).
- **Diagram/prose drift:** mitigated by D5 — diagrams add no facts of their own, so prose is the source of truth and a diagram that disagrees with prose is visibly wrong in the same page.

## Verification

1. `docfx build` succeeds; the docs site renders every new diagram (mermaid fences render client-side in the default template).
2. No references to `system_overview.*` remain anywhere.
3. `openspec validate --all`.
