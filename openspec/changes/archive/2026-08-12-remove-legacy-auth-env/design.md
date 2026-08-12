## Context

The `add-sqlite-auth-db` change made the SQLite credential store the source of truth for credentials. The reserved cms username is resolved in `Program.cs` via `ResolveCmsUsername`, which today reads the legacy `AUTH_CMS_USERNAME` environment variable as an override over the `Auth:CmsUsername` config value; `scripts/init-db.sh` similarly falls back to `AUTH_CMS_PASSWORD`. Both fallbacks predate the store and are now redundant. See proposal.md — Why for motivation.

## Goals / Non-Goals

**Goals:**
- One source of truth for the cms username (config `Auth:CmsUsername`) and for the password (the credential store).
- Remove every `AUTH_CMS_*` reference from code, script, tests, and README.
- Keep the full test suite green (42 tests) with 0 build warnings.

**Non-Goals:**
- No test reduction: the unit tests in `QueueApi.Auth.Tests` and the integration tests in `CmsWebhook.Api.Tests` are intentionally layered (mechanism vs. HTTP contract) and are all kept as-is.
- No refactor of the seeding path (`AuthDbInitializer` / init tool): the seed logic will need restructuring anyway once User flows introduce an administrator user, so it stays unchanged here.
- No edits to historical planning artifacts (`harden-basic-auth`, `add-sqlite-auth-db` design) that mention the variables.

## Decisions

### D1: `ResolveCmsUsername` reads configuration only
`Program.cs` resolves the username as `configuration["Auth:CmsUsername"] ?? "cms-webhook"`, keeping the `[10,20]` length validation and its startup failure.
- *Alternative considered*: keep `AUTH_CMS_USERNAME` as an override — rejected: two sources of truth with different precedence is exactly the stale rule being removed.

### D2: The `[10,20]` startup test injects via `Auth__CmsUsername`
`CmsWebhookApiAuthTests.CreateClient_WhenConfiguredUsernameLengthIsInvalid_ThrowsAtStartup` sets the process environment variable `Auth__CmsUsername` instead of `AUTH_CMS_USERNAME`.
- Why this works: process env vars are part of the default configuration chain read during `WebApplicationFactory`'s host build, so they are visible to the top-level `Program.cs` config reads (unlike `ConfigureAppConfiguration` callbacks, which run after top-level code — the trap hit in `add-sqlite-auth-db`). `Program.cs` needs no special casing.
- *Alternative considered*: a custom `ConfigureAppConfiguration` override — rejected as unreliable for minimal-hosting top-level reads.

### D3: `init-db.sh` takes the password positionally
`PASSWORD="${2:-<local-dev default>}"` with the warning branch simplified to "no password argument given" (`[ "$#" -lt 2 ]`). The header comment drops the `AUTH_CMS_PASSWORD` mention.

### D4: README documents the single config knob
The "Credentials & configuration" note states the cms username comes from `Auth:CmsUsername` (or `Auth__CmsUsername`) and the password from the store, with no env-var fallbacks.

## Risks / Trade-offs

- [**BREAKING** for anyone relying on `AUTH_CMS_*`] → There are no committed deployments; the window is pre-commit; README documents the replacement (`Auth:CmsUsername` / `Auth__CmsUsername`, positional script arguments).
- [`Auth__CmsUsername` is itself an environment variable] → It is the standard .NET config-chain knob, not a bespoke code rule; it remains the documented override mechanism.
- [Historical artifacts reference the removed variables] → Deliberately left untouched as a historical record of the env-based era.

## Migration Plan

Pre-commit cleanup; no deployment sequence. Verification is: `dotnet build` (0 warnings), `dotnet test` (all green), and a manual onboarding run of `scripts/init-db.sh` (with and without a password argument) followed by authenticated requests.

## Open Questions

None.
