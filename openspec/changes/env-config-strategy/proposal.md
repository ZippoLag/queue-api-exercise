## Why

Configuration is currently ad hoc and fragile: relative SQLite paths in `appsettings.json` are rewritten at startup by a custom walk that hunts for the `QueueApi.slnx` marker, there is no per-environment configuration beyond Development, and there is no documented strategy for where secrets and connection strings come from. This makes the API awkward to deploy and easy to misconfigure — and deployment is now on the horizon (AWS credits available), so the config story must be professional and provider-neutral before that happens.

## What Changes

- Introduce a clear, documented environment strategy: **Development**, **Staging**, and **Production**, each with its own `appsettings.{Environment}.json`.
- Adopt the standard .NET 12-factor precedence chain, provider-neutral: `appsettings.json` → `appsettings.{Environment}.json` → **user-secrets** (Development only) → **environment variables** (Staging/Production). Sensitive values are never committed defaults.
- Make the SQLite database **base directory an explicit configuration value** (e.g. `Data:DbBasePath`), and remove the `FindRepositoryRoot`/`QueueApi.slnx` walk from `Program.cs` so published deployments no longer depend on a repository marker file.
- Extend the same base-path awareness to `scripts/init-db.sh` and `tools/AuthDbInit` (the credential store path is also relative today).
- Update `docs/configuration.md` to describe the precedence chain and the per-environment matrix; align `docs/architecture.md`'s persistence section and `README.md` quickstart/deployment guidance.
- Preserve the existing single-source-of-truth rules (`Auth:CmsUsername`, `ConnectionStrings:*`) — this change only governs *how* values are supplied per environment.
- **Non-goal / deferred**: provider-specific secret stores (AWS Secrets Manager, SSM Parameter Store) are deliberately out of scope — the strategy stays provider-neutral so a provider source can be added later without rework.

## Capabilities

### New Capabilities

- `configuration`: the environment configuration strategy — per-environment config files, the configuration precedence chain, user-secrets restricted to Development, environment variables for Staging/Production, and an explicitly configurable database base path.

### Modified Capabilities

None — the existing `auth` ("credential store location is configurable") and `cms-webhook-api` ("configured credential format") requirements already describe configurable, env-var-overridable values; this change adds new strategy-level behavior rather than altering those requirements.

## Impact

- **Code**: `src/CmsWebhook/CmsWebhook.Api/Program.cs` — remove `FindRepositoryRoot` and the relative-path rewriting; read the database base path from configuration.
- **Config**: new `appsettings.Staging.json` and `appsettings.Production.json`; `appsettings.json` defaults simplified; user-secrets enabled for Development.
- **Tooling**: `scripts/init-db.sh` and `tools/AuthDbInit` gain the same base-path awareness.
- **Tests**: startup/config resolution tests updated to cover the new precedence and base-path behavior.
- **Docs**: `docs/configuration.md`, `docs/architecture.md`, `README.md`.
