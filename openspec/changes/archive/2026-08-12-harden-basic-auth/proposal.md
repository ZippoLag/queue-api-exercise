## Why

A code review of the completed `add-basic-auth` change found the implementation to be correct and well-tested, but carrying a few small leftovers: dead/misleading code (an unused `"cms"` constant and an unused role claim), duplicated credential-reading logic between `Program.cs` and the credential provider, and security constraints (TLS requirement, timing side-channels) that are only implicit. This change tidies the code and makes those constraints explicit so future increments and deployers don't misread them.

## What Changes

- **Remove** the unused `ReservedCmsUsername = "cms"` constant from `EnvironmentUserCredentialsProvider`. It is never referenced, and it misleads: the spec authorizes the *configured* cms user, which per the `[10,20]` username rule can never literally be `"cms"`.
- **Remove** the unused `ClaimTypes.Role = "AuthenticatedUser"` claim from `BasicAuthenticationHandler`. No policy consumes it, and it silently pollutes the principal for any future API reusing the handler.
- **De-duplicate** credential reading in `CmsWebhook.Api/Program.cs`: construct the `EnvironmentUserCredentialsProvider` once, register that instance, and build the authorization policy from its `Username` property — eliminating the second env-var read and the duplicated error message.
- **Document** (no behavior change) that Basic Auth must be served over TLS in production and that the known timing side-channels are accepted for the current single-user system.
- Update `README.md` with the required `AUTH_CMS_USERNAME` / `AUTH_CMS_PASSWORD` environment variables, fulfilling a promise made in the original design.

## Capabilities

### New Capabilities

None — this change is a pure refactor plus hardening documentation. No externally observable behavior changes, so it opts out of specs via `skip_specs: true` (see `.openspec.yaml`).

### Modified Capabilities

None.

## Impact

- `src/Shared/QueueApi.Auth/EnvironmentUserCredentialsProvider.cs` — remove the unused `ReservedCmsUsername` constant (and its XML docs).
- `src/Shared/QueueApi.Auth/BasicAuthenticationHandler.cs` — remove the unused role claim.
- `src/CmsWebhook/CmsWebhook.Api/Program.cs` — single provider construction; policy uses `provider.Username`.
- `tests/` — no behavior changes expected; existing 27 tests must keep passing. Verify no test references the removed constant (none do today).
- `README.md` — document the auth environment variables.
- No dependency, API surface, or spec changes.
