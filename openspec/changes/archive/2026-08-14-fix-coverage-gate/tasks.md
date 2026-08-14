## 1. Coverage metric fix

- [x] 1.1 Rewrite `scripts/check-coverage.sh` to aggregate unique source lines (union across reports, paths normalized) instead of summing per-report counts; document the normalization table in the script header and fail loudly on unrecognized path prefixes
- [x] 1.2 Verify the corrected metric locally: re-run the full suite, confirm the script reports ~92.99% (663/713) and that shared-auth lines covered by `QueueApi.Auth.Tests`/`Api.Tests` are no longer counted as uncovered in `AuthDbInit.Tests`

## 2. Refactor entry-point glue for testability

- [x] 2.1 Hoist `tools/AuthDbInit/Program.cs` arg-parsing → outcome logic into a testable class (thin top-level shim only); XML-documented, per codebase conventions
- [x] 2.2 Add unit tests for the hoisted `AuthDbInit` CLI logic (usage error, relative vs absolute db path, already-exists vs created outcome)
- [x] 2.3 Confirm `CmsWebhook.Api/Program.cs` fail-fast branches (missing `ConnectionStrings:AuthDb`/`CmsDb`, invalid `Auth:CmsUsername` length, unreachable CMS DB) are exercised by tests; `ResolveConnectionString`/`FindRepositoryRoot` made `internal` (+ `InternalsVisibleTo`, per the Infrastructure convention) with unit coverage for missing-key throw / absolute / `:memory:` / repo-root-relative / no-repo-root warning branches — the missing-key case is unit-tested directly because host `ConfigureAppConfiguration` overrides run after the top-level config reads and cannot remove an appsettings key

## 3. Meaningful tests for genuinely-untested logic

- [x] 3.1 `CmsEventProcessorWorker`: tests for the sweep-failure retry path and the per-event failure continuation (mocked scope factory)
- [x] 3.2 `EfCmsEventProcessor`: extend failure tests to the inner `MarkFailedAsync`-fails branch
- [x] 3.3 `EfCmsEventLogRepository`: close the remaining `UpdateStatusAsync` lines
- [x] 3.4 `UserCredential`: cover the remaining property line

## 4. Gate threshold and verification

- [x] 4.1 Raise `.config/coverage-min.txt` to 95.1 (or the achieved value rounded down if measurement lands below) in the same commit as the tests — already at 95.1 (user-set); final measured value is 100.00% (716/716). After the deterministic runs confirmed the metric is stable, the ratchet was raised to **100.0** (user request) — any uncovered line now fails CI
- [x] 4.2 Run the full suite + gate locally across repeated runs to confirm the metric is deterministic and the gate is green — 3 consecutive fresh runs all report exactly 716/716 (100.00%); the flaky async-worker measurement is gone
- [x] 4.3 Run `openspec validate --all` to confirm the change artifacts are valid — 11/11 passed

## 5. Documentation

- [x] 5.1 Update `docs/development-style.md` (coverage gate now measures unique lines; note the normalization) and `README.md` (CI section wording) if the metric description appears there
