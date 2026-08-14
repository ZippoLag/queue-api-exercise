# Configuration

The API follows the standard .NET 12-factor configuration chain, provider-neutral: sensitive values are never committed, and each environment supplies them through the appropriate channel.

## Environments

Three environments are supported, each with its own `appsettings.{Environment}.json` layered on top of the shared base file:

| Environment | File | Purpose |
|---|---|---|
| Development | `appsettings.Development.json` | local development |
| Staging | `appsettings.Staging.json` | pre-production validation |
| Production | `appsettings.Production.json` | production |

The environment is selected with the standard `ASPNETCORE_ENVIRONMENT` variable (defaults to `Production` when unset).

## Precedence chain

Values resolve in this fixed order — later sources override earlier ones:

1. `appsettings.json` — committed shared defaults (non-sensitive only).
2. `appsettings.{Environment}.json` — committed per-environment overrides (non-sensitive only).
3. **User-secrets** — Development only; ignored in Staging and Production.
4. **Environment variables** — the channel for sensitive values in Staging/Production (and any ad-hoc override in Development).

The double-underscore convention maps environment variables to configuration keys:

| Environment variable | Configuration key |
|---|---|
| `ConnectionStrings__AuthDb` | `ConnectionStrings:AuthDb` |
| `ConnectionStrings__CmsDb` | `ConnectionStrings:CmsDb` |
| `Auth__CmsUsername` | `Auth:CmsUsername` |
| `Auth__AdministratorUsername` | `Auth:AdministratorUsername` (Users API) |
| `Data__DbBasePath` | `Data:DbBasePath` |

## Secrets guidance

- **Never commit connection strings, passwords, or other secrets.** The committed `appsettings*.json` files contain only non-sensitive defaults (logging levels, etc.).
- **Development:** use user-secrets (`dotnet user-secrets set "ConnectionStrings:AuthDb" "Data Source=..."` from the API project — `CmsWebhook.Api` carries a `UserSecretsId` so the provider is wired automatically). Secrets stay out of the tree.
- **Staging/Production:** supply secrets as environment variables, e.g. `ConnectionStrings__CmsDb="Data Source=/var/queue/cms.db"`.
- The chain is provider-neutral by design: an external secret store (AWS Secrets Manager, SSM Parameter Store, etc.) can be added later as another provider without rework.

## Database base directory

Relative `Data Source=` values in the connection strings resolve against **`Data:DbBasePath`** when configured; when unset they fall back to the application's **content root** (for the web project, its own directory — `src/CmsWebhook/CmsWebhook.Api/`). Absolute and `:memory:` data sources are used as-is. The resolved directory is created automatically at startup when missing, so a fresh checkout or deployment needs no pre-existing `db/` folder. There is **no repository-marker walk**: deployments are not tied to a solution file anywhere on disk.

In local development the stores land at:

- `src/CmsWebhook/CmsWebhook.Api/db/queue-auth.db` — the shared credential store
- `src/CmsWebhook/CmsWebhook.Api/db/queue-cms.db` — the CMS event database

Both APIs address these same two files: the CmsWebhook project's content root is its own directory, and the
Users API sets its `Data:DbBasePath` to `../CmsWebhook/CmsWebhook.Api` (relative to its own content root).

Both are gitignored (`**/db/*.db*`) and are throwaway data — delete them and re-run the init script to start over.

Deployments point the stores elsewhere with environment variables, e.g.:

```bash
export Data__DbBasePath=/var/lib/queue-api
export ConnectionStrings__AuthDb="Data Source=/var/lib/queue-api/auth.db"
export ConnectionStrings__CmsDb="Data Source=/var/lib/queue-api/cms.db;Default Timeout=30"
```

## Credential store

Credentials live in the SQLite credential store (`db/queue-auth.db` by default — see above), provisioned idempotently by `scripts/init-db.sh`. The script's default `DB_PATH` already matches the APIs' own resolution (the CmsWebhook project's `db/` directory); override it with `DB_PATH` or point `ConnectionStrings:AuthDb` elsewhere and pass the absolute path.

- The script seeds the three reserved users `cms-webhook`, `administrator` and `regular-user`, taking the three passwords as positional arguments: `scripts/init-db.sh [cms-password] [admin-password] [regular-password]`. Re-running over an already-seeded store leaves existing users unchanged (idempotent).
- The store location is configurable via `ConnectionStrings:AuthDb` (e.g. the `ConnectionStrings__AuthDb` environment variable).
- The reserved cms username via `Auth:CmsUsername` (e.g. the `Auth__CmsUsername` environment variable); the Users API's administrator username via `Auth:AdministratorUsername` (e.g. `Auth__AdministratorUsername`).
- To change a seeded user's password, delete `db/queue-auth.db` and re-run the script (re-running with a different password leaves the existing user unchanged).
- The local-development default passwords used by `scripts/init-db.sh` are `0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61` (cms), `a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d` (administrator) and `6d5c4b3a-2f1e-4d0c-9b8a-7f6e5d4c3b2a` (regular) — DO NOT use them outside local development.

## CMS event database

The CMS event database (`db/queue-cms.db` by default, configurable via `ConnectionStrings:CmsDb`) is created automatically at startup — no init step is needed beyond the credential store above.

## TLS requirement

Basic authentication transmits credentials as base64, which is *not* encryption. Production deployments of `CmsWebhook.Api` MUST serve over TLS (HTTPS); the plain-http profile in `launchSettings.json` is for local development only.
