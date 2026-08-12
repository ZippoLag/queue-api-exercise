## Context

See proposal.md - Why for motivation. Current state that shapes the approach:

- `QueueApi.Auth` resolves credentials through `IUserCredentialsProvider`; production uses `EnvironmentUserCredentialsProvider` (reads `AUTH_CMS_USERNAME` / `AUTH_CMS_PASSWORD`). `BasicAuthenticationHandler` compares plaintext passwords in constant time and sets `ClaimTypes.Name`. `Program.cs` derives the cms username from the provider to build the "only cms user" authorization policy.
- The architecture doc (`docs/architecture.md`) already commits to a single SQLite file database for persistence and reserves `cms-webhook` (length 11, within the `[10,20]` rule) for the CMS API.
- Tests rely on the `IUserCredentialsProvider` seam (`InMemoryUserCredentialsProvider`, `CmsWebhookApiFactory` override) and on `EnvironmentVariableScope` for env-driven startup tests.
- The project is dependency-averse (see README), so new dependencies are a deliberate, justified choice.

## Goals / Non-Goals

**Goals:**
- Store credentials in a SQLite database under the repo root `db/` folder and authenticate against it.
- Provide an idempotent onboarding script that a new developer runs once before `dotnet run`.
- Verify passwords against PBKDF2 hashes with no plaintext ever persisted or compared.
- Keep the DB provider swappable with minimal change (config + package, no code rewrite).
- Preserve the existing auth behavior contract: `401` for missing/invalid credentials, `403` for valid non-cms users.

**Non-Goals:**
- No user-management API yet (create/update/delete users) — provisioning happens only via the init script.
- No `dotnet ef` migrations infrastructure — schema creation is part of the init tool.
- No Users API changes; the credential store is designed to be reused there, but wiring it in is out of scope.
- No DB engine migration in this change — only the structural readiness for it (EF Core provider pattern).

## Decisions

### D1: EF Core (SQLite provider) for data access
`QueueApi.Auth` references `Microsoft.EntityFrameworkCore.Sqlite`. A single `AuthDbContext` exposes a `Users` set.

- **Why:** The user's explicit goal of "easy replacement of the sqlite connection with another DB" is exactly EF Core's sweet spot: swapping the provider (Sqlite → SqlServer/Npgsql) is a package + connection-string change, and the provider-neutral LINQ queries need no rewrite. The shared Auth project is the natural home since both APIs will consume the store.
- **Alternatives considered:** `Microsoft.Data.Sqlite` + hand-written repository (lighter, but SQL portability and mapping are manual); Dapper (same portability cost). Both rejected because they push DB-dialect portability onto us later.

### D2: In-box PBKDF2 hashing via `Rfc2898DeriveBytes` (SHA-256, 100,000 iterations)
A `Pbkdf2PasswordHasher` static class in `QueueApi.Auth` with `Hash(string password)` and `Verify(string password, string encodedHash)`.

- **Format:** self-describing single column `password_hash` string: `PBKDF2-SHA256$<iterations>$<base64 salt>$<base64 derived-key>`. Self-describing so the iteration count can be raised in the future without breaking existing rows.
- **Why:** User-selected; zero new packages, matching the project's KISS/dependency-averse stance. `Rfc2898DeriveBytes` defaults to SHA-1, so the design pins SHA-256 explicitly and 100,000 iterations (OWASP-recommended floor for PBKDF2-HMAC-SHA256). Salt: 16 random bytes per user via `RandomNumberGenerator`. Verification compares derived keys with `CryptographicOperations.FixedTimeEquals` (constant time, preserving the `harden-basic-auth` decision).
- **Alternatives considered:** ASP.NET Core `PasswordHasher<T>` (user declined — extra Microsoft package); bcrypt (third-party package, declined). Both produce formats we can adopt later if ever needed since verification goes through one helper.

### D3: Provider seam evolves to verification
`IUserCredentialsProvider` changes from `string? GetPassword(string username)` to `Task<bool> VerifyCredentialsAsync(string username, string password)`, and gains `Task<bool> UserExistsAsync(string username)` used by the host's startup check (D7).

- **Why:** With hashed storage, "return the password" no longer exists — returning the hash would just move hashing knowledge into the handler and leak hash data through the seam. Verification encapsulates "resolve user + hash-check" so the handler stays presentation-only. Async fits `HandleAuthenticateAsync` and the EF Core query. A wrong-password and an unknown-user both return `false` → `401`, preserving the current externally-observable behavior.
- **Implication:** `Program.cs` can no longer read the cms username from the provider. It comes from configuration (D4). The `Username` property is removed from the provider; `InMemoryUserCredentialsProvider` (test helper) implements the new methods by comparing against its plaintext dictionary.

### D4: The cms username becomes configuration
New config section `Auth:CmsUsername` (default `cms-webhook`), read in `Program.cs` to build the authorization policy's `RequireClaim(ClaimTypes.Name, ...)`. Env override via `AUTH_CMS_USERNAME` is honored automatically by the config provider layering (so existing deployments keep working with zero changes beyond the DB init). Length `[10,20]` validated at startup as today.

### D5: Connection string and DB file location
- Default DB file: `<repo-root>/db/queue-auth.db`; `db/` is tracked (with a `.gitkeep`), the `.db` file is gitignored.
- Config key: `ConnectionStrings:AuthDb` (env override `ConnectionStrings__AuthDb`), default value `Data Source=db/queue-auth.db`.
- **Path resolution:** relative `Data Source=` paths are resolved against the repository root, located by walking up from the content root until `QueueApi.slnx` is found. This keeps the documented "run from repo root" flow working regardless of the working directory the API is launched from, and keeps the connection string short and portable.
- **Why:** A single config value is the "easy replacement" knob; nothing in code hardcodes the file path.

### D6: Initialization script + shared init tool
- `scripts/init-db.sh [username] [password]` — bash wrapper (defaults username to `cms-webhook`, password to `AUTH_CMS_PASSWORD` if set, else a documented local-dev default GUID) that runs `dotnet run --project tools/AuthDbInit`.
- `tools/AuthDbInit` is a console project referencing `QueueApi.Auth`: creates the schema (if not exists), and inserts the user only when the username is absent (idempotent — re-running never duplicates or errors). It reuses `Pbkdf2PasswordHasher` so seeded hashes are always compatible with the API.
- **Why:** Hashing must be done with the exact code the API verifies with — a raw `sqlite3` + SQL script would duplicate the algorithm and drift. A tiny shared tool keeps a single source of truth.

### D7: Fail-fast startup for the store
`Program.cs` keeps the existing startup resolve (`GetRequiredService<IUserCredentialsProvider>()`) and performs one cheap verification query (look up the configured cms user) so an unreachable or uninitialized DB surfaces as a descriptive startup error instead of runtime 401s. The DB-backed provider wraps EF Core connection errors in `InvalidOperationException` with guidance ("run scripts/init-db.sh").

## Risks / Trade-offs

- **EF Core dependency weight in the shared Auth project** → Accepted by design (D1); it is the mechanism for the user's DB-replacement goal, and `QueueApi.Auth` is a shared infra layer, not a leaf domain project.
- **Relative DB path resolution walking up to `QueueApi.slnx`** could surprise if the marker is renamed → Mitigation: resolution falls back to the working directory with a warning log; README documents the flow.
- **PBKDF2 iteration count / algorithm drift over time** → Mitigation: self-describing hash format (D2) lets the API verify old hashes while new hashes use current parameters.
- **Existing env-var tests break** (provider removal, interface change) → Mitigation: `InMemoryUserCredentialsProvider` and the `CmsWebhookApiFactory` seam survive; env-var tests are replaced by DB-backed provider tests using SQLite in-memory/`db/` temp files; handler tests switch to the new interface.
- **SQLite is single-writer** → Out of scope now (one API + local dev); the store is designed so the connection layer can move to a real engine later without code changes.
- **Well-known local-dev default password in the script** could be mistaken for production → Mitigation: README + script output clearly label it as local-dev-only; production must pass an explicit password.

## Migration Plan

1. Add packages, `AuthDbContext`, `Pbkdf2PasswordHasher`, `DbUserCredentialsProvider`; update `IUserCredentialsProvider` and the handler; keep building until green with updated unit tests.
2. Add `tools/AuthDbInit` + `scripts/init-db.sh`; add `db/` folder + `.gitignore` entry.
3. Wire `Program.cs` (config, DI, policy, fail-fast) and update `appsettings.json`.
4. Update integration tests to the DB-backed flow; delete `EnvironmentUserCredentialsProvider` and its tests.
5. Update `README.md` onboarding.
- **Rollback:** revert the change; the previous commit still reads env credentials. No destructive migration runs against any external system (the DB is local and reproducible via the script).
