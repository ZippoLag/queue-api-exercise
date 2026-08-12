## Context

- `CmsWebhook.Api` is a minimal-API hello-world (`Program.cs` maps a single `GET /`); it already references the shared `QueueApi.Auth` class library, which is an empty placeholder (`Class1.cs`).
- `docs/architecture.md` mandates Basic Auth (username + password) for all incoming requests to both APIs, with `"cms"` as the reserved username for the CMS API. No persistence layer exists yet (sqlite is TBD), and no third-party packages are referenced anywhere in the solution (README KISS principle).
- User decisions for this increment: credentials come from **environment variables only** (no `appsettings` defaults), and coverage includes **unit + integration tests**.
- AGENTS.md constraints: TDD, xUnit + Moq + FluentAssertions, XML comments on all public members, leveled logging, and tests must cite source business rules.

## Goals / Non-Goals

**Goals:**
- A reusable Basic Auth implementation in `QueueApi.Auth` that both APIs can eventually share.
- `CmsWebhook.Api` rejects unauthenticated requests (`401`) and rejects authenticated non-`cms` users (`403`).
- Fail-fast startup when required env vars are missing or the configured username violates the `[10,20]` length rule.
- Full unit + integration coverage of the behavior in the delta spec.

**Non-Goals:**
- Wiring auth into the User API (the handler is designed to be reused, but only the CMS API is wired here).
- Any persistence/credential store; a DB-backed user store is deferred until the sqlite layer exists.
- HTTPS enforcement, password hashing (passwords are randomly generated GUIDs per architecture), signature verification, rate limiting, or multi-credential rotation.

## Decisions

### 1. Custom `AuthenticationHandler<TOptions>` instead of a third-party package
Implement Basic Auth as a custom `AuthenticationHandler<BasicAuthenticationOptions>` in `QueueApi.Auth`, registered via `AddAuthentication().AddScheme<...>()`.
**Rationale:** reuses ASP.NET Core's built-in auth abstractions (`AuthenticateResult`, challenge/forbid flow), keeps the solution dependency-free per the README's KISS stance, and is directly unit-testable by invoking `HandleAuthenticateAsync` against a mock `HttpContext`.
**Alternatives considered:** the `AspNetCore.Authentication.Basic` NuGet package (rejected: adds a dependency for ~150 lines of code); a raw middleware check (rejected: bypasses the auth abstractions, harder to reuse, no policy support).

### 2. `QueueApi.Auth` gains `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
The shared project is currently a plain class library; hosting an `AuthenticationHandler` requires the ASP.NET Core shared framework.
**Rationale:** the architecture explicitly plans shared auth for both APIs, so the handler belongs in `QueueApi.Auth`, not in the web project. This is the standard way to reference ASP.NET Core from a class library without adding package references.

### 3. Scheme/option shape: `BasicAuthenticationScheme` + options carrying a credential validator
A constant scheme name (e.g. `"BasicAuth"`) and `BasicAuthenticationOptions` that inject a user-credential provider.
**Rationale:** keeps the handler generic (parse/validate/authenticate) while the CMS API supplies the "who may call me" rule via an authorization policy (see decision 5). The User API later reuses the handler with its own policy.

### 4. Credential source: environment variables read at startup, fail-fast
Env vars `AUTH_CMS_USERNAME` and `AUTH_CMS_PASSWORD` (prefix `AUTH_` to avoid collisions with unrelated config). Read through the options pattern with a validator (`IValidateOptions<BasicAuthenticationOptions>` / startup validation) that throws a descriptive `InvalidOperationException` when a var is missing or the username length is outside `[10,20]`.
**Rationale:** the user explicitly chose env-var-only credentials; validating at startup surfaces misconfiguration at deploy time instead of as a puzzling `401` at runtime.
**Alternatives considered:** `appsettings.json` defaults (rejected by user), lazy validation on first request (rejected: hides misconfiguration), a DB-backed user store (deferred, no persistence yet).

### 5. 401 vs 403 split: handler authenticates, policy authorizes
- Missing header / non-Basic scheme / undecodable header / unknown username / wrong password → handler returns `NoResult`/`Fail` → challenge → **401** with a `WWW-Authenticate: Basic realm="..."` header.
- Valid credentials → handler returns an authenticated `ClaimsPrincipal` carrying the username claim (e.g. `ClaimTypes.Name`) and an `"AuthenticatedUser"` role/flag.
- The CMS API applies `RequireAuthorization(policy)` with a policy requiring the authenticated user to be `cms`; any other validly authenticated user fails authorization → **403**.
**Rationale:** mirrors the architecture's "only `cms` may connect, others return 403" while keeping the handler generic. The 403 path is provable in integration tests by overriding the credential provider with a second known user (production config only ever contains `cms`).
**Alternative considered:** returning 403 directly from the handler for any non-`cms` username (rejected: couples the generic handler to CMS-specific rules).

### 6. Constant-time password comparison
Compare the presented password against the stored one with `CryptographicOperations.FixedTimeEquals`.
**Rationale:** avoids username/byte-length timing side channels. Cheap and standard.

### 7. Credential-provider abstraction enables unit/integration testing
A small `IUserCredentialsProvider` (or equivalent) interface that resolves a username to its password; the env-var implementation returns only `cms`. Unit tests mock it; integration tests swap in a provider with an extra user to exercise the `403` path.
**Rationale:** the spec's 403 behavior needs a second known user to be observable; without this seam the path would be untestable. The seam is also the future persistence point (sqlite user store).

### 8. Logging follows AGENTS.md leveled-logging rule
Auth successes log at `Information`, auth failures at `Warning` (including the username attempted, never the password), startup config errors at `Error`. Since no Serilog package is referenced yet and AGENTS.md allows `Console.WriteLine` with explicit levels, the minimal API project uses its built-in `ILogger` to satisfy leveled logging without a new dependency; adopting Serilog is a separate decision.
**Note:** if the user prefers strict Serilog adoption now, that is a small, isolated follow-up and does not change specs or task breakdown.

## Risks / Trade-offs

- [Only `cms` is configurable in production, so a real `403` requires another user] → The policy + provider seam keeps the behavior correct-by-construction and proven by integration tests with a stubbed second user; production simply never authenticates non-`cms` users.
- [Custom auth code is security-sensitive] → Constant-time comparison, minimal parsing surface, single-purpose handler, and review of the handler's exact HTTP surface in code review.
- [Env-var-only credentials are awkward for local dev] → Devcontainer/CI sets the two vars; documented in README/`launchSettings.json` comments so onboarding is frictionless.
- [Shared project referencing the ASP.NET Core framework] → Standard practice via `FrameworkReference`; `QueueApi.Auth` remains usable by both API projects and stays package-free.

## Migration Plan

Greenfield change: `CmsWebhook.Api` currently serves an unauthenticated hello-world. After this change every request requires Basic Auth, so any existing ad-hoc consumers (none in the repo) would need credentials. Rollback = revert the commit; no data migration involved.

## Open Questions

None.
