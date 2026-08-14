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

Both are gitignored (`**/db/*.db*`) and are throwaway data — delete them and re-run the init script to start over.

Deployments point the stores elsewhere with environment variables, e.g.:

```bash
export Data__DbBasePath=/var/lib/queue-api
export ConnectionStrings__AuthDb="Data Source=/var/lib/queue-api/auth.db"
export ConnectionStrings__CmsDb="Data Source=/var/lib/queue-api/cms.db;Default Timeout=30"
```

## Credential store

Credentials live in the SQLite credential store (`db/queue-auth.db` by default — see above), provisioned idempotently by `scripts/init-db.sh`. The script's default `DB_PATH` already matches the API's own resolution (the API project's `db/` directory); override it with `DB_PATH` or point `ConnectionStrings:AuthDb` elsewhere and pass the absolute path.

- The store location is configurable via `ConnectionStrings:AuthDb` (e.g. the `ConnectionStrings__AuthDb` environment variable).
- The reserved cms username via `Auth:CmsUsername` (e.g. the `Auth__CmsUsername` environment variable).
- To change a seeded user's password, delete `db/queue-auth.db` and re-run the script (re-running with a different password leaves the existing user unchanged).
- The local-development default password used by `scripts/init-db.sh` is `0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61` — DO NOT use it outside local development.

## CMS event database

The CMS event database (`db/queue-cms.db` by default, configurable via `ConnectionStrings:CmsDb`) is created automatically at startup — no init step is needed beyond the credential store above.

## TLS requirement

Basic authentication transmits credentials as base64, which is *not* encryption. Production deployments of `CmsWebhook.Api` MUST serve over TLS (HTTPS); the plain-http profile in `launchSettings.json` is for local development only.
