## 1. Shared provider switch

- [ ] 1.1 Create `src/Shared/QueueApi.Persistence` (net9.0 class library referencing the solution's existing EF Core version, XML docs enabled) with the `UseConfiguredProvider(DbContextOptionsBuilder, string provider, string connectionString)` extension: a case-insensitive provider switch with the single `sqlite` branch calling `UseSqlite`, and a descriptive fail-fast error naming the supported providers when the value is unknown
- [ ] 1.2 Add the `QueueApi.Persistence` project reference to `CmsWebhook.Infrastructure`, `Users.Infrastructure`, `QueueApi.Auth`, and `tools/AuthDbInit`

## 2. Wire the registration sites

- [ ] 2.1 `CmsServiceCollectionExtensions`: read `Db:Provider` from `IConfiguration` (default `sqlite`) and register `CmsDbContext` through the helper
- [ ] 2.2 `UsersServiceCollectionExtensions`: same for `UsersDbContext`
- [ ] 2.3 `BasicAuthenticationServiceCollectionExtensions` (QueueApi.Auth): same for `AuthDbContext`
- [ ] 2.4 `tools/AuthDbInit/AuthDbInitializer.cs`: build `AuthDbContext` options through the helper with the default provider; CLI contract unchanged

## 3. Documentation

- [ ] 3.1 `docs/configuration.md`: document `Db:Provider` (default `sqlite`, supported values, position in the precedence chain)
- [ ] 3.2 `docs/deployment-aws.md`: document the `Db__Provider` environment-variable form; note that AWS stays SQLite so no value is required
- [ ] 3.3 `docs/architecture.md`: update the persistence provider-swap claim to match reality (config-selected provider; EF migrations are the precondition for a non-SQLite engine)

## 4. Verification

- [ ] 4.1 `dotnet build` — the whole solution compiles with the new project and references
- [ ] 4.2 Full test run (unit + API integration + E2E) — all green on SQLite, proving no behavior change under the default
- [ ] 4.3 `bash scripts/smoke-e2e.sh` — real-process vertical still passes
- [ ] 4.4 `openspec validate --all`
