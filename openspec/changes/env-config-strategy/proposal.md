## Why

Configuration is currently ad hoc and fragile: relative SQLite paths in `appsettings.json` are rewritten at startup by a custom walk that hunts for the `QueueApi.slnx` marker, there is no per-environment configuration beyond Development, and there is no explicit strategy for where secrets and connection strings come from. This makes the API awkward to deploy and easy to misconfigure.

## What Changes

- Introduce a clear, documented environment strategy: **Development**, **Staging**, and **Production**, each with its own `appsettings.{Environment}.json`.
- Adopt **.NET user-secrets for local Development** and **environment variables for Staging/Production** (12-factor), replacing committed defaults for anything sensitive.
- Make the SQLite database **base directory an explicit configuration value**, and remove the `FindRepositoryRoot`/`QueueApi.slnx` walk from `Program.cs` so published deployments no longer depend on a repository marker file.
- Update `docs/configuration.md` to describe the precedence chain (appsettings → appsettings.{Environment} → user-secrets (dev) → environment variables) and the per-environment matrix; `docs/architecture.md`'s persistence section is aligned.
- Preserve the existing single-source-of-truth rules (`Auth:CmsUsername`, `ConnectionStrings:*`) — this change only governs *how* values are supplied per environment.

## Capabilities

### New Capabilities

- `configuration`: the environment configuration strategy — per-environment config files, the configuration precedence chain, user-secrets restricted to Development, environment variables for Staging/Production, and an explicitly configurable database base path.

### Modified Capabilities

None — the existing `auth` ("credential store location is configurable") and `cms-webhook-api` ("configured credential format") requirements already describe configurable, env-var-overridable values; this change adds new strategy-level behavior rather than altering those requirements.

## Impact

- **Code**: `src/CmsWebhook/CmsWebhook.Api/Program.cs` — remove `FindRepositoryRoot` and the relative-path rewriting; read the database base path from configuration.
- **Config**: new `appsettings.Staging.json` and `appsettings.Production.json`; `appsettings.json` defaults simplified; user-secrets enabled for Development.
- **Tooling**: `scripts/init-db.sh` and `tools/AuthDbInit` gain the same base-path awareness (the credential store path is also relative today).
- **Tests**: startup/config resolution tests updated to cover the new precedence and base-path behavior.
- **Docs**: `docs/configuration.md`, `docs/architecture.md`, `README.md` (quickstart/deployment guidance).
