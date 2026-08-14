## Context

See proposal.md - Why. `Program.cs` maps the Scalar API reference UI only inside an `if (app.Environment.IsDevelopment())` guard; the raw contract at `/openapi/v1.json` is already anonymous in every environment. The `cms-webhook-api` spec now requires the browsable UI in all environments (see specs).

## Goals / Non-Goals

**Goals:**
- Scalar UI always-on and anonymous for the CMS Webhook API.
- Establish the pattern the Users API ships with from day one (see `users-api-vertical`).
- No change to the generated contract itself.

**Non-Goals:**
- Authenticating the UI (it renders the already-public contract).
- A shared multi-application docs gateway.

## Decisions

### 1. Always-on Scalar

Remove the `IsDevelopment()` guard around `MapScalarApiReference()`, keeping `.AllowAnonymous()`. Rationale: deployed consumers need to browse the contract, the UI renders exactly the same public JSON already served anonymously, and the environment guard buys no real security.

- *Alternative:* serve the UI only outside Production — rejected; the user chose always-on for consistency.

### 2. Tests assert non-Development behavior

`WebApplicationFactory` defaults the host environment to **Development**, so the always-on test MUST run with `UseEnvironment("Production")` — without it the test passes even while the guard is still in place (vacuous). The test asserts `GET /scalar/v1` returns `200` anonymously and serves the UI (`text/html`); a second regression test asserts Scalar is still reachable in Development. Verified: no existing test references Scalar or the environment, so nothing currently asserts the dev-only behavior, and the existing `401` carve-out test for `/cms/events` is unaffected.

## Risks / Trade-offs

- [Serving the UI is mild info exposure] → the raw JSON contract is already anonymous; the UI is a renderer of the same content. Documented, accepted.
- [Existing tests assume dev-only Scalar] → none exist (verified); the new test's `UseEnvironment("Production")` is what makes it meaningful.

## Open Questions

None.
