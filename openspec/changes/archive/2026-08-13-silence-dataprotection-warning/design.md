## Context

ASP.NET Core registers Data Protection services by default in minimal hosting, and the key-ring repository initializes on first access, logging a `Warning` when it falls back to the user-profile directory (`~/.aspnet/DataProtection-Keys`). In the devcontainer that directory is ephemeral, so the warning appears on every run. `CmsWebhook.Api` never resolves `IDataProtectionProvider` — authentication is stateless Basic Auth with no cookies or anti-forgery — so no protected data exists to lose. See proposal.md — Why.

## Goals / Non-Goals

**Goals:**
- Remove the Data Protection warning from API startup and integration-test output.
- Keep any real Data Protection *errors* visible (don't blind the whole category).
- Zero code change; configuration only.

**Non-Goals:**
- Making the key ring persist across container restarts (the app holds no protected data; the user chose to silence, not fix).
- Disabling Data Protection itself or switching to an ephemeral provider.
- Changing any observable API behavior.

## Decisions

### 1. Silence via a `LogLevel` override, not by reconfiguring Data Protection
Add `"Microsoft.AspNetCore.DataProtection": "Error"` to `Logging.LogLevel` in the base `appsettings.json`. The warning is logged at `Warning` level under the `Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository` category, so the more-specific `Microsoft.AspNetCore.DataProtection` prefix suppresses it while `Error`-level failures still surface.
**Rationale:** the app does not use Data Protection, so the honest fix is to stop listening to its noise — not to invest in key-ring infrastructure that nothing consumes.
**Alternatives considered:**
- `UseEphemeralDataProtectionProvider()` (rejected: adds code and in-memory key semantics; a footgun if cookie auth is added later, since keys would die with the process).
- `PersistKeysToFileSystem(...)` to a stable path (rejected by the user — that's the "fix properly" option, and it configures persistence for data the app never protects).
- `LogLevel: "None"` (rejected: would also hide genuine Data Protection errors).

### 2. Override in the base `appsettings.json`, effective for all environments
The base file already sets `"Microsoft.AspNetCore": "Warning"`; the new, more-specific category wins for every environment, including Development (which only overrides `Default` and `Microsoft.AspNetCore`). The warning is noise in any non-persisted container deployment, not just locally.
**Alternative considered:** scoping to `appsettings.Development.json` only (rejected: the warning also pollutes containerized test/prod runs, and the category is unused everywhere).

### 3. Removal condition recorded for the future
If the API ever actually uses Data Protection (cookies, anti-forgery, protected payloads), this override must be removed and a real key ring configured — at that point the warning becomes meaningful.
**Rationale:** prevents a future dev from "fixing" a silent key-loss problem they can't see.

## Risks / Trade-offs

- [Overriding the category hides future Data Protection warnings] → The app uses no Data Protection today; the removal condition in decision 3 is recorded in this design and will surface at apply/archive time.
- [Log category name drift across .NET versions] → Low risk; the prefix `Microsoft.AspNetCore.DataProtection` is stable in current .NET 9.

## Migration Plan

Config-only change: no migration. Rollback = revert the single `LogLevel` entry.

## Open Questions

None.
