## Why

With the database-backed credential store from `add-sqlite-auth-db` in place, the legacy `AUTH_CMS_*` environment variables are redundant. The username has an explicit configuration knob (`Auth:CmsUsername`) and the password lives in the store, so keeping env-var fallbacks in the code leaves stale rules that a fresh contributor cannot discover, and gives two sources of truth where there should be one. We are removing them now, pre-commit, while the window for a breaking cleanup is free.

## What Changes

- **`Program.cs`** — `ResolveCmsUsername` stops reading `AUTH_CMS_USERNAME`; the cms username comes solely from `Auth:CmsUsername` (default `cms-webhook`), with the `[10,20]` length validation unchanged. **BREAKING**: a deployment that relied on `AUTH_CMS_USERNAME` to change the authorized user must migrate to `Auth:CmsUsername` / `Auth__CmsUsername`.
- **`scripts/init-db.sh`** — removes the `AUTH_CMS_PASSWORD` fallback; the password comes only from the positional argument (falling back to the documented local-development default). The warning branch simplifies to "no password argument given". **BREAKING**: a workflow that seeded via `AUTH_CMS_PASSWORD` must pass the password as an argument.
- **Tests** — the `[10,20]` startup-failure test injects the invalid username via the `Auth__CmsUsername` configuration environment variable instead of `AUTH_CMS_USERNAME`. No tests are removed: the unit tests in `QueueApi.Auth.Tests` are intentionally kept (they test the mechanism; the API integration tests test the contract — see design decisions).
- **`README.md`** — drops both env-var mentions.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cms-webhook-api/basic-auth`: the "Configured credential format" requirement changes so the cms username is read exclusively from configuration and the legacy `AUTH_CMS_*` environment variables are no longer consulted.
- `auth/credential-store`: the "Credential store is provisioned by an initialization script" requirement changes so the script seeds from positional arguments only and does not read credentials from `AUTH_CMS_PASSWORD`.

## Impact

- `src/CmsWebhook/CmsWebhook.Api/Program.cs`
- `scripts/init-db.sh`
- `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiAuthTests.cs`
- `README.md`
- No changes to `QueueApi.Auth` or its unit tests. Historical planning artifacts (`harden-basic-auth`, the `add-sqlite-auth-db` design) that mention the variables are left untouched.
