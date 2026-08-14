## 1. Implementation

- [ ] 1.1 Remove the `IsDevelopment()` guard around `MapScalarApiReference()` in `src/CmsWebhook/CmsWebhook.Api/Program.cs`, keeping `.AllowAnonymous()`

## 2. Tests

- [ ] 2.1 Add an integration test running `UseEnvironment("Production")` (the factory defaults to Development, so this is what makes the test meaningful) asserting `GET /scalar/v1` returns `200` anonymously with the UI content type
- [ ] 2.2 Add a regression test asserting Scalar remains reachable in the default Development environment
- [ ] 2.3 Confirm the existing OpenAPI contract tests (anonymous contract, endpoint description, `/cms/events` still `401`) still pass

## 3. Documentation

- [ ] 3.1 Update the OpenAPI section of `docs/architecture.md` (Scalar is no longer Development-only)
