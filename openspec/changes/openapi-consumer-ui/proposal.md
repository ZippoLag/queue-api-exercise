## Why

API consumers — of both the CMS Webhook API and the upcoming Users API — need to browse the OpenAPI contract as easily as Swagger UI used to allow. Today the CMS Webhook API maps its Scalar reference UI only in the Development environment, so deployed consumers have nothing to browse but the raw `/openapi/v1.json` JSON. Browsing the contract should be a first-class, always-on experience for both applications.

## What Changes

- **CMS Webhook API**: serve the Scalar API reference UI in **all** environments, not just Development — remove the `IsDevelopment()` guard around `MapScalarApiReference()` (keeping it anonymous, like the raw contract).
- Establish the pattern the upcoming Users API ships with from day one: each application serves its own always-on Scalar UI at its own URL (see the `users-api-vertical` change).
- Keep `/openapi/v1.json` and the Scalar UI anonymous, consistent with the existing exception for discovery endpoints (load balancers, orchestrators, and clients probe/discover without credentials).
- No change to the generated contract itself — only to who can view the browsable UI.

## Capabilities

### New Capabilities

None — the behavior change lands on the existing CMS Webhook API; the Users API counterpart is declared by `users-api-vertical`.

### Modified Capabilities

- `cms-webhook-api`: the OpenAPI requirement gains an always-on browsable UI — the Scalar reference UI SHALL be served in every environment, not only Development. (The raw contract at `/openapi/v1.json` is unchanged.)

## Impact

- **Code**: `src/CmsWebhook/CmsWebhook.Api/Program.cs` — drop the environment guard around `MapScalarApiReference()`; adjust the OpenAPI document transformer if it references environment-specific behavior.
- **Tests**: `tests/CmsWebhook/CmsWebhook.Api.Tests` — add/update scenarios asserting Scalar is reachable outside Development (e.g. running the factory in a non-Development environment) and stays anonymous.
- **Docs**: `docs/architecture.md`'s OpenAPI section (currently states Scalar is Development-only) and the `cms-webhook-api` spec.
- The Users API mirrors this pattern via `users-api-vertical`; no other cross-module changes.
