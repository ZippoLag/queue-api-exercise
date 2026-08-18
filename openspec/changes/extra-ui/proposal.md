## Why

The entity store served by the Users API currently has no human-facing UI: consumers must drive `GET /entities` and the admin enable/disable commands with curl or the Scalar reference. A minimalistic browser UI served from the same origin as the Users API gives both roles a direct way to see the published entities and lets the administrator flip entity visibility without leaving the browser.

## What Changes

- **New Blazor WebAssembly client project** (`src/Users/Users.Web`, Wasm-only, .NET 9): a minimalistic single-page app with a login form, an entity table, and role-based columns.
- **Users API serves the UI at its origin root** (`/`): the WASM bundle is served as anonymous static content with an SPA fallback, hosted inside the Users API project so the app calls the existing endpoints same-origin (no CORS anywhere). **No existing endpoint, auth policy, or API contract changes** — this is purely additive static-file serving. The "no change in the APIs" constraint is honored at the contract level: existing consumers of the API are unaffected.
- **Role-based UIs**: the `administrator` sees the full entity table (id, visibility flag, version, update time, payload) plus an enable/disable toggle per row that calls the existing `POST /entities/{id}/disable|enable` endpoints; a regular user sees the same table **without** the toggle column. The role is derived from the authenticated username, exactly like the API derives it.
- **Credentials**: the login form accepts the same credentials as the Users API (the shared credential store). The app attaches them as a Basic `Authorization` header on same-origin calls. `cms-webhook` is rejected (the Users API returns `403`), surfaced as a descriptive login error.
- **Deployment (AWS demo node)**: CI publishes the UI together with the Users API artifacts, the deploy script ships it, and the live-deployment verification gains a check that `/` serves the UI. The users host's root already routes to the Users API in Caddy, so no Caddy/Terraform changes are required.
- **Docs**: architecture, API contract, deployment runbook, and README updated to describe the UI; OpenSpec deltas for `users-api` and `aws-deployment`.

## Capabilities

### New Capabilities

None — the UI belongs to the existing `users-api` domain (same origin, same credentials, same endpoints).

### Modified Capabilities

- `users-api`: New requirement — the Users API hosts a browser UI: anonymous WASM app at `/`, login with the same credentials, role-based entity table, administrator toggle against the existing enable/disable endpoints, no change to any existing endpoint contract.
- `aws-deployment`: New requirement — the deployment publishes and ships the UI with the Users API artifacts and verifies `/` serves it after a deploy.

## Impact

- **New project**: `src/Users/Users.Web` (Blazor WebAssembly client), added to `QueueApi.slnx`.
- **Users API project**: anonymous static-file serving + SPA fallback for the WASM bundle, new `Microsoft.AspNetCore.Components.WebAssembly.Server` package reference (the standard hosted-WASM pattern). All existing endpoints, auth policies, and OpenAPI contracts unchanged.
- **Pipeline**: `.github/workflows/ci.yml` and `scripts/deploy-aws.sh` publish/ship the UI and extend live verification; `scripts/smoke-e2e.sh` optionally checks the served UI root.
- **Tests**: new integration tests asserting `/` serves the WASM shell anonymously and the API endpoints still behave as specified; existing tests must stay green.
- **Docs**: `docs/architecture.md`, `docs/api-contract.md`, `docs/deployment-aws.md`, `README.md`.
- **Out of scope**: PWA/offline support, styling framework, user management, changes to the CMS Webhook API, or any change to the Users API's existing endpoints.
