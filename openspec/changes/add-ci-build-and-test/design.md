## Context

See proposal.md - Why. There is no `.github/workflows`, no `global.json`, no `Directory.Build.props`, and no tool manifest. Every test project already references `coverlet.collector`. The repository is hosted on GitHub (`origin`), and `openspec validate` is available as a pinned npm CLI.

## Goals / Non-Goals

**Goals:**
- Automatic verification on every push and pull request: restore, build, and the full test suite.
- .NET-native quality gates: analyzer warnings fail the build; a coverage ratchet that never regresses; `openspec validate` in CI.
- One lean job; no third-party quality service (deliberately no SonarCloud/SonarQube — see proposal.md).

**Non-Goals:**
- Packaging, publishing, or deployment.
- A build matrix across operating systems (ubuntu-latest only; .NET is cross-platform so linux is representative).
- Historical quality dashboards — the ratchet threshold file is the durable record.

## Decisions

### 1. Single workflow, single job, ubuntu-latest

`.github/workflows/ci.yml` with one lean job (KISS, matching the repo's style): checkout → setup-dotnet → restore → build → test with coverage → coverage gate → `openspec validate`.

### 2. Pin the SDK with a global.json

Add `global.json` pinning the .NET 9 SDK (`rollForward: latestFeature`) so local and CI builds use the same SDK family. setup-dotnet in CI reads it.

### 3. Warnings-as-errors via Directory.Build.props

A root `Directory.Build.props` sets `TreatWarningsAsErrors=true` for every project (src, tests, tools), so Roslyn analyzer findings and compiler warnings block the build locally and in CI.

- *Alternative:* `-warnaserror` flag only in CI — rejected; developers wouldn't see failures until CI, and the gate should be universal.
- *Verified:* the solution currently builds with **zero warnings** under `TreatWarningsAsErrors=true` (all src, tests, and tools projects), so there is nothing to fix — the gate protects future code only.

### 4. Coverage gate: coverlet + cobertura + a threshold script

`dotnet test --collect:"XPlat Code Coverage"` (cobertura is the collector's default output format, verified). A single test run emits **one cobertura file per test project** (six for this solution), so `scripts/check-coverage.sh` must **aggregate**: glob `TestResults/**/coverage.cobertura.xml`, sum `lines-valid` and `lines-covered` across every file, compute the aggregate line rate, and fail when it is below the value in `.config/coverage-min.txt`. The threshold file is committed and starts at the **measured baseline of 75.5% (1226/1624 lines, measured during review)** — the "coverage ratchet": the gate only gets tighter.

- *Alternative:* coverlet.msbuild inline thresholds — rejected; the number lives in a csproj, less visible and harder to raise deliberately.
- *Alternative:* ReportGenerator dashboards — rejected; adds a dependency for presentation we don't need yet.
- *Alternative:* `dotnet-coverage merge` (Microsoft's coverage tool) instead of a hand-rolled aggregator — noted as a future simplification; the script keeps CI free of extra tooling for now.

### 5. openspec validate as a gate

Install the pinned `@fission-ai/openspec` CLI as a workflow step and run `openspec validate --all` so changes that break the spec discipline fail CI.

## Risks / Trade-offs

- [Warnings-as-errors surfaces latent warnings] → fixed in this change; the workflow proves the build is green.
- [Coverage threshold churn] → ratchet principle: the committed number only rises; the script and a README note document how to raise it deliberately.
- [openspec CLI availability in CI] → pinned version; a registry/network failure fails the workflow visibly rather than silently skipping the gate.

## Open Questions

None — the baseline was measured during review at **75.5% aggregate line coverage (1226/1624)** and is committed as the starting threshold in `.config/coverage-min.txt`.
