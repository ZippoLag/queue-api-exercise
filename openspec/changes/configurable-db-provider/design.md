# Design: Configurable Database Provider

## Problem

`UseSqlite` is hardcoded at every EF Core registration site, so swapping the database engine requires source edits despite `docs/architecture.md` claiming otherwise. The provider must become configuration-driven with SQLite as the only wired implementation, and an explicit extension point for future providers.

## Decisions

### D1 — One shared provider switch lives in a new `QueueApi.Persistence` project

A new class library `src/Shared/QueueApi.Persistence` holds a single static extension:

```
UseConfiguredProvider(this DbContextOptionsBuilder builder, string provider, string connectionString)
```

The switch maps the configured provider name to the EF Core provider call. Today the switch has exactly one branch (`sqlite` → `UseSqlite`); adding PostgreSQL later is one new branch plus the `Npgsql.EntityFrameworkCore.PostgreSQL` package reference — no registration call site changes.

**Why a new project:** the three production registration sites (`CmsWebhook.Infrastructure`, `Users.Infrastructure`, `QueueApi.Auth`) share no common non-auth dependency today (verified in the `.csproj` reference graph). Reusing `QueueApi.Auth` for a persistence helper would force infrastructure projects to reference an auth-named library for a non-auth concern. The helper depends only on `Microsoft.EntityFrameworkCore` (same version the solution already uses) so every consumer adds only the new project reference.

### D2 — `Db:Provider` configuration key, default `sqlite`, fail-fast on unknown values

Each registration site already receives `IConfiguration` to resolve its connection string; it reads `Db:Provider` the same way. Absent value → `sqlite`. The comparison is case-insensitive. An unsupported value throws at startup with a descriptive error naming the supported providers — an unknown provider must never silently fall back, because a deployment pointed at the wrong engine would corrupt its data contract.

### D3 — `EnsureCreated` stays for SQLite; migrations are the documented precondition for a swap

SQLite keeps its current `EnsureCreated` startup schema creation. A real provider swap (e.g. PostgreSQL) additionally requires EF migrations, which is deferred with the Npgsql work — the switch is the precondition that makes that future change a config+package+one-branch change instead of a re-architecture. The design note goes into `docs/architecture.md` so the swap path is honest.

### D4 — AWS deployment needs no functional change

The deployed node stays SQLite, so `scripts/deploy-aws.sh` and the systemd environment are untouched. `docs/deployment-aws.md` gains the `Db__Provider` environment-variable form in its configuration table (documented, defaulting to sqlite, no value required). This is the "update aws deployment if need be" from the request — the answer is: documentation only.

### D5 — Test plumbing stays SQLite and is untouched

The `WebApplicationFactory` hosts, E2E hosts, and test database builders construct `DbContextOptionsBuilder` directly with `UseSqlite` — they are not config-driven and must keep using in-memory/temp SQLite regardless of any runtime provider. No test changes; the existing suites double as the no-behavior-change proof.

### D6 — The `AuthDbInit` tool routes through the same switch

The tool pins the default provider (`sqlite`) via the shared helper rather than calling `UseSqlite` directly, so there is exactly one place in the codebase where provider selection lives. Its CLI contract (connection string argument) is unchanged.

## Affected Files

| File | Change |
|---|---|
| `src/Shared/QueueApi.Persistence/` (new) | Provider-switch extension + project file |
| `src/CmsWebhook/CmsWebhook.Infrastructure/CmsServiceCollectionExtensions.cs` | Read `Db:Provider`, use helper |
| `src/Users/Users.Infrastructure/UsersServiceCollectionExtensions.cs` | Read `Db:Provider`, use helper |
| `src/Shared/QueueApi.Auth/BasicAuthenticationServiceCollectionExtensions.cs` | Read `Db:Provider`, use helper |
| `tools/AuthDbInit/AuthDbInitializer.cs` | Use helper with default provider |
| `docs/configuration.md` | Document `Db:Provider` |
| `docs/deployment-aws.md` | Document `Db__Provider` env form |
| `docs/architecture.md` | Make the provider-swap claim accurate; note migrations precondition |

## Risks

- **Behavior regression under default:** the switch's sqlite branch must call exactly `UseSqlite(connectionString)` as today. Mitigated by the full existing suite (unit, integration, E2E, smoke) all running on sqlite with no expected change.
- **Over-abstraction:** a whole project for one branch could look like ceremony. Mitigated by D1's rationale: there is no existing shared non-auth project, and the switch genuinely centralizes the one decision a provider swap touches. The class is a dozen lines, not a framework.

## Verification

1. `dotnet build` — all projects compile with the new reference graph.
2. Full test run (unit + integration + E2E) — all green on sqlite, proving no behavior change.
3. `bash scripts/smoke-e2e.sh` — real-process vertical still passes.
4. `openspec validate --all`.
