## Why

The Auth vertical is currently incomplete: credentials live only in environment variables (`AUTH_CMS_USERNAME` / `AUTH_CMS_PASSWORD`), are compared as plaintext, and the CMS API's "only the cms user is authorized" policy is wired to a startup-time provider that cannot grow. A database-backed credential store finishes the vertical: proper login logic with hashed passwords, a user store the future Users API can share, and a repeatable onboarding flow where a new developer initializes the DB once and runs the API — no env-var juggling.

## What Changes

- Add a SQLite database under the repository root's `db/` folder. The database file itself is gitignored (it is a generated artifact); the `db/` directory is tracked.
- Add an idempotent initialization script (`scripts/init-db.sh`) that creates the schema and seeds the `cms-webhook` user with a **PBKDF2-hashed** password.
- Introduce EF Core (SQLite provider) into `QueueApi.Auth` and a new DB-backed `IUserCredentialsProvider` implementation that replaces `EnvironmentUserCredentialsProvider` in production.
- Rework `BasicAuthenticationHandler` so the presented password is verified against the stored PBKDF2 hash (constant-time comparison of derived bytes), never against plaintext.
- The CMS API's reserved username becomes configuration (`Auth:CmsUsername`, defaulting to `cms-webhook`) instead of being derived from the environment credential provider.
- Make the connection string configurable (`ConnectionStrings:*` in `appsettings.json`, overridable via environment), and structure the data access behind EF Core so replacing SQLite with another EF Core provider later is a package + connection-string change.
- **BREAKING**: `AUTH_CMS_USERNAME` and `AUTH_CMS_PASSWORD` are no longer required to run the CMS API. The database is the source of truth for credentials; the API fails fast at startup when the DB is unreachable or not initialized.

## Capabilities

### New Capabilities

- `auth/credential-store`: shared, database-backed storage of user credentials with PBKDF2-hashed passwords, provisioned by an idempotent initialization script and consumed through a provider seam in `QueueApi.Auth`.

### Modified Capabilities

- `cms-webhook-api/basic-auth`: credentials are now sourced from the credential-store database (initialized via the provided script) instead of environment variables, and passwords are verified against stored hashes. The `401`/`403` rules and the reserved `cms-webhook` username are unchanged.

## Impact

- `src/Shared/QueueApi.Auth`: new package `Microsoft.EntityFrameworkCore.Sqlite` (+ transitive EF Core), new `DbUserCredentialsProvider` (or equivalent), PBKDF2 hashing/verification helper, handler rework, DI extension changes. `EnvironmentUserCredentialsProvider` and its unit tests are removed.
- `src/CmsWebhook/CmsWebhook.Api`: `Program.cs` registers the DB-backed provider from configuration, authorization policy reads the cms username from config, startup fails fast on an unavailable/uninitialized DB.
- New `db/` folder, new `scripts/init-db.sh`, and a small DB-init tool (console project under `tools/`) that shares the hashing code with the API so seeded hashes are always compatible.
- Tests: env-var-driven unit tests for the credential provider are replaced by DB-backed provider tests (in-memory SQLite); handler tests are updated for hash verification; integration tests cover the DB-backed flow.
- `README.md`: onboarding updated to "run `scripts/init-db.sh`, then `dotnet run`".
