## Why

The repository has no continuous integration: every change is built and tested only when a developer remembers to run `dotnet build` / `dotnet test` locally, so regressions can reach `main` silently. There is also no automated code-quality measurement — no coverage gate, no analyzer enforcement — so quality is judged by eye. A GitHub Actions pipeline that builds, runs the full test suite, and enforces quality gates turns verification from a manual habit into an automatic gate.

## What Changes

- Add a GitHub Actions workflow (`.github/workflows/ci.yml`) that runs on every push and every pull request.
- Single lean job on `ubuntu-latest` (matching the project's KISS style): checkout, install the .NET 9 SDK, restore, build, and run `dotnet test` across `QueueApi.slnx`.
- Add **.NET-native quality gates** — the "SonarQube-like" measurement with no third-party service:
  - Treat compiler and analyzer warnings as build failures (`TreatWarningsAsErrors`) so Roslyn code-analysis findings (the .NET SDK ships analyzers by default) block the build.
  - Add a **coverage gate**: collect line coverage with coverlet (already referenced by every test project) and fail the build when coverage drops below a threshold initialized to the current measured baseline — a "coverage ratchet" so the gate never regresses existing coverage and can be raised over time.
  - Run `openspec validate` in the same workflow (the CLI is installed as a step) so changes that break the spec discipline fail CI.
- No packaging, publishing, or deployment — verification only. Deliberately no SonarCloud/SonarQube dependency; the gates are plain .NET tooling so CI stays free of external accounts.

## Capabilities

### New Capabilities

None — this is tooling/CI; it changes no product behavior. Opts out of specs via `skip_specs: true` (see `.openspec.yaml`).

### Modified Capabilities

None.

## Impact

- **New file**: `.github/workflows/ci.yml`.
- **No source, API, or runtime behavior changes**; no new test dependencies (reuses `coverlet.collector` already present). `TreatWarningsAsErrors` may require fixing pre-existing warnings in the build once enabled.
- The starting coverage number is measured during implementation so the gate is set to the real baseline rather than an arbitrary target.
- `openspec` must be installable in CI (e.g. `npm install -g @fission-ai/openspec`); pinned to avoid surprise breakage.
