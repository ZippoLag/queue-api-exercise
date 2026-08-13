## Why

The API's `GET /` returns a placeholder `"Hello World!"`, and both endpoints live inline in `Program.cs` instead of at their proper locations. With Swagger removed from .NET 9 templates, the project needs the standard OpenAPI setup: a contract generated from code (so it can never drift) and a way to navigate it via REST.

## What Changes

- Replace `GET /` ("Hello World") with a standard **anonymous liveness healthcheck** at `/health` (`AddHealthChecks` + `MapHealthChecks`) returning a JSON body (`200`/`503`), so load balancers and orchestrators can probe the API without credentials.
- Move endpoints out of `Program.cs` into per-feature endpoint classes exposing `MapXxx(this IEndpointRouteBuilder)` extension methods (official minimal-API pattern): `HealthEndpoints`, `CmsEventEndpoints`. `Program.cs` keeps startup/configuration and calls the mapping methods.
- Add **OpenAPI document generation** the standard .NET 9 way: `Microsoft.AspNetCore.OpenApi` package, `AddOpenApi()` + `MapOpenApi()`, serving `/openapi/v1.json` generated from code at runtime — the contract is always in sync with the endpoints. The document endpoint is anonymous.
- Add **Scalar** (`Scalar.AspNetCore`) as the browsable API reference UI (the Swagger replacement documented by ASP.NET Core), mapped in the Development environment so the contract is navigable from the browser.
- **BREAKING**: `GET /` is removed; `/health` and `/openapi/v1.json` are the first endpoints exempt from the "all endpoints require authentication" rule (both are `.AllowAnonymous()`). The existing auth spec and the tests/README that use `GET /` are updated accordingly.
- Documentation updates: README quickstart sanity-check command moves from `GET /` to `GET /health`; architecture.md records the healthcheck, OpenAPI contract, and the anonymous-endpoint carve-out.

## Capabilities

### New Capabilities
<!-- None: healthcheck and OpenAPI are part of the existing CMS Webhook API surface. -->

### Modified Capabilities
- `cms-webhook-api`: the "All endpoints require authentication" requirement gains explicit exceptions for the anonymous `/health` and `/openapi/v1.json` endpoints; new requirements added for the healthcheck and the OpenAPI contract.

## Impact

- **Code**: `src/CmsWebhook/CmsWebhook.Api/Program.cs` (remove `/`, wire endpoint mappings), new `src/CmsWebhook/CmsWebhook.Api/Endpoints/HealthEndpoints.cs` and `Endpoints/CmsEventEndpoints.cs` (endpoint definitions + OpenAPI metadata), `CmsWebhook.Api.csproj` (add `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`).
- **Tests**: `CmsWebhookApiAuthTests` and the smoke/sanity flows that hit `GET /` move to `/health`; new tests for the health endpoint (anonymous `200`, JSON body) and the OpenAPI endpoint (`/openapi/v1.json` served, anonymous).
- **Docs**: `README.md` (curl example), `docs/architecture.md` (auth carve-out, health, OpenAPI), `docs/configuration.md` if the healthcheck path is configurable.
- **Behavior preserved**: `/cms/events` semantics are unchanged; endpoint reorganization is behavior-neutral.
