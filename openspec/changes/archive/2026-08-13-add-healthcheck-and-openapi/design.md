# Design: Healthcheck, OpenAPI contract, and endpoint organization

## Context

The API currently exposes `GET /` returning `"Hello World!"` and `POST /cms/events`, both mapped inline in `Program.cs`. The app uses a fallback authorization policy (Basic auth + cms-username claim) applied to every endpoint without explicit authorization metadata, so any new endpoint must opt out explicitly. See `proposal.md` for the why; `specs/cms-webhook-api/spec.md` for the required behavior. All patterns below follow the official .NET 9 docs: [Generate OpenAPI documents](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0) and [Minimal APIs — endpoint defined outside of Program.cs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-9.0#endpoint-defined-outside-of-programcs).

## Goals / Non-Goals

**Goals**
- Replace `GET /` with an anonymous liveness healthcheck (`/health`) that load balancers can probe.
- Move endpoint definitions out of `Program.cs` into per-feature endpoint classes.
- Expose a generated OpenAPI contract at `/openapi/v1.json` that stays in sync with the code, plus a browsable reference UI in Development.

**Non-Goals**
- No readiness/deep health checks (DB connectivity probes): the app already fails fast at startup when either store is unreachable, so a liveness check is sufficient for v1 (user decision).
- No checked-in / build-time generated OpenAPI artifact (see D3).
- No changes to `/cms/events` behavior — endpoint reorganization is behavior-neutral.
- No API versioning or multiple OpenAPI documents.

## Decisions

### D1: Anonymous liveness healthcheck at `/health` with JSON body

Use the built-in `AddHealthChecks()` + `MapHealthChecks("/health")`. Because the app's fallback authorization policy would otherwise require auth, the mapped endpoint calls `.AllowAnonymous()`. A small `HealthCheckOptions.ResponseWriter` delegate returns a JSON body (`{"status": "Healthy"}` / `{"status": "Unhealthy"}`) mapped to `200`/`503` — .NET 9 has no built-in JSON health response writer, so a ~10-line custom writer is used instead of a dependency.

- **Alternatives considered**: `WriteResponseAsync` default (plain text) — rejected because the user asked for a JSON body; health check packages like `AspNetCore.HealthChecks.Sqlite` — rejected (no DB checks in scope).
- **Rationale**: liveness-only matches the user's choice; anonymous matches how orchestrators probe services.

### D2: Endpoints in per-feature classes (official minimal-API pattern)

New folder `src/CmsWebhook/CmsWebhook.Api/Endpoints/` with static classes exposing `IEndpointRouteBuilder` extension methods, exactly per the official docs:

- `HealthEndpoints.MapHealthEndpoints(this IEndpointRouteBuilder)` — registers `/health`.
- `CmsEventEndpoints.MapCmsEventEndpoints(this IEndpointRouteBuilder)` — moves the `/cms/events` handler and its `DeserializeCmsRequest`/`DeserializeCmsRequestBatch` helpers out of `Program.cs`; adds OpenAPI metadata (`WithSummary`, `WithDescription`, `WithTag`).
- `Program.cs` keeps startup concerns (configuration resolution, DI, auth policy, fail-fast store checks, `app.Run()`) and calls `app.MapHealthEndpoints()`, `app.MapCmsEventEndpoints()`, `app.MapOpenApi()`.

- **Alternatives considered**: controllers (rejected — the codebase is minimal-API style, and the official minimal-API guidance is the extension-method pattern); a single `ApiEndpoints.cs` (rejected — per-feature separation scales and matches feature-based organization).
- **Rationale**: official pattern, keeps `Program.cs` about composition, and gives each endpoint area a home.

### D3: OpenAPI contract generated at runtime from code (standard .NET 9 approach)

- Add the `Microsoft.AspNetCore.OpenApi` package.
- `builder.Services.AddOpenApi(options => ...)` with a document transformer setting `document.Info` (title "CMS Webhook API", version `v1`).
- `app.MapOpenApi().AllowAnonymous()` serves the document at `/openapi/v1.json`. The docs note the OpenAPI endpoint does not enable auth by default, but our fallback policy requires an explicit `.AllowAnonymous()`.
- **Sync strategy**: the document is generated from the endpoint code on every request, so it can never drift from the implementation — code is the source of truth. Endpoint metadata (`WithSummary`/`WithDescription`/`WithTag`, `[Description]` on request fields) enriches the contract. Optional output-caching of the document (`CacheOutput`, per docs) is deferred — not needed at this scale.
- **Rejected**: build-time generation via `Microsoft.Extensions.ApiDescription.Server`. It launches the app's entry point with a mock server, which would execute our DB fail-fast startup (a real complication here), and a committed artifact re-introduces the drift/commit-discipline problem runtime generation eliminates. Noted for the future if CI linting or client generation (e.g. Kiota) is wanted.
- **UI**: add `Scalar.AspNetCore` and `app.MapScalarApiReference()` **in the Development environment only** — the browsable reference the ASP.NET Core docs point to as the Swagger replacement. Production keeps the raw contract at `/openapi/v1.json`.

### D4: Documentation and tests updated in the same change

- `README.md` sanity check changes from `curl -u ... /` to `curl /health` (no auth).
- `docs/architecture.md`: authentication section records the anonymous carve-out for `/health` and `/openapi/v1.json`; new short sections for the healthcheck and the OpenAPI contract.
- Tests: the auth integration tests that exercised `GET /` move to a protected endpoint (or assert `/health` is anonymous); new tests cover anonymous `/health` (`200` + JSON body) and `/openapi/v1.json` (anonymous `200`, document contains the endpoint paths).

## Risks / Trade-offs

- **Anonymous endpoints escaping the fallback policy** → the two `.AllowAnonymous()` calls are the only exceptions; integration tests assert both are reachable without credentials while `/cms/events` still returns `401`.
- **Scalar UI is Development-only** → production loses the browsable UI; the contract JSON remains available. If a production UI is wanted later, map Scalar unconditionally (or behind a config flag).
- **`GET /` removal is breaking** for anyone scripting the old health probe → README and any scripts are updated in this change; rollback is a revert (the endpoint is trivial to restore).
- **Custom JSON health writer** → must keep the `200`/`503` mapping correct; covered by the health tests.

## Migration Plan

No data or schema changes. Deploy normally; the only external-facing difference is `GET /` replaced by `GET /health`. Rollback: revert this change (or restore the `GET /` mapping).

## Open Questions

- Expose the contract in YAML too (`/openapi/v1.yaml`)? Defer — JSON is sufficient for v1 and the docs show YAML is a one-line addition later.
- Output-cache the OpenAPI document? Defer until document size/usage justifies it.
