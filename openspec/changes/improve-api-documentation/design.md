## Context

Both APIs generate their OpenAPI document with `builder.Services.AddOpenApi(...)` (Microsoft.AspNetCore.OpenApi 9) and serve it at `/openapi/v1.json` with the Scalar UI at `/scalar/v1`. Today the endpoint metadata is only `WithSummary`/`WithDescription` — so the generator emits a default `200 OK` response for every operation and **no request/response schemas** (the handlers return `IResult` and the ingestion handler parses `HttpRequest` manually, so nothing is inferred). Verified against the live deployment:

| Endpoint | Runtime behavior (verified) | Documented in OpenAPI |
|---|---|---|
| `POST /cms/events` | 201 single object, 201 batch array, 400 invalid, 401 unauth | 200 only; no requestBody; no 400/401 |
| `GET /entities` | 200 list, 401 unauth | 200 only; no response schema; no 401 |
| `POST /entities/{id}/disable` | 204, 401, 403, 404 | 200 only |
| `POST /entities/{id}/enable` | 204, 401, 403, 404 | 200 only |

`components` is empty — no security scheme anywhere. The archived initial requirements and the implemented behavior are both correct; only the contract is wrong/barebones.

## Decisions

### D1: Declare the ingestion request body with a `oneOf` schema in a document transformer

The `POST /cms/events` handler parses `HttpRequest.Body` manually (single object or batch array), so the generator cannot infer a schema. A document transformer on the CMS API's `AddOpenApi` options sets the operation's `RequestBody` to a schema with `oneOf`:

- one branch: a single event object (`type` enum `publish|update|unPublish|delete` — exact case, `id` string, `payload` object, `version` integer ≥ 1, `timestamp` string ISO-8601; `payload`/`version` required except for `delete`);
- the other branch: an array of that object.

This states the dual acceptance truthfully (both verified live as 201) instead of documenting only the archived schema's array form. Rationale: a `oneOf` is the OpenAPI-native way to express "either shape"; a schema defined inline in the transformer keeps the DTOs (`CmsRequest`) free of OpenAPI concerns, consistent with the domain owning transport shapes only.

### D2: Correct response metadata per operation

Handlers return `IResult`, so status codes must be declared explicitly:

- CMS `POST /cms/events`: `201` (description: accepted and recorded in the outbox), `400` (unparseable JSON, unknown `type`, missing/invalid fields), `401` (missing/invalid credentials).
- Users `GET /entities`: `200` with `Produces<IReadOnlyList<EntityListItem>>`-equivalent response schema (array items: `id`, `isVisibleByAdmin`, `latestVersion`, `updatedAt`, `payload`), `401`.
- Users `POST /entities/{id}/disable|enable`: `204` (idempotent success), `401`, `403` (authenticated non-administrator), `404` (unknown id).

Rationale: the previous "200 OK" default was generated, not designed — it lied about both 201/204 endpoints. Explicit declarations make the document the source of truth for consumers, and the integration tests (D5) pin them so drift fails CI.

### D3: One shared basic-auth security scheme per API

Each API's document transformer adds `components.securitySchemes.basic` (`type: http`, `scheme: basic`) and applies `security: [{ basic: [] }]` to every operation except `/health`, `/openapi/v1.json` and the Scalar UI route (the documented anonymous carve-out). The users-api enable/disable operations already carry the `AdministratorOnly` policy — the OpenAPI security requirement stays `basic` (the role distinction is described in the operation description and the conceptual docs, matching the existing docs' voice).

### D4: `servers` entry reflects the request scheme

The generator derives the `servers` URL from the request; when the document is fetched over HTTPS it must emit `https://…` (the live fetch showed `http://` even over https — verify at implementation and, if the generator hardcodes the scheme, add a server transformer emitting the request's `X-Forwarded-Proto`/`Scheme`). This is cosmetic for consumers but avoids docs that point at plain HTTP for a TLS-only deployment.

### D5: Extend the OpenAPI integration tests to pin the contract

Replace the presence-only assertions in `CmsWebhookApiOpenApiTests` and `UsersApiOpenApiTests` with contract assertions that parse the served document and verify, per endpoint: the exact response status codes (201/204/400/401/403/404 as applicable), the presence and shape of the `/cms/events` requestBody `oneOf` (single-object branch and array branch, `type` enum values incl. `unPublish`), the `/entities` response schema fields, and the basic security scheme. Rationale: the OpenAPI-document requirements (spec deltas) are only machine-enforced if tests assert them; the existing tests only checked that endpoints exist, which is why the drift went unnoticed.

### D6: A conceptual API contract page in `docs/`

New `docs/api-contract.md` owns the human-readable endpoint contract: per endpoint — acceptable requests (including the single-object-or-batch forms), expected responses with status codes, the meaning of each event type (`publish`/`update`/`unPublish`/`delete`, the capital-P wire value, the no-prior-publish corner case, delete = hard delete vs unPublish = hidden), role behavior (regular vs administrator vs `cms-webhook`), and failure modes (400/401/403/404 semantics). Wired into the DocFX `toc.yml` and the README documentation list; the DocFX build picks it up automatically via the existing `docs/**/*.md` glob. The OpenAPI/Scalar UI remains the machine contract; this page is the narrative companion — no duplication of field tables that live in the OpenAPI schemas (one fact, one home: the page links to `/openapi/v1.json` and the Scalar UI).

## Open Questions

- Whether the generator's `servers` entry already honors the request scheme once metadata is fixed — resolved during implementation (D4).
