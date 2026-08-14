## Why

The coverage gate added in `add-ci-build-and-test` measures a metric that cannot honestly reach high percentages: it **sums `lines-valid`/`lines-covered` across all six cobertura reports**, but coverlet instruments *every referenced assembly* per test project. Shared code (the `QueueApi.Auth` library, the `CmsWebhook.*` layers) is therefore counted once per referencing report — e.g. the `AuthDbInit.Tests` report alone drags in 112 lines of `BasicAuthenticationHandler`, an ASP.NET middleware the CLI tool can never legitimately run. The gate reads 74.82% today, but only **50 unique lines are genuinely untested**; the rest of the gap is measurement artifact. Raising the threshold to 95.1% on the current metric would require meaningless tests (re-exercising the auth handler through the CLI tool). The gate must measure what it means to protect: **every source line covered by at least one test**.

## What Changes

- **Fix the coverage metric**: `scripts/check-coverage.sh` aggregates cobertura reports by **unique source line** (union across reports, paths normalized), instead of summing per-report counts. A line counts as covered if *any* test project covers it. This is the industry-standard interpretation (SonarQube/Coveralls style) and removes the shared-code double-counting.
- **Raise the threshold** in `.config/coverage-min.txt` — on the corrected metric the baseline is 92.99% (663/713), so the user's initial target of 95.1% is reachable with the meaningful tests below; the measured outcome is deterministic at 100.00% (716/716), so the committed ratchet is **100.0**.
- **Add the missing meaningful tests** for the genuinely-untested logic:
  - `CmsEventProcessorWorker` — sweep error and per-event failure paths (11 lines)
  - `EfCmsEventProcessor` — failure-marking path (4 lines)
  - `EfCmsEventLogRepository` — status-update edge (2 lines)
  - `UserCredential` — property (1 line)
- **Refactor thin entry-point glue for testability** (per explicit requirement to amend untestable code):
  - `tools/AuthDbInit/Program.cs` (17 lines): hoist the arg-parsing → outcome decision logic into a testable class so the CLI entry stays a thin shim.
  - `CmsWebhook.Api/Program.cs` (15 lines): expose the fail-fast branches (missing config, invalid username length, DB unreachable, `IsDevelopment()` Scalar mapping) through the existing `Program` partial-class helpers so `WebApplicationFactory` tests can exercise them with bad config.
- No product behavior changes; no duplicated code to remove (the shared auth library is intentionally reused, not duplicated).

## Capabilities

### New Capabilities

None — this is tooling/CI/tests; it changes no product behavior. Opts out of specs via `skip_specs: true` (see `.openspec.yaml`), matching the `add-ci-build-and-test` precedent.

### Modified Capabilities

None.

## Impact

- **Modified**: `scripts/check-coverage.sh` (aggregation algorithm), `.config/coverage-min.txt` (74.5 → 95.1), `tests/CmsWebhook/CmsWebhook.Infrastructure.Tests/` (worker/processor/repository tests), `tools/AuthDbInit/` (Program refactor), `src/CmsWebhook/CmsWebhook.Api/Program.cs` (helper exposure), `.github/workflows/ci.yml` (unchanged — the gate script is the only change).
- **No source, API, or runtime behavior changes**; no new test dependencies.
- The corrected baseline is measured during implementation so the threshold lands exactly where the honest metric puts it.
