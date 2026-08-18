## Why

The EF Core database provider is hardcoded as SQLite at every registration site: `CmsServiceCollectionExtensions` (`CmsDbContext`), `UsersServiceCollectionExtensions` (`UsersDbContext`), `BasicAuthenticationServiceCollectionExtensions` (`AuthDbContext`), and the `AuthDbInit` tool. Connection strings are already configurable per environment (`ConnectionStrings:*`), but swapping the database engine currently requires editing source code. `docs/architecture.md` already claims a provider swap happens "without code changes" — that claim is aspirational today. This change makes the code match the documented intent: the provider becomes a configuration value, with SQLite as the only wired implementation and an explicit extension point for additional providers (PostgreSQL first) later.

## What Changes

- New shared project `src/Shared/QueueApi.Persistence` with a single static helper that applies a configured provider to a `DbContextOptionsBuilder`.
- New `Db:Provider` configuration key (default `sqlite`); unknown values fail fast at startup with a descriptive error, making the extension point explicit rather than silently ignored.
- All three production EF registrations (`CmsDbContext`, `UsersDbContext`, `AuthDbContext`) and the `AuthDbInit` tool route through the helper; behavior is unchanged under the default.
- Schema creation stays `EnsureCreated` for SQLite; the design documents that a non-SQLite engine swap additionally requires EF migrations (deferred with the Npgsql work, not built here).
- Docs: `docs/configuration.md` documents `Db:Provider`; `docs/deployment-aws.md` documents the `Db__Provider` environment-variable form (AWS stays SQLite, so no deploy change is required); the `architecture.md` provider-swap claim becomes accurate after implementation.

## Capabilities

### New Capabilities

### Modified Capabilities

- `configuration`: the database-access area gains a provider-selection requirement.

## Impact

- New project: `src/Shared/QueueApi.Persistence`, referenced by `CmsWebhook.Infrastructure`, `Users.Infrastructure`, `QueueApi.Auth`, and `tools/AuthDbInit`.
- Modified files: the three service-collection extension classes and `tools/AuthDbInit/AuthDbInitializer.cs`.
- Docs: `docs/configuration.md`, `docs/deployment-aws.md`, `docs/architecture.md`.
- No behavior change under the default; test plumbing (WebApplicationFactory, E2E hosts, in-memory SQLite) stays SQLite and is untouched.
- AWS: no functional change — documentation only.
