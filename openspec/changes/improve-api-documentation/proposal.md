## Why

A verification pass of the archived initial requirements against the implementation found the solution **fully satisfies them** (single-object and batch ingestion both verified live, `unPublish` capital-P casing honored, the no-prior-publish corner case handled, delete hard-deletes while unpublish keeps data). The gaps are entirely in the **documentation layer**: the OpenAPI documents served by both APIs at `/openapi/v1.json` are out of sync with the implemented endpoints and too barebones for consumers — the `/cms/events` request body is undocumented entirely, success status codes are wrong (200 documented where 201/204 are returned), no failure modes or auth scheme are described, and neither the DocFX site nor the Scalar UI conveys acceptable requests, expected responses, their meaning, or their failure modes.

## What Changes

- **Correct and enrich the OpenAPI contract of the CMS Webhook API** (`/openapi/v1.json`):
  - `POST /cms/events`: add a request-body schema documenting both accepted forms (a single event object **or** a batch array) with per-field types and descriptions (`type` ∈ `publish|update|unPublish|delete` case-sensitive, `id`, `payload` object, `version ≥ 1`, ISO-8601 `timestamp`); correct the success response to `201 Created`; document `400` (invalid body/validation) and `401` (missing/invalid credentials).
- **Correct and enrich the OpenAPI contract of the Users API** (`/openapi/v1.json`):
  - `GET /entities`: add the response schema (array of items with `id`, `isVisibleByAdmin`, `latestVersion`, `updatedAt`, `payload`); document `200` and `401`.
  - `POST /entities/{id}/disable` and `/enable`: correct the success response to `204 No Content`; document `401`, `403` (non-administrator), and `404` (unknown id).
- **Add OpenAPI security metadata**: a `basic` security scheme in `components` and per-operation security requirements, so clients can see the auth contract.
- **Add request/response examples** to the operations and fix the `servers` entry scheme (https for the deployed/documented surface).
- **Add a conceptual API contract page** under `docs/` describing each endpoint: acceptable requests, expected responses, event-type semantics (including the unPublish corner case and delete vs unpublish), and failure modes; link it from the README and the DocFX `toc.yml`.
- **Extend the OpenAPI integration tests** to assert the corrected contract (status codes, presence of request/response schemas, security scheme), replacing the current presence-only assertions.

No endpoint behavior changes: the runtime behavior (single object and batch accepted, status codes, validation) is verified correct and stays as-is. This change only makes the documented contract truthful and usable.

## Capabilities

### New Capabilities

- `users-api`: the Users API currently has no OpenAPI-document requirement (the document is only mentioned in the auth carve-out); this change adds one, mirroring the CMS Webhook API's.

### Modified Capabilities

- `cms-webhook-api`: the existing "OpenAPI document" requirement is tightened from "describes the endpoints" to "describes them accurately" — correct status codes, request/response schemas, and a security scheme, staying in sync with the implemented endpoints.

## Impact

- `src/CmsWebhook/CmsWebhook.Api/Program.cs`, `src/CmsWebhook/CmsWebhook.Api/Endpoints/CmsEventEndpoints.cs` — OpenAPI metadata (request/response schemas, status codes, security, examples).
- `src/Users/Users.Api/Program.cs`, `src/Users/Users.Api/Endpoints/EntityEndpoints.cs` — same for the Users API.
- Possibly a small shared OpenAPI helper (e.g. `Shared/` or per-API document transformers) for the basic-auth security scheme and response descriptions.
- `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiOpenApiTests.cs`, `tests/Users/Users.Api.Tests/UsersApiOpenApiTests.cs` — contract-accuracy assertions.
- `docs/` — new API contract page; `toc.yml`, `README.md` — pointers.
- DocFX site regenerates from the same sources (no config change expected).
