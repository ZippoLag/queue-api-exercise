## Context

See proposal.md — Why. The Users API (`src/Users/Users.Api`) is a .NET 9 minimal API on `http://localhost:5265` (dev), serving `GET /entities` (role-filtered listing) and administrator-only `POST /entities/{id}/disable|enable` over Basic auth, with anonymous `/health`, `/openapi/v1.json`, and `/scalar/v1`. It has no CORS, and no UI exists. The coverage ratchet (`scripts/check-coverage.sh`, `.config/coverage-min.txt`) enforces a **100.0% unique-line union** across the solution — any newly added uncovered line fails CI, so the client and every `Program.cs` addition need an explicit coverage story.

## Goals / Non-Goals

**Goals:**
- A minimalistic Wasm-only Blazor client served by the Users API from the same origin, so no CORS is required and the deployed users host needs no Caddy/Terraform change (its root already routes to the Users API).
- Role-based UI derived from the authenticated username, toggle backed entirely by the existing admin endpoints.
- Zero change to the existing API contract: endpoints, auth policies, and the OpenAPI document behave exactly as before.
- CI stays green: the 100% coverage ratchet holds, deploy publishes the UI, live verification checks it.

**Non-Goals:**
- No PWA/offline support, no styling framework, no user management, no changes to the CMS Webhook API, no CORS configuration anywhere, no changes to the API's OpenAPI contract.

## Decisions

### D1. Host the client inside the Users API with the hosted-WASM pattern
`src/Users/Users.Web` is a `blazorwasm` client (Wasm-only, .NET 9, no PWA). `Users.Api` gets a `ProjectReference` to it and the `Microsoft.AspNetCore.Components.WebAssembly.Server` package, then serves it with the classic hosted-WASM pipeline: `UseBlazorFrameworkFiles()` (serves the client's `_framework` files) + `MapFallbackToFile("index.html").AllowAnonymous()` (client-side routes fall back to the shell). The client's static web assets flow into the API's build and publish output automatically via the project reference — CI's existing Users API publish command ships the UI with no extra packaging step.

- **Why this over alternatives:** copying publish output into `wwwroot` (double-build, version skew, no dev-mode serving) and a separate host process (second service on the node, cross-origin calls needing CORS) were both worse. Same-origin hosting also makes the deployed UI reachable at the existing users-host root with zero Caddy changes.
- **.NET 9 caveat:** do **not** add `MapStaticAssets()` for the framework files — the `UseBlazorFrameworkFiles` + `MapFallbackToFile` pair is the supported hosted-WASM path.

### D2. Anonymous static assets, protected endpoints
The fallback policy currently requires authentication for everything except `/health`, `/openapi/v1.json`, `/scalar/v1`. Static file middleware short-circuits before endpoint authorization, so `UseStaticFiles()`/`UseBlazorFrameworkFiles()` run **before** `UseAuthentication`/`UseAuthorization`, and the SPA fallback endpoint is explicitly `.AllowAnonymous()`. Result: the shell and assets load without credentials, while every existing endpoint keeps its exact auth semantics (401/403 behavior unchanged — covered by the existing tests).

### D3. Login form → Basic header on a same-origin HttpClient
The UI's login form captures username/password and sets `Authorization: Basic base64(user:pass)` on a single `HttpClient` whose `BaseAddress` is the app's own origin. Credentials live only in memory for the session. Failed logins surface the API's `401`/`403` (including the reserved `cms-webhook` rejection) as an inline error.

### D4. Role derived from the username, mirroring the API default
The UI treats the signed-in user as administrator iff the username equals its `AdministratorUsername` appsetting (default `administrator`, ordinal compare) — the same default the API resolves from `Auth:AdministratorUsername` and the seeded store ships with. Considered deriving the role from the listing response (regular users never receive disabled items), but that is unreliable on empty lists; adding a "who am I" endpoint would violate the no-API-change constraint.

### D5. Toggle semantics
The administrator's table shows a per-row toggle: `IsVisibleByAdmin == true` renders "Disable" (calls `POST /entities/{id}/disable`), otherwise "Enable" (calls `POST /entities/{id}/enable`). After a `204`, the list is re-fetched so the table reflects the new visibility. `404`/`401`/`403` responses render as an inline error. The regular user's table simply omits the column (their listing never contains disabled entities, so no toggle is meaningful).

### D6. Coverage: exclude the client from the union, test the API wiring
The client assembly carries `[assembly: ExcludeFromCodeCoverage]` (coverlet honors this by default), so its compiled Razor lines don't drag the 100% union below threshold — the behavior the gate protects is the API's, and the client's role/toggle logic is thin enough to verify through the API-level integration tests plus the served shell. The `Program.cs` additions (static files, framework files, fallback) **are** covered by new `WebApplicationFactory` tests: `GET /` returns the shell, a client-route path falls back to the shell, both anonymous, and the existing endpoint tests still pass. bUnit was considered and rejected: a new test dependency for a deliberately minimalistic UI.

### D7. Deployment rides the existing Users API publish
CI's existing `dotnet publish` of `Users.Api` (linux-arm64) emits the client inside the API's publish output (project reference). `scripts/deploy-aws.sh` gains a live-verify step: `GET <users-host>/` returns the shell (contains the Blazor boot script marker), added to the existing health + smoke verification. Local `scripts/smoke-e2e.sh` gets the same shell check.

## Risks / Trade-offs

- **Coverage gate at 100%** → the client is excluded via `ExcludeFromCodeCoverage` (documented in D6) and every new `Program.cs` line has a matching integration test; `bash scripts/check-coverage.sh` must pass before commit.
- **Middleware ordering mistakes would 401 the UI** → integration tests assert the shell is anonymous and protected endpoints still 401/403 exactly as before.
- **Admin-username divergence** if an operator overrides `Auth:AdministratorUsername` on the API without touching the UI's appsetting (D4) → documented in the app's appsettings and the deployment docs; the shipped demo environment uses the default.
- **Static-assets vs `MapStaticAssets` interaction in .NET 9** → stick to `UseBlazorFrameworkFiles` (D1); verified against the hosted-WASM template behavior.
- **UI rides the API deploy** → a UI-only change triggers a full API redeploy; accepted for a single-node demo deployment (rollback = redeploying the previous artifacts; no database impact).

## Migration Plan

Additive change: no data migration. Local: `dotnet run` the Users API and browse `http://localhost:5265/`. Deployed: standard redeploy (artifacts published and applied as today, plus the new UI-shell verify step). Rollback: redeploy the previous artifact set — the store is untouched.

## Open Questions

None material; assumptions (project name `Users.Web`, origin-root serving, in-memory credentials) are recorded in the proposal and design and are easy to change without touching the specs.
