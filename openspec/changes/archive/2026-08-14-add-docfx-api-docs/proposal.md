## Why

Every project already emits XML documentation files (`GenerateDocumentationFile` is on in all ten `.csproj` files) and carries high-quality `<summary>`/`<remarks>` comments explaining the what and the why — but there is no browsable API reference; the only docs are hand-written Markdown and the runtime OpenAPI contract. DocFX — the current Microsoft standard (successor to the legacy Sandcastle) — turns those existing XML docs plus the `docs/` Markdown into a single static HTML site, with no new prose required.

This also serves both halves of the "documentation useful for human *and* agentic developers" goal: humans get a browsable site, while agents keep the canonical Markdown (`docs/*.md`, `openspec/specs`) as their machine-readable source of truth. The DocFX site is *generated from* those sources — never a parallel, hand-maintained copy — so humans and agents always read the same content.

## What Changes

- Add **DocFX** as a local .NET tool via a tool manifest (`.config/dotnet-tools.json`) so generation is reproducible and version-pinned.
- Add a `docfx.json` that:
  - generates the API reference from the `src/**` projects' XML docs (excluding tests and tools, which are internal);
  - includes the existing conceptual Markdown (`docs/*.md`, `README.md`) so the site merges hand-written docs with the API reference.
- Add a **GitHub Pages** deploy (a job in the CI workflow or a dedicated workflow) that builds the site and publishes it from the `main` branch.
- Gitignore the generated `_site/` output and remove the stale, untracked `_site/` directory currently at the repository root.
- Keep `docs/*.md` and the OpenSpec specs as the canonical documentation sources; the site is a rendered view, not a second copy.

## Capabilities

### New Capabilities

None — documentation/tooling; no product behavior changes. Opts out of specs via `skip_specs: true` (see `.openspec.yaml`).

### Modified Capabilities

None.

## Impact

- **New files**: `.config/dotnet-tools.json`, `docfx.json`, and the Pages deploy workflow/job.
- **`.gitignore`**: add `_site/`.
- **No source or API changes**; consumes the XML docs already produced by the build.
- **Deployment caveat**: GitHub Pages serves the site publicly when the repository is public; a private repository requires a paid GitHub plan or an alternate host — the exact choice is confirmed at implementation time (default: Pages on `main`).
- **Optional follow-up** (not in this change): convert the `docs/architecture.md` PNG diagram (`system_overview.png`) to Mermaid so diagrams become editable text.
