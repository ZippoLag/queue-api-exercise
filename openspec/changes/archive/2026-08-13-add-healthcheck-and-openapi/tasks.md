## 1. OpenAPI setup

- [x] 1.1 Add `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore` packages to `CmsWebhook.Api.csproj`
- [x] 1.2 Register `AddOpenApi(options => ...)` with a document transformer setting the document title ("CMS Webhook API") and version
- [x] 1.3 Map the OpenAPI document endpoint (`app.MapOpenApi().AllowAnonymous()`) serving `/openapi/v1.json`
- [x] 1.4 Map the Scalar API reference UI in the Development environment only

## 2. Healthcheck

- [x] 2.1 Register `AddHealthChecks()` and map `GET /health` with `.AllowAnonymous()` and a JSON `ResponseWriter` producing `{"status": "Healthy"}` / `{"status": "Unhealthy"}` mapped to `200`/`503`
- [x] 2.2 Remove the `GET /` "Hello World" endpoint

## 3. Endpoint reorganization

- [x] 3.1 Create `src/CmsWebhook/CmsWebhook.Api/Endpoints/HealthEndpoints.cs` with `MapHealthEndpoints(this IEndpointRouteBuilder)`
- [x] 3.2 Create `src/CmsWebhook/CmsWebhook.Api/Endpoints/CmsEventEndpoints.cs` moving the `/cms/events` handler and its deserialization helpers out of `Program.cs`, with `WithSummary`/`WithDescription`/`WithTag` OpenAPI metadata
- [x] 3.3 Update `Program.cs` to call `MapHealthEndpoints()`, `MapCmsEventEndpoints()` and `MapOpenApi()` instead of mapping endpoints inline

## 4. Tests

- [x] 4.1 Update auth integration tests that used `GET /` (retarget to a protected endpoint; assert `/health` and `/openapi/v1.json` are anonymous)
- [x] 4.2 Add healthcheck tests: anonymous `GET /health` returns `200` with a JSON body; protected endpoints still require auth
- [x] 4.3 Add OpenAPI tests: anonymous `GET /openapi/v1.json` returns `200` and the document describes `/cms/events` and `/health`; anonymous `POST /cms/events` still returns `401`

## 5. Documentation

- [x] 5.1 Update README quickstart sanity check from `GET /` to `GET /health` (no credentials)
- [x] 5.2 Update `docs/architecture.md`: record the anonymous carve-out for `/health` and `/openapi/v1.json` in the authentication section and add short sections for the healthcheck and the OpenAPI contract

## 6. Verification

- [x] 6.1 `dotnet build` succeeds with no warnings
- [x] 6.2 `dotnet test` passes for all suites
- [x] 6.3 Smoke-test locally: `GET /health` returns `200` anonymously, `GET /openapi/v1.json` returns the contract, and `/cms/events` still returns `401` anonymous / `201` authenticated
