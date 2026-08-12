## 1. Auth layer: hashing and DB-backed store

- [x] 1.1 Add `Microsoft.EntityFrameworkCore.Sqlite` package to `QueueApi.Auth` (and `Microsoft.EntityFrameworkCore` as needed)
- [x] 1.2 Add `Pbkdf2PasswordHasher` (PBKDF2-HMAC-SHA256, 100,000 iterations, 16-byte random salt, self-describing `PBKDF2-SHA256$<iterations>$<salt>$<hash>` format, `FixedTimeEquals` verification) with unit tests covering hash/verify round-trip, wrong password, malformed stored hash, and format stability
- [x] 1.3 Add `AuthDbContext` with a `Users` table (unique username, `password_hash` column) and its SQLite configuration
- [x] 1.4 Evolve the `IUserCredentialsProvider` seam: replace `GetPassword` with `Task<bool> VerifyCredentialsAsync(string username, string password)` and update `BasicAuthenticationHandler` to await it (unknown user and wrong password both yield `false` → `401`)
- [x] 1.5 Add `DbUserCredentialsProvider` implementing the seam against `AuthDbContext`, wrapping connectivity/initialization failures in descriptive `InvalidOperationException`s
- [x] 1.6 Update `AddBasicAuthentication` DI extension to register the `AuthDbContext` (from the configured connection string) and the DB-backed provider instead of the environment provider

## 2. DB initialization tooling

- [x] 2.1 Add `tools/AuthDbInit` console project referencing `QueueApi.Auth` that creates the schema if missing and seeds the user idempotently (insert only when the username is absent), reusing `Pbkdf2PasswordHasher`
- [x] 2.2 Create the tracked `db/` folder with a `.gitkeep` and add `db/*.db` (and `db/*.db-*`) to `.gitignore`
- [x] 2.3 Add `scripts/init-db.sh` wrapping the tool: `[username] [password]` args, `AUTH_CMS_PASSWORD` fallback, documented local-dev default password, defaults username to `cms-webhook`
- [x] 2.4 Verify onboarding flow manually: run the script, `dotnet run --project src/CmsWebhook/CmsWebhook.Api`, authenticate with seeded credentials (`200`), wrong credentials (`401`)

## 3. CMS API wiring

- [x] 3.1 Add `ConnectionStrings:AuthDb` (default `Data Source=db/queue-auth.db`, relative paths resolved against the repo root found via `QueueApi.slnx`) and `Auth:CmsUsername` (default `cms-webhook`) to `appsettings.json`
- [x] 3.2 Update `Program.cs`: build the "only cms user" policy from `Auth:CmsUsername`, validate its length `[10,20]` at startup, and fail fast by resolving the provider and confirming the cms user exists in the store
- [x] 3.3 Remove `EnvironmentUserCredentialsProvider` and its unit tests (superseded by the DB-backed provider)
- [x] 3.4 Update `README.md` onboarding: run `scripts/init-db.sh`, then `dotnet run`; document the connection-string override and env-var removal

## 4. Tests

- [x] 4.1 Update `BasicAuthenticationHandlerTests` to the new `VerifyCredentialsAsync` seam
- [x] 4.2 Add `DbUserCredentialsProviderTests` (SQLite in-memory/temp file): valid user succeeds, wrong password fails, unknown user fails, missing/uninitialized store throws descriptive error
- [x] 4.3 Update `CmsWebhookApiAuthTests` and `CmsWebhookApiFactory` to the DB-backed flow with a seeded temp store, keeping every `401`/`403`/`200` scenario from the basic-auth spec
- [x] 4.4 Run `dotnet build` and `dotnet test` for the whole solution and confirm everything is green
