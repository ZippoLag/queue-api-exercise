## Why

Every project already emits XML documentation files (`GenerateDocumentationFile` is on in all ten `.csproj` files) and carries high-quality `<summary>`/`<remarks>` comments explaining the what and the why — but there is no browsable API reference; the only docs are hand-written Markdown and the runtime OpenAPI contract. DocFX — the current Microsoft standard (successor to the legacy Sandcastle) — turns those existing XML docs plus the `docs/` Markdown into a single static HTML site, published to GitHub Pages, with no new prose required.

## What Changes

- Add **DocFX** as a local .NET tool via a tool manifest (`.config/dotnet-tools.json`) so generation is reproducible and version-pinned.
- Add a `docfx.json` that:
  - generates the API reference from the `src/**` projects' XML docs (excluding tests and tools, which are internal);
  - includes the existing conceptual Markdown (`docs/*.md`, `README.md`) so the site merges hand-written docs with the API reference.
- Add a **GitHub Pages** deploy (a job in the CI workflow or a dedicated workflow) that builds the site and publishes it from the `main` branch.
- Gitignore the generated `_site/` output.

## Capabilities

### New Capabilities

None — documentation/tooling; no product behavior changes. Opts out of specs via `skip_specs: true` (see `.openspec.yaml`).

### Modified Capabilities

None.

## Impact

- **New files**: `.config/dotnet-tools.json`, `docfx.json`, and the Pages deploy workflow/job.
- **`.gitignore`**: add `_site/`.
- **No source or API changes**; consumes the XML docs already produced by the build.
- **Optional follow-up** (not in this change): convert the `docs/architecture.md` PNG diagram (`system_overview.png`) to Mermaid so diagrams become editable text.
