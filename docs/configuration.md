# Configuration

## Credentials & configuration
Credentials live in the SQLite credential store at `db/queue-auth.db` (gitignored, provisioned by `scripts/init-db.sh`), not in environment variables.

- The store location is configurable via `ConnectionStrings:AuthDb` (e.g. the `ConnectionStrings__AuthDb` environment variable).
- The reserved cms username via `Auth:CmsUsername` (e.g. the `Auth__CmsUsername` environment variable).
- To change a seeded user's password, delete `db/queue-auth.db` and re-run the script (re-running with a different password leaves the existing user unchanged).
- The local-development default password used by `scripts/init-db.sh` is `0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61` — DO NOT use it outside local development.

## CMS event database
The CMS event database (`db/queue-cms.db`, configurable via `ConnectionStrings:CmsDb`) is created automatically at startup — no init step is needed beyond the credential store above.

## TLS requirement
Basic authentication transmits credentials as base64, which is *not* encryption. Production deployments of `CmsWebhook.Api` MUST serve over TLS (HTTPS); the plain-http profile in `launchSettings.json` is for local development only.
