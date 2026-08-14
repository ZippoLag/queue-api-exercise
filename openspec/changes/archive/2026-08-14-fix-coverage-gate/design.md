## Context

See proposal.md - Why. The gate from `add-ci-build-and-test` sums `lines-valid`/`lines-covered` across the six cobertura reports (`scripts/check-coverage.sh`), which double-counts shared assemblies: coverlet instruments every referenced assembly per test project, so e.g. `AuthDbInit.Tests` reports all 112 lines of `BasicAuthenticationHandler` (an ASP.NET middleware) as uncovered even though `QueueApi.Auth.Tests` and `CmsWebhook.Api.Tests` cover them. Measured on the current metric: 1215/1624 = 74.82%. Measured as unique lines across all reports (paths normalized): 663/713 = 92.99%, with only 50 genuinely-untested unique lines.

## Goals / Non-Goals

**Goals:**
- A coverage gate that measures the meaningful quantity: *each source line is covered by at least one test* (union across reports), matching how SonarQube/Coveralls report solution coverage.
- Reach the user's 95.1% target honestly — ~15 meaningful new tests on the corrected metric.
- Refactor the two thin entry points (`tools/AuthDbInit/Program.cs`, `CmsWebhook.Api/Program.cs` fail-fast branches) so previously-untestable glue becomes testable.
- Keep the ratchet semantics: the committed threshold only rises.

**Non-Goals:**
- Covering `BasicAuthenticationHandler` and other shared-auth lines from the `AuthDbInit.Tests` project (meaningless — a CLI tool must not re-test ASP.NET middleware).
- Changing product behavior or public APIs.
- Adding coverage tooling dependencies (ReportGenerator, dotnet-coverage) — the script stays self-contained, consistent with the `add-ci-build-and-test` design decision 4.
- Achieving 100%: a few lines of defensive/entry-point glue may remain uncovered by design (e.g. the `IsDevelopment()` Scalar branch cannot run in test's non-dev environment) — see Risks.

## Decisions

### 1. Unique-line (union) aggregation in `scripts/check-coverage.sh`

The script changes from summing per-report counts to: collect every `(normalized-path:line)` from every report; a line is **covered** if `hits > 0` in *any* report; the ratio is `unique-covered / unique-valid` across all reports. Paths must be normalized because the six reports emit different prefixes for the same source file (e.g. `CmsRequestValidator.cs` vs `CmsWebhook.Domain/CmsRequestValidator.cs` vs `src/CmsWebhook/CmsWebhook.Domain/CmsRequestValidator.cs`).

- *Alternative:* per-report filters (coverlet `<Include>` restricting each report to its own assembly) — rejected; Api.Tests legitimately covers the whole stack through `WebApplicationFactory`, and excluding cross-layer coverage would *lower* the honest number and lose integration-test credit.
- *Alternative:* `dotnet-coverage merge` — rejected for now; same rationale as the sibling change (keep CI tooling minimal), and the union logic is ~30 lines of awk.

**Path normalization rule**: strip known prefixes (`./`, `src/`, `CmsWebhook/`, `Shared/`, and the project-name prefixes emitted by each report) to a canonical repo-relative path; fall back to bare filename + line when the prefix is unrecognized. The normalization table is derived from the actual reports and documented in the script header.

### 2. `tools/AuthDbInit/Program.cs` → extract decision logic into a testable class

The 17 untested lines are top-level statements (arg parsing, connection-string shaping, outcome messaging). Introduce `AuthDbInitializer.RunAsync`-adjacent logic: a small static method (e.g. `Cli.RunAsync(string[] args, TextWriter stdout, TextWriter stderr)` returning the exit code) so the top-level `Program.cs` becomes a two-line shim (`return await Cli.RunAsync(args, Console.Out, Console.Error);`). The existing `AuthDbInitializer.InitializeAsync` stays the storage seam.

- *Alternative:* `[ExcludeFromCodeCoverage]` on the top-level shim — rejected; the logic is genuinely testable once hoisted, and covering it is more honest than excluding it.
- The `Program.cs` shim itself may keep 1–2 uncovered lines (top-level statements are not directly unit-testable); in the end it is covered too: a test invokes the assembly's `EntryPoint` with valid args (the compiler may elide the async state machine, so the test accepts both a synchronous `int` and a `Task<int>` return), proving the arg plumbing end to end.

### 3. `CmsWebhook.Api/Program.cs` → expose fail-fast branches to `WebApplicationFactory` tests

The untested lines are the startup guards: missing `ConnectionStrings:AuthDb`/`CmsDb`, invalid `Auth:CmsUsername` length, unreachable CMS DB, and the `IsDevelopment()` Scalar mapping. The first three are already reachable via `WebApplicationFactory<Program>` with overridden configuration (the auth tests already use `CaptureStartupFailure`); the missing piece is **config-driven variants** of those tests plus a `ResolveConnectionString` unit test for the repo-root/absolute/`:memory:` branches. No code change is needed for these — only tests. The `IsDevelopment()` branch cannot be exercised in the test host's `Development` default (the branch *is* active in Development, so integration tests actually do cover it — verify during implementation; if not, it is the one line of glue we may exclude or accept).

### 4. New tests for the genuinely-untested logic

- `CmsEventProcessorWorker` (11 lines): sweep-failure retry path and per-event failure continuation via a mocked scope factory that throws once, then succeeds.
- `EfCmsEventProcessor` (4 lines): failure-marking path already partially covered by `Process_FailingEvent_IsMarkedFailedAndProcessingContinues` — extend for the inner `MarkFailedAsync`-fails branch.
- `EfCmsEventLogRepository` (2 lines): `UpdateStatusAsync` edge (already covered at 56/60 — close the remaining lines).
- `UserCredential` (1 line): property accessor.

All new tests follow the existing conventions: xUnit + Moq + FluentAssertions, XML `<summary>`/`<remarks>` citing the business rule, no inline comments beyond what a logging statement conveys.

### 5. Threshold set to the honest measured value

`/config/coverage-min.txt` moves to **95.1** once the corrected metric plus new tests land; if the measured value is slightly above/below 95.1 after implementation, the committed number is the achieved value rounded down (ratchet semantics: never commit a number the current tree cannot pass). In the end the measured rate is deterministic at 100.00% (716/716), so the committed ratchet was raised to **100.0** — a line added without a test now fails CI, which is exactly what the ratchet is for.

## Risks / Trade-offs

- [Path normalization is brittle if a future report emits an unseen prefix] → the script fails loudly with the offending path listed, and the normalization table lives in the script header for easy extension; CI proves it on every run.
- [Top-level `Program.cs` shims keep 1–2 uncovered lines each] → resolved during implementation: the `AuthDbInit` shim is covered via an `EntryPoint` invocation test, and the Api entry-point glue is exercised through `WebApplicationFactory`; the final measured rate is 100.00% (716/716).
- [`IsDevelopment()` Scalar branch may be uncoverable in tests] → the integration test host runs in Development by default, so it should be covered; if not, accept the single line as a documented exclusion rather than contorting the test host.
- [Raising the threshold to 100.0% makes the gate sensitive to new code] → that is the point of the ratchet (every new line needs its test); the README's "how to raise the threshold" section already documents the deliberate-raise workflow, and new feature changes will add their own tests.

## Migration Plan

No deployment/migration — this is CI tooling and tests. Rollback = revert the commit (the threshold file and script are the only gate behavior). The gate goes red during implementation until the threshold is raised together with the tests; land the script change and tests in the same commit as the threshold bump.

## Open Questions

None — the union metric, the 50-line gap, and the achievable ~15 test lines were measured during exploration (see proposal.md).
