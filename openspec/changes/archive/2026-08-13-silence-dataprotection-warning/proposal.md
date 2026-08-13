## Why

Every run of `CmsWebhook.Api` in the devcontainer emits a Data Protection warning: `FileSystemXmlRepository[60]` complains that key-ring files are being stored in `~/.aspnet/DataProtection-Keys`, which does not persist outside the container. The API does not use Data Protection at all (no cookies, no anti-forgery tokens, stateless Basic Auth), so the warning is pure noise on every startup and test run.

## What Changes

- Add a log-level override in `src/CmsWebhook/CmsWebhook.Api/appsettings.json`:
  - `"Microsoft.AspNetCore.DataProtection": "Error"` under `Logging.LogLevel`.
- This silences the `Warning`-level key-ring message while keeping any genuine `Error`-level Data Protection failures visible.
- No code, no behavior, no dependency changes.

## Capabilities

### New Capabilities

None — this is a configuration-only change (tooling/logging noise), with no externally observable behavior change. Opts out of specs via `skip_specs: true` (see `.openspec.yaml`).

### Modified Capabilities

None.

## Impact

- `src/CmsWebhook/CmsWebhook.Api/appsettings.json` — one `LogLevel` entry added.
- `appsettings.Development.json` — unchanged; it inherits the base `LogLevel` override (it does not redefine the `Microsoft.AspNetCore.DataProtection` category).
- No tests should change; existing behavior is untouched. Verification is that the warning no longer appears when the API runs or when integration tests execute.
