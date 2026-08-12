## Why

The `CmsWebhook.Api` project currently exposes a single unauthenticated "Hello World" endpoint, while the architecture (`docs/architecture.md`) mandates that all incoming requests to both APIs are protected by Basic Auth (username + password). Without authentication, any client can post CMS events pretending to be the CMS. This change makes the CMS Webhook API require valid Basic Auth credentials on every request, restricted to the reserved `cms` username.

## What Changes

- Enforce Basic Auth (username + password) on **all** requests to `CmsWebhook.Api`, including the existing `/` endpoint.
- Implement the reusable authentication logic in the shared `QueueApi.Auth` project (already referenced by `CmsWebhook.Api`), following the architecture's plan that both APIs share Basic Auth.
- Read the `cms` user's credentials **exclusively from environment variables** — no defaults in `appsettings.json`; the application fails to start if they are not set.
- Reserve the `cms` username for the CMS API: requests with valid credentials for any other username return `403 Forbidden` (per architecture note).
- Missing or invalid credentials return `401 Unauthorized`.
- Enforce the architecture's username rule: `username` length is `[10, 20]` characters; `password` is a randomly generated GUID.
- Add full test coverage: unit tests for the auth components (xUnit + Moq + FluentAssertions) and API-level integration tests via `WebApplicationFactory` asserting `401`/`403`/`200` behavior.

## Capabilities

### New Capabilities

- `cms-webhook-api/basic-auth`: The CMS Webhook API's required behavior for authenticating incoming requests via Basic Auth, including the reserved `cms` username, credential source, and the `401`/`403` response rules.

### Modified Capabilities

- None. This is the first spec in the repository.

## Impact

- `src/Shared/QueueApi.Auth/` — new implementation: credential configuration from env vars, credential validation, and an ASP.NET Core `AuthenticationHandler` for Basic Auth. The project needs a `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to host the handler.
- `src/CmsWebhook/CmsWebhook.Api/Program.cs` — register the auth scheme and require authentication on all endpoints.
- `src/CmsWebhook/CmsWebhook.Api/CmsWebhook.Api.csproj` — no new package references expected; configuration wiring only.
- `tests/Shared/QueueApi.Auth.Tests/` — unit tests for the new auth components.
- `tests/CmsWebhook/CmsWebhook.Api.Tests/` — integration tests booting the API host and asserting HTTP auth behavior.
- No new third-party dependencies: Basic Auth is implemented on top of ASP.NET Core's built-in authentication abstractions.
