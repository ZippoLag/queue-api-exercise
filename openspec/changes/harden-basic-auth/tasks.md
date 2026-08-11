## 1. Auth Core Cleanup (`src/Shared/QueueApi.Auth/`)

- [x] 1.1 Remove the unused `ReservedCmsUsername = "cms"` constant (and its XML docs) from `EnvironmentUserCredentialsProvider` (design decision 1; verified referenced nowhere)
- [x] 1.2 Remove the `ClaimTypes.Role = "AuthenticatedUser"` claim from `BasicAuthenticationHandler` and adjust the class XML remarks if they reference it (design decision 2)
- [x] 1.3 Update the valid-credentials unit test in `BasicAuthenticationHandlerTests` to assert the principal carries the username claim and no role claim, locking in decision 2

## 2. Wiring (`src/CmsWebhook/CmsWebhook.Api/Program.cs`)

- [x] 2.1 Construct `EnvironmentUserCredentialsProvider` once, register it as the `IUserCredentialsProvider` singleton, and build the authorization policy from its `Username` property, removing the direct `AUTH_CMS_USERNAME` read and duplicated error message (design decision 3)
- [x] 2.2 Keep the `GetRequiredService<IUserCredentialsProvider>()` fail-fast trigger; verify startup-failure behavior is unchanged

## 3. Documentation

- [x] 3.1 Update `README.md` "Running instructions" with the required `AUTH_CMS_USERNAME` / `AUTH_CMS_PASSWORD` environment variables and the note that production must serve over TLS (design decision 4)

## 4. Verification

- [x] 4.1 Run `dotnet build QueueApi.slnx` — 0 warnings, 0 errors
- [x] 4.2 Run `dotnet test` on the whole solution — all tests pass (401/403/200 + startup-failure paths unchanged)
- [x] 4.3 Code review of the dedup'd wiring and the removals (HTTP surface and constant-time comparison untouched)
