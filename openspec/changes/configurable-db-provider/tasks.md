## 1. Shared provider switch

- [x] 1.1 Create `src/Shared/QueueApi.Persistence` (net9.0 class library referencing the solution's existing EF Core version, XML docs enabled) with the `UseConfiguredProvider(DbContextOptionsBuilder, string provider, string connectionString)` extension: a case-insensitive provider switch with the single `sqlite` branch calling `UseSqlite`, and a descriptive fail-fast error naming the supported providers when the value is unknown
- [x] 1.2 Add the `QueueApi.Persistence` project reference to `CmsWebhook.Infrastructure`, `Users.Infrastructure`, `QueueApi.Auth`, and `tools/AuthDbInit`
- [ ] 1.3 Create `tests/Shared/QueueApi.Persistence.Tests` mirroring the `QueueApi.Auth.Tests` convention (xUnit, Moq, FluentAssertions, coverlet.collector, XML docs) referencing the new project, and add it to `QueueApi.slnx`
- [x] 1.4 Write the fail-fast unit test in the new test project: `UseConfiguredProvider` with an unknown provider value (e.g. `postgres`) throws with a descriptive error naming the supported providers — this is the one branch no boot-time integration test exercises, and the 100% unique-line ratchet requires it covered

## 2. Wire the registration sites

- [x] 2.1 `CmsServiceCollectionExtensions`: read `Db:Provider` from `IConfiguration` (default `sqlite`) and register `CmsDbContext` through the helper
- [x] 2.2 `UsersServiceCollectionExtensions`: same for `UsersDbContext`
- [x] 2.3 `BasicAuthenticationServiceCollectionExtensions` (QueueApi.Auth): same for `AuthDbContext`
- [x] 2.4 `tools/AuthDbInit/AuthDbInitializer.cs`: build `AuthDbContext` options through the helper with the default provider; CLI contract unchanged

## 3. Documentation

- [x] 3.1 `docs/configuration.md`: document `Db:Provider` (default `sqlite`, supported values, position in the precedence chain)
- [x] 3.2 `docs/deployment-aws.md`: document the `Db__Provider` environment-variable form; note that AWS stays SQLite so no value is required
- [x] 3.3 `docs/architecture.md`: update the persistence provider-swap claim to match reality (config-selected provider; EF migrations are the precondition for a non-SQLite engine)

## 4. Verification

- [x] 4.1 Update `scripts/check-coverage.sh` path normalization: add the `Shared/QueueApi.Persistence/` prefix rule (mirroring the existing `Shared/QueueApi.Auth/` rule) plus the bare-filename mapping for the new extension class, so the ratchet's union aggregates the new project instead of failing loudly on an unseen prefix
- [ ] 4.2 `dotnet build` — the whole solution compiles with the new project and references
- [ ] 4.3 Full test run (unit + API integration + E2E) — all green on SQLite, proving no behavior change under the default
- [ ] 4.4 `bash scripts/check-coverage.sh` — the 100% unique-line union holds (the new project's `sqlite` branch is covered by every boot; the fail-fast branch by task 1.4)
- [ ] 4.5 `bash scripts/smoke-e2e.sh` — real-process vertical still passes
- [ ] 4.6 `openspec validate --all`
