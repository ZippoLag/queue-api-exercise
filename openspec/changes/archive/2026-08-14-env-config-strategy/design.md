## Context

See proposal.md - Why. Today `Program.cs` resolves relative SQLite paths by walking up from the content root hunting for the `QueueApi.slnx` marker (`FindRepositoryRoot`), only the Development environment has a config file, and secrets have no defined home. The integration tests pass absolute temporary database paths, so base-path resolution changes do not affect them. There is no `global.json` or `Directory.Build.props` in the repository.

## Goals / Non-Goals

**Goals:**
- Provider-neutral, 12-factor configuration using stock .NET configuration providers only.
- An explicit, documented database base directory (`Data:DbBasePath`) with the repository-marker walk removed.
- Per-environment files for Development, Staging, and Production; secrets supplied via user-secrets (dev) and environment variables (staging/prod).

**Non-Goals:**
- AWS Secrets Manager / SSM Parameter Store integration (explicitly deferred; the chain stays provider-neutral).
- Non-SQLite engines or changing the `ConnectionStrings:*` keys / `Auth:CmsUsername` semantics.
- Encrypted configuration.

## Decisions

### 1. Stock .NET configuration providers; no custom source

`WebApplication.CreateBuilder` already layers `appsettings.json` → `appsettings.{Environment}.json` → user-secrets (Development only) → environment variables, which is exactly the 12-factor chain we want. Ensure the API project carries a `UserSecretsId` so user-secrets are active in Development, and document the double-underscore environment-variable convention (`ConnectionStrings__CmsDb`).

- *Alternative:* a custom `IConfigurationSource` — rejected; more code to maintain for zero benefit over the built-in chain.

### 2. `Data:DbBasePath` with a content-root default

New configuration key. Relative data sources resolve against it; when unset they resolve against the content root. `FindRepositoryRoot` and the marker walk are deleted.

- *Alternative:* keep the marker walk for local dev — rejected; that is precisely the fragility this change removes.
- *Alternative:* require absolute paths everywhere — rejected; poor local-dev experience.
- *Verified behavior:* for web projects, `dotnet run --project ...` and IDE launches set the working directory — and therefore the content root — to the **project directory** (verified empirically with a minimal web app: `CWD == ContentRoot ==` the project dir). The repository-marker walk is what currently relocates relative `db/queue-*.db` paths up to the repository root.
- *Consequence:* after the change, local development databases land in the API project's `db/` directory (`src/CmsWebhook/CmsWebhook.Api/db/queue-*.db`), not the repository root. `scripts/init-db.sh`'s default `DB_PATH` moves to match (it already points at the repository root today), and `.gitignore` covers project-local `db/` files. Deployment points `Data__DbBasePath` (or `ConnectionStrings__*`) elsewhere via environment variables. The old repo-root `db/*.db` dev stores are gitignored throwaway data — copy them or re-run the init script.

### 3. The app creates the database directory

SQLite cannot create a database file in a directory that does not exist, and today the app never creates directories — it relies on the committed repo-root `db/` directory (`.gitkeep`). With the walk gone, the resolved directory (`src/CmsWebhook/CmsWebhook.Api/db/`) does not exist on a fresh checkout, so startup MUST create it (`Directory.CreateDirectory`) for each resolved relative store before opening it — matching the documented promise that the CMS database is "created automatically at startup". The credential store's directory is also covered by `scripts/init-db.sh` (which already `mkdir -p`s), but creating it in the app keeps the resolution self-contained.

- *Alternative:* document "create the directory yourself" — rejected; the CMS DB is already documented as auto-created, and a fresh checkout would otherwise fail on first run.

### 4. Environment files are committed; secrets are not

`appsettings.Staging.json` / `appsettings.Production.json` contain only non-sensitive defaults (logging levels, etc.). Connection strings and other secrets for those environments come from environment variables.

### 5. Tooling alignment

`tools/AuthDbInit` already takes an explicit database path argument. `scripts/init-db.sh` computes the default from the repository root; its default `DB_PATH` moves to the API project's `db/` directory so the documented "run from the repo root" flow lines up with the new resolution. `.gitignore` switches from `db/*.db` to `**/db/*.db*` so project-local database files stay ignored.

## Risks / Trade-offs

- [Local dev database location changes (project dir instead of repo root)] → `init-db.sh`'s default moves with it and `.gitignore` covers project-local `db/` files; dev stores are throwaway.
- [Fresh checkout has no `db/` directory] → mitigated by directory creation at startup (decision 3).
- [Relative `Data:DbBasePath` confusion] → document that relative base paths resolve against the content root.
- [Deployment misconfiguration] → the existing fail-fast startup errors for missing connection strings are preserved; a relative data source with no base path and no content root fails visibly.

## Migration Plan

1. Update `scripts/init-db.sh` default path; re-run it so the credential store is seeded at the new location.
2. Copy or ignore the old repo-root `db/*.db` files (dev-only data).
3. Deployments set `Data__DbBasePath` and/or `ConnectionStrings__*` via environment variables.

## Open Questions

None — the approach, specs, and task breakdown are settled. Exact file names for the new environment files are fixed by the spec (`appsettings.{Environment}.json`).
