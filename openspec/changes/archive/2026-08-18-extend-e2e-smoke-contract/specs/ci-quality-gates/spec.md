## ADDED Requirements

### Requirement: End-to-end smoke gates cover the documented contract

The CI workflow SHALL run an end-to-end smoke gate that exercises both APIs over one shared store in two layers — an in-process test host (`tests/E2E/QueueApi.E2E.Tests`) and real published processes over real SQLite files (`scripts/smoke-e2e.sh`) — and the gate SHALL fail when either layer fails. The smoke vertical SHALL cover the documented contract of both APIs: the acceptance path (ingest → outbox processing → listing → administrator visibility control) and every deterministic rejection path — `401` for a request without credentials, `400` for a timestamp that is not an ISO 8601 / RFC 3339 date-time, `400` for a payload that is not a JSON object, `400` for an empty or whitespace-only route id, `403` for valid credentials of a user not authorized on the API, and `404` for an unknown entity id. A rejected request SHALL be shown to record nothing: its unique entity id never appears on the Users API listing. A change that adds or modifies a documented status code or request/response contract of either API SHALL extend this smoke vertical in the same change. Timing-sensitive behaviors — notably the ingestion rate limiter's `429` — SHALL NOT be asserted in the smoke layers; they remain covered by the deterministic API integration suite.

#### Scenario: Smoke gates run on every push

- **WHEN** a push or pull request reaches the `end-to-end` CI job
- **THEN** both layers run — the in-process E2E project and the real-process smoke script — and the job fails when either layer fails

#### Scenario: Rejection paths are asserted

- **WHEN** the smoke vertical sends a non-RFC 3339 timestamp or a non-object payload to the ingestion endpoint, an anonymous request to a protected endpoint, or an empty or whitespace-only id to the enable/disable endpoints
- **THEN** the responses are the documented `401`/`400` statuses and the rejected unique entity id never appears on the Users API listing

#### Scenario: Contract changes extend the vertical

- **WHEN** a change adds or modifies a documented status code or request/response contract of either API
- **THEN** the change's tasks include extending the smoke vertical with assertions for the new or changed behavior

#### Scenario: Rate limiting stays out of the smoke layers

- **WHEN** the smoke vertical is executed
- **THEN** it never asserts on the rate limiter's `429` response, which remains covered by the API integration suite with an overridden permit limit
