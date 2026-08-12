## Why

The project's XML documentation standards (AGENTS.md) mandate `<exception>` tags wherever a member can throw, and several members instead explain thrown exceptions in prose `<summary>`/`<remarks>` text or plain `//` comments. That prose is easy to miss and does not surface in tooling, so it should be formalized into `<exception cref="...">` tags — while keeping `<remarks>` for the *why* (business rules / design decisions), not the *what can go wrong*.

## What Changes

- **`src/Shared/QueueApi.Auth/IUserCredentialsProvider.cs`** — `VerifyCredentialsAsync` and `UserExistsAsync` document the store-unavailable failure with `<exception cref="InvalidOperationException">`; the concrete `DbUserCredentialsProvider` methods inherit it via `<inheritdoc/>`.
- **`tools/AuthDbInit/AuthDbInitializer.cs`** — `InitializeAsync` documents the store-unreachable failure with `<exception cref="System.Data.Common.DbException">`.
- **`src/CmsWebhook/CmsWebhook.Api/Program.cs`** — the three helper methods (`ResolveConnectionString`, `FindRepositoryRoot`, `ResolveCmsUsername`) get proper XML doc blocks (they are already static methods, so tags can attach); the prose "Throws InvalidOperationException when…" lines become `<exception cref="InvalidOperationException">` tags.
- **Verified already-compliant, no change**: `Pbkdf2PasswordHasher.Hash` already tags its `ArgumentNullException`; `Verify` never throws (malformed input returns `false`).
- No behavior, API, or test changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None — documentation conventions only, no spec-level behavior change. The change's `.openspec.yaml` declares `skip_specs: true`.

## Impact

- `src/Shared/QueueApi.Auth/IUserCredentialsProvider.cs`
- `tools/AuthDbInit/AuthDbInitializer.cs`
- `src/CmsWebhook/CmsWebhook.Api/Program.cs`
- Build must stay at 0 warnings; all tests must stay green.
