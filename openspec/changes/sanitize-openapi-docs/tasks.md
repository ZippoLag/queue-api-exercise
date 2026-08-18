## 1. Users API contract sanitization

- [ ] 1.1 Replace the `GET /entities` 403 description in `src/Users/Users.Api/Endpoints/EntityEndpoints.cs` (`ConfigureListOperation`) with "The caller is authenticated but not authorized on this API." and update the surrounding XML doc `<remarks>` that currently explains the "reserved cms-webhook user" rejection
- [ ] 1.2 Replace the Basic security scheme description in `src/Users/Users.Api/Program.cs` with generic wording ("HTTP Basic authentication with a valid username and password.")

## 2. CMS Webhook API contract sanitization and accuracy

- [ ] 2.1 Replace the Basic security scheme description in `src/CmsWebhook/CmsWebhook.Api/Program.cs` with the same generic wording as 1.2
- [ ] 2.2 Replace the outbox mentions in `src/CmsWebhook/CmsWebhook.Api/Endpoints/CmsEventEndpoints.cs` — the endpoint `.WithDescription` ("records them in the outbox") and the `201` response description ("recorded in the outbox") — with processing-oriented wording ("records them for processing" / "recorded for processing") and update the class XML doc `<remarks>` if it references the wording
- [ ] 2.3 Add the missing `403 Forbidden` response to `ConfigureOpenApiOperation` in `CmsEventEndpoints.cs` with generic wording ("The caller is authenticated but not authorized on this API."), matching the documented runtime authorization behavior

## 3. Rate limiting on the CMS Webhook API ingestion endpoint

- [ ] 3.1 Register the rate limiter in `src/CmsWebhook/CmsWebhook.Api/Program.cs` with `AddRateLimiter` and a named fixed-window policy configured from `RateLimiting:PermitLimit` and `RateLimiting:WindowSeconds` (documented defaults in `appsettings.json`), and place `app.UseRateLimiter()` before `UseAuthentication`/`UseAuthorization`
- [ ] 3.2 Apply `.RequireRateLimiting("<policy>")` to `POST /cms/events` only, leaving `/health`, `/openapi/v1.json` and `/scalar/v1` exempt
- [ ] 3.3 Add the `429 Too Many Requests` response (generic wording) to `ConfigureOpenApiOperation` so the contract documents the rate-limit rejection

## 4. Regression tests

- [ ] 4.1 Add a leak-scan test to `tests/Users/Users.Api.Tests/UsersApiOpenApiTests.cs`: fetch `/openapi/v1.json` anonymously and assert the body does not contain `cms-webhook`, `shared credential store`, or `outbox`, and that the `GET /entities` 403 description equals the generic wording
- [ ] 4.2 Extend `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiOpenApiTests.cs`: leak-scan test asserting the body does not contain `cms-webhook`, `shared credential store`, or `outbox`; extend the contract-accuracy test to assert the `403` and `429` responses are present on `POST /cms/events` and the descriptions use generic wording
- [ ] 4.3 Add rate-limit tests to `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiEventIngestionTests.cs` (overriding `RateLimiting:PermitLimit` via the test factory to keep the suite fast): requests within the limit succeed; excess requests get `429` and the handler does not execute; `/health` and `/openapi/v1.json` are not rate limited; an unauthenticated flood gets `429` rather than `401`
- [ ] 4.4 Run the full test suite (`dotnet test` from the repository root) and confirm the new tests pass and no existing OpenAPI contract-accuracy or ingestion test regresses

## 5. Documentation sync

- [ ] 5.1 Update `docs/api-contract.md`: the Users API `GET /entities` 403 row and the CMS Webhook API `201`/`403`/`429` rows to the new generic wording; leave the narrative Authentication table and design explanations (reserved users, outbox, shared store) unchanged
- [ ] 5.2 Document the rate-limiting configuration (`RateLimiting:PermitLimit`, `RateLimiting:WindowSeconds`) and defaults in `docs/configuration.md`

## 6. Verification

- [ ] 6.1 Run `dotnet build` and `dotnet test` for the whole solution; confirm zero failures
- [ ] 6.2 Optionally curl the local `/openapi/v1.json` on both APIs and eyeball that no reserved username or implementation phrase remains and the CMS Webhook document lists the `403` and `429` responses; flood `POST /cms/events` past the configured limit and confirm `429`
