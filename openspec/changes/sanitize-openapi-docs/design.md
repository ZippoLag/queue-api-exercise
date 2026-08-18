## Context

See proposal.md - Why. Both APIs build their OpenAPI document in code (document transformers in `Program.cs` plus the endpoint-level `Configure*Operation` helpers) and serve it anonymously at `/openapi/v1.json`, so every description string in that document is public. The strings to sanitize are literal in three source files: the Users API `GET /entities` 403 description (`EntityEndpoints.ConfigureListOperation`), the Basic security scheme description (both `Program.cs` files), and the CMS Webhook ingestion summary/201 description (`CmsEventEndpoints`). Nothing in the runtime error paths needs changing — the auth middleware returns empty 401/403 bodies.

## Goals / Non-Goals

**Goals:**
- Remove the reserved `cms-webhook` username and the other implementation phrases ("shared credential store", "outbox") from the served OpenAPI documents.
- Make the CMS Webhook API contract accurate: document the real `403 Forbidden` (and the new `429`) responses the ingestion endpoint returns.
- Protect `POST /cms/events` from traffic floods with a configurable rate limit, without re-implementing standard .NET middleware.
- Add regression tests so a re-introduced leak fails CI and the rate limiter behaves as specified.

**Non-Goals:**
- Not changing authorization policies or the existing status codes' semantics.
- Not redacting internal documentation (`docs/architecture.md`, `docs/dsl_glossary.md`, the narrative sections of `docs/api-contract.md`) that intentionally explain the reserved-user and outbox design to operators.
- Not rate limiting the Users API, the anonymous discovery endpoints, or applying per-client (username/IP) partitions — a single global window per instance is the scope of this change.

## Decisions

### D1: Generic role-based wording for the Users API 403

The `GET /entities` 403 description becomes *"The caller is authenticated but not authorized on this API."* — the user-approved generic wording, parallel to the existing enable/disable 403 ("The caller is not the administrator."). It stays truthful (the rejection is exactly that) without naming `cms-webhook` or revealing that the account belongs to another API.

- *Alternative:* "The authenticated user is not a human user of this API." — rejected by the user in favor of the generic wording; it describes the machine-account rationale, which is precisely the internal detail the contract should not expose.

### D2: One sanitization approach in code, not a post-processor

The reworded strings are edited directly where they are declared (the three source sites above). No OpenAPI post-processing/redaction pass is added.

- *Rationale:* the leaks are three known literal strings; a document-wide redaction layer would hide future additions rather than force authors to write non-leaking copy, and would add moving parts for no benefit.
- *Alternative:* a document transformer that scrubs banned substrings — rejected as over-engineering that masks the underlying writing rule.

### D3: Leak-scan regression tests assert on the served document

Extend `UsersApiOpenApiTests` and `CmsWebhookApiOpenApiTests` with a test that fetches `/openapi/v1.json` anonymously and asserts the body does not contain the leaked phrases (`cms-webhook`, `shared credential store`, `outbox`) and does contain the new generic descriptions. Asserting on the served JSON (not on the source strings) is what makes the test a contract guard.

- *Note:* the CMS Webhook API document currently has no occurrence of these phrases after the rewording; the Users API test's `cms-webhook` assertion is the critical one (the 403 description is its only current occurrence).
- *Alternative:* unit-testing the `Configure*Operation` helpers directly — rejected: the tests' stated purpose (see `UsersApiOpenApiTests`) is verifying the *served* contract, and the transformer wiring in `Program.cs` is part of what can regress.

### D4: Docs sync is limited to the mirrored rows

`docs/api-contract.md`'s rows that mirror OpenAPI wording are updated to match: the Users API `GET /entities` 403 row, and the CMS Webhook API's `201`/`403`/`429` rows. The narrative tables and sections (the Authentication table listing the reserved users, the outbox explanation, the "shared credential store" reference in Authentication) stay — they are internal operator documentation whose purpose is explaining the design, per the proposal.

### D5: The CMS Webhook API 403 is documented, not just sanitized

The contract-accuracy gap (runtime rejects valid non-cms credentials with 403, but the document omits it) is fixed by adding a `403` response to `CmsEventEndpoints.ConfigureOpenApiOperation` with generic wording ("The caller is authenticated but not authorized on this API."), matching the Users API wording. This is a doc-improvement task that lives with the other OpenAPI work in the `cms-webhook-api` spec delta — the runtime already implements the 403, so nothing behavioral changes here.

### D6: Standard ASP.NET Core rate-limiting middleware, fixed window, endpoint-scoped

Rate limiting uses ASP.NET Core's built-in middleware (`AddRateLimiter` + `AddFixedWindowLimiter`, `app.UseRateLimiter()`, `.RequireRateLimiting("<policy>")` on the ingestion endpoint), per the official docs — no reimplementation. A named fixed-window policy is registered from configuration (`RateLimiting:PermitLimit`, `RateLimiting:WindowSeconds`, with documented defaults), and `UseRateLimiter` is placed **before** `UseAuthentication`/`UseAuthorization` so an unauthenticated flood is rejected with 429 without touching the credential store. Only `POST /cms/events` carries `RequireRateLimiting`; the anonymous discovery endpoints and `/health` are exempt (they are not annotated). Rejections use the middleware's default 429 with `Retry-After`.

- *Why fixed window:* the ingestion endpoint's cost is bounded per time window; fixed window is the simplest standard algorithm that satisfies the spec (sliding/token bucket add precision this change doesn't need).
- *Why endpoint-scoped rather than global:* only ingestion writes to the shared store; the anonymous contract/UI endpoints must stay reachable for discovery and probes.
- *Alternative:* a global limiter over all endpoints — rejected, it would rate limit the liveness probe and break load-balancer health checks.

## Risks / Trade-offs

- [A future contributor re-introduces a username or internal phrase in a description] → D3's leak-scan tests fail CI on the served document; the failing test names the banned phrase, making the fix obvious.
- [The leak-scan test is too strict and blocks a legitimate future mention] → the scan targets the specific known phrases, not a general ban on words like "administrator" or "store", so role-based copy like "The caller is not the administrator." remains legal.
- [Description wording drifts from the actual semantics] → the rewording keeps the meaning intact (403 = authenticated but not authorized; 201 = accepted and recorded for processing; 429 = rate limit exceeded), and the existing contract-accuracy tests still pass unchanged.
- [Rate limiter breaks the E2E/smoke flows by rejecting legitimate traffic in tests] → the default permit limit is generous (e.g. 60 requests/min) and tests either stay under it or override `RateLimiting:PermitLimit` via the test factory; the E2E suite's request volume is far below the default.
- [429 rejection semantics surprise existing consumers] → the 429 is now documented in the OpenAPI contract and `docs/api-contract.md`; it only triggers when the (generous, configurable) window is exceeded.

## Migration Plan

No deployment steps for the documentation part: description strings and tests only. The rate limiter ships in the same release with documented defaults in `appsettings.json`; operators can tune `RateLimiting:*` before or after deploy. Rollback is a revert of the source files. The docs row updates land in the same commit, so nothing drifts.
