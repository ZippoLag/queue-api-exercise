## Why

The end-to-end smoke vertical — `tests/E2E/QueueApi.E2E.Tests` (in-process test host) and `scripts/smoke-e2e.sh` (real published processes over real SQLite), both run by the CI `end-to-end` job on every push — only exercises happy paths. The `request-sanitization` change altered both APIs' documented failure contract (strict RFC 3339 timestamps → `400`, non-object payload → `400`, Users API route-id validation → `400`/trimmed lookup) and shipped with zero E2E/smoke coverage; verifying that contract required a manual smoke test against running processes. Two structural causes: the `end-to-end` CI job has no spec requirement of its own (`ci-quality-gates` only mentions it inside the deploy requirement), so there is no stated contract for the vertical to drift from, and no rule requires contract changes to extend it.

## What Changes

- **In-process E2E suite** (`tests/E2E/QueueApi.E2E.Tests/EndToEndSmokeTests.cs`) — add rejection-path scenarios: non-RFC 3339 timestamp → `400` with the unique entity id never materializing on the Users API (cross-API proof nothing was recorded); non-object payload → `400` with nothing recorded; anonymous request → `401`; whitespace-only id → `400`; whitespace-padded id → trimmed lookup (`204`) and hidden from regular users; unknown id → `404`.
- **Real-process smoke** (`scripts/smoke-e2e.sh`) — the same assertions over real HTTP: `401` anonymous, `400` invalid timestamp, `400` non-object payload, `400` whitespace-only id, `204` padded-id trim, `404` unknown id, each with a unique entity id and a single-shot absence check (a rejected request never enters the pipeline, so no absence-polling and no flakiness).
- **Spec it** — add a `ci-quality-gates` requirement "End-to-end smoke gates cover the documented contract": the two-layer vertical (in-process host + real-process script) covers the acceptance path and every deterministic rejection path, contract changes extend it in the same change, and `429` is explicitly excluded (timing-sensitive, stays in the API integration suite).
- **Docs sync** — `docs/testing.md` describes the vertical's rejection coverage and the explicit `429` exclusion.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-quality-gates`: ADDED requirement "End-to-end smoke gates cover the documented contract" — the CI end-to-end job runs both smoke layers; the vertical SHALL exercise every deterministic documented rejection path of both APIs (401, the 400s, 403, 404); a change that alters a documented status code or request/response contract SHALL extend the vertical in the same change; and timing-sensitive behavior — the ingestion rate limiter's `429` — SHALL NOT be asserted in the smoke layers (it stays in the deterministic API integration suite).

## Impact

- **Code**: `tests/E2E/QueueApi.E2E.Tests/EndToEndSmokeTests.cs` (new scenarios), `scripts/smoke-e2e.sh` (rejection assertions + header comment).
- **Docs**: `docs/testing.md` (E2E section describes rejection coverage and the 429 exclusion).
- **Tests run**: the E2E project (`dotnet test tests/E2E/QueueApi.E2E.Tests/QueueApi.E2E.Tests.csproj`) and `scripts/smoke-e2e.sh` locally; CI runs them on every push.
- **Out of scope**: rate-limiter `429` assertions in the smoke layers (timing-sensitive; deterministically covered by `CmsWebhookApiEventIngestionTests` with an overridden permit limit); the live AWS deployment verification (`deploy-aws.sh` `verify_live` — it keeps its existing health + happy-path smoke flow; touching a real environment is out of scope); any change to the unit/API-integration suites or the coverage ratchet; the archived specs.
