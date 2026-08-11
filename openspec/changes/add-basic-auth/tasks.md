## 1. Project Scaffolding

- [ ] 1.1 Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `src/Shared/QueueApi.Auth/QueueApi.Auth.csproj` so the shared library can host an `AuthenticationHandler`
- [ ] 1.2 Delete the placeholder `src/Shared/QueueApi.Auth/Class1.cs`
- [ ] 1.3 Add `Microsoft.AspNetCore.Mvc.Testing` package to `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhook.Api.Tests.csproj` for `WebApplicationFactory` integration tests
- [ ] 1.4 Add `AUTH_CMS_USERNAME` and `AUTH_CMS_PASSWORD` to the `environmentVariables` of both profiles in `src/CmsWebhook/CmsWebhook.Api/Properties/launchSettings.json` for local development

## 2. Auth Core Unit Tests (write first — TDD)

- [ ] 2.1 Write unit tests for the env-var credential provider: returns configured `cms` credentials, throws a descriptive error when either env var is missing (spec: "Credentials are sourced from environment variables")
- [ ] 2.2 Write unit tests for username length validation: usernames of 9 and 21 chars fail, 10 and 20 chars pass (spec: "Configured credential format"; architecture: `username [10,20]`)
- [ ] 2.3 Write unit tests for the `BasicAuthenticationHandler` `HandleAuthenticateAsync`: missing `Authorization` header → no result/challenge, non-`Basic` scheme → fail, malformed base64 → fail, unknown username → fail, wrong password → fail, valid `cms` credentials → success with username claim (spec: "All endpoints require authentication" + "Only the cms user is authorized")
- [ ] 2.4 Ensure every test cites its source business rule (`docs/architecture.md` section or spec scenario) in its XML `<remarks>` per AGENTS.md

## 3. Auth Core Implementation (`src/Shared/QueueApi.Auth/`)

- [ ] 3.1 Implement `BasicAuthenticationOptions` and a `"BasicAuth"` scheme constant
- [ ] 3.2 Implement the env-var credential provider (`AUTH_CMS_USERNAME` / `AUTH_CMS_PASSWORD`), resolving a username to its password and exposing the reserved `cms` username
- [ ] 3.3 Implement `BasicAuthenticationHandler`: parse `Basic` header, base64-decode into `username:password`, compare password with `CryptographicOperations.FixedTimeEquals`, return `AuthenticateResult` and issue `401` challenge with `WWW-Authenticate: Basic realm="..."` on failure
- [ ] 3.4 Implement fail-fast startup validation: missing env vars or configured username outside `[10,20]` characters throw a descriptive `InvalidOperationException` at startup (spec: "Credentials are sourced from environment variables" + "Configured credential format")
- [ ] 3.5 Implement a DI extension (`AddBasicAuthentication`) registering the scheme with options validation
- [ ] 3.6 Add XML comments (`<summary>`, `<remarks>`, params/returns) to all public members per AGENTS.md; log auth successes at `Information` and failures at `Warning` (never the password)

## 4. CMS Webhook API Wiring

- [ ] 4.1 In `src/CmsWebhook/CmsWebhook.Api/Program.cs`, register the Basic Auth scheme via `QueueApi.Auth` extension and an authorization policy requiring the authenticated username to be `cms`
- [ ] 4.2 Apply `RequireAuthorization` to all endpoints so every request needs authentication (spec: "All endpoints require authentication")
- [ ] 4.3 Verify the app fails to start (descriptive error) when `AUTH_CMS_USERNAME`/`AUTH_CMS_PASSWORD` are not set

## 5. Integration Tests (`tests/CmsWebhook/CmsWebhook.Api.Tests/`)

- [ ] 5.1 Create a `WebApplicationFactory`-based test host that sets valid `AUTH_CMS_USERNAME`/`AUTH_CMS_PASSWORD` and a second known user for the 403 path
- [ ] 5.2 Test `401 Unauthorized`: request without `Authorization` header, with a non-`Basic` scheme, with malformed base64, with unknown username, and with a wrong password (spec: "All endpoints require authentication")
- [ ] 5.3 Test `200 OK`: request with valid `cms` credentials (spec: "Only the cms user is authorized")
- [ ] 5.4 Test `403 Forbidden`: request with valid credentials of a non-`cms` user (spec: "Only the cms user is authorized")
- [ ] 5.5 Test host startup failure when required env vars are missing and when the configured username length is invalid

## 6. Verification

- [ ] 6.1 Run `dotnet build QueueApi.slnx` and fix all warnings/errors
- [ ] 6.2 Run `dotnet test` on the whole solution; all tests pass and no placeholder `UnitTest1.cs` tests remain
- [ ] 6.3 Code review of the auth handler's HTTP surface (challenge headers, status codes) and constant-time comparison
