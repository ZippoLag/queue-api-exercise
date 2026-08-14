## Context

See proposal.md - Why. All ten `.csproj` files emit XML documentation (`GenerateDocumentationFile`), and the XML comments already carry `<summary>`/`<remarks>` with the what and the why. `docs/*.md`, `README.md`, and `openspec/specs` are the canonical hand-written sources. The repository is hosted on GitHub (origin), there is no tool manifest yet, and a stale, untracked `_site/` directory (empty folders) sits at the root.

## Goals / Non-Goals

**Goals:**
- A version-pinned DocFX build that merges the XML API reference with the conceptual Markdown into one static site.
- Deploy the site to GitHub Pages from `main`.
- Keep `docs/*.md` and the OpenSpec specs as the canonical, agent-facing sources — the site is generated from them, never hand-edited.

**Non-Goals:**
- Writing new prose (the XML comments already exist).
- Converting the `docs/architecture.md` PNG diagram to Mermaid (optional follow-up, not in this change).
- Multi-version documentation sets.

## Decisions

### 1. DocFX as a local .NET tool

`.config/dotnet-tools.json` pins the DocFX version so local and CI builds are reproducible.

### 2. docfx.json layout

- Metadata: build the API reference from `src/**/*.csproj` (the four CmsWebhook projects + `Shared/QueueApi.Auth`). The glob is scoped to `src/`, so `tests/**` and `tools/**` are excluded structurally (verified: every `src/` project emits XML docs; the only project without `GenerateDocumentationFile` is `tools/AuthDbInit`, which is out of scope). Metadata output goes to `api/` (the default; a DocFX intermediate that must be gitignored).
- Conceptual: include `docs/**/*.md` and `README.md` so the hand-written docs merge with the API reference; exclude `docs/archived/**` (historical material) from the site.
- Resource: add `docs/**/*.png` and `docs/**/*.drawio` so images referenced by the Markdown (e.g. `system_overview.png`) are copied into the site — DocFX only copies files matched by `content` or `resource` globs.
- Output: `_site/`. Default markdown processor (markdig) is fine. Pin **DocFX 2.78.5** (current) in the tool manifest.

### 3. Dedicated deploy workflow

A separate `.github/workflows/docs.yml` (triggered on `main`) builds the site and publishes `_site/` to GitHub Pages — decoupled from the CI workflow added by `add-ci-build-and-test`, so the two changes don't edit the same file.

- Steps: checkout → setup-dotnet → `dotnet tool restore` (from the manifest) → `dotnet docfx build` → publish. DocFX needs no extra install beyond the pinned manifest.
- Publish via the modern GitHub Actions Pages flow: `actions/configure-pages` → `actions/upload-pages-artifact` (path `_site`) → `actions/deploy-pages`, with permissions `contents: read`, `pages: write`, `id-token: write`, and a `github-pages` environment.
- **One-time repository setting (cannot be done from a workflow):** the repo's Pages source must be set to "GitHub Actions". Site will live at `https://ZippoLag.github.io/queue-api-exercise/`.

### 4. Output hygiene

DocFX writes two intermediate outputs next to `docfx.json`: `_site/` (the site) and `api/` (the metadata manifest). Add **both** to `.gitignore`, and remove the stale untracked `_site/` and empty `api/` directories at the repository root (leftovers from a prior DocFX attempt).

### 5. Agent-facing sources stay canonical

The site is a rendered view. `docs/*.md` and `openspec/specs` remain the source of truth for both humans (linked from the site) and agents (read directly). A docs index note makes this explicit.

## Risks / Trade-offs

- [GitHub Pages hosting] → resolved: the repository is public, so Pages is free; the site is publicly visible, consistent with the public code.
- [One-time Pages source setting] → the workflow cannot set it; documented in the tasks and README so the first deploy fails visibly until set.
- [DocFX metadata compatibility with .NET 9 SDK projects] → pin the current DocFX (2.78.5); if metadata generation trips on the SDK, fall back to the `dotnet/docfx-action` or pin an even newer release.
- [DocFX version drift] → pinned tool manifest.
- [Site drift from the canonical markdown] → generated from canonical sources only, regenerated in CI on every `main` push.

## Open Questions

None — the hosting decision is settled: the repository is **public** (`github.com/ZippoLag/queue-api-exercise`), so GitHub Pages is available free of charge. The site will be publicly visible, which is consistent with the codebase already being public.
