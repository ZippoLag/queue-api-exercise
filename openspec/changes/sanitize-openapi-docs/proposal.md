## Why

The OpenAPI documents both APIs serve anonymously at `/openapi/v1.json` are public contracts, but their response/scheme descriptions currently expose internal implementation details. The clearest example is the Users API `GET /entities` 403 description: *"The caller is the reserved cms-webhook user, which is not authorized on this API."* — it names the concrete reserved machine account, revealing the cross-API CMS integration to anyone who reads the contract. The same document also advertises the shared credential store and the outbox pattern, which are architecture facts consumers do not need and should not be able to learn from a public contract.

The CMS Webhook API contract is also *inaccurate*: the runtime rejects valid credentials of any user other than the cms user with `403`, but the served document for `POST /cms/events` only lists `201`/`400`/`401`. And the ingestion endpoint has no protection against traffic floods, leaving the shared store open to abuse.

## What Changes

- **Users API `GET /entities` 403 description** — replace *"The caller is the reserved cms-webhook user, which is not authorized on this API."* with generic authorization wording: *"The caller is authenticated but not authorized on this API."* (parallels the enable/disable 403 "The caller is not the administrator.").
- **Basic security scheme description (both APIs)** — replace *"HTTP Basic authentication against the shared credential store."* with generic wording, e.g. *"HTTP Basic authentication with a valid username and password."*, so the contract no longer reveals that the APIs share a credential store.
- **CMS Webhook API `POST /cms/events`** — replace *"records them in the outbox"* / *"recorded in the outbox"* with processing-oriented wording, e.g. *"records them for processing"* / *"recorded for processing"*, so the contract no longer reveals the outbox architecture.
- **CMS Webhook API OpenAPI documents the real `403`** — add a `403 Forbidden` response to the `POST /cms/events` contract with generic wording (valid credentials of a user not authorized on this API), so the document matches the implemented authorization behavior instead of omitting the failure mode.
- **Rate limiting on the CMS Webhook API ingestion endpoint** — protect `POST /cms/events` with ASP.NET Core's standard rate-limiting middleware: a configurable fixed-window policy (per-window request limit), applied only to the ingestion endpoint (the anonymous `/health`, `/openapi/v1.json` and `/scalar/v1` stay exempt), rejecting excess requests with `429 Too Many Requests`.
- **Regression test** — assert the served OpenAPI documents never contain the reserved username `cms-webhook` or the implementation phrases above, that the CMS Webhook document lists the `403`, and that the rate limiter rejects excess ingestion requests with `429` while leaving the discovery endpoints exempt.
- **Docs sync** — update `docs/api-contract.md` rows that mirror the reworded descriptions and the new `403`/`429` responses (the narrative "Authentication" table that explains the reserved-user model stays, as it is internal documentation of the design); document the rate-limiting settings in `docs/configuration.md`.

No runtime authorization changes: the auth policies, handlers, and status codes are untouched — only the text served in the OpenAPI documents, the added `403` documentation, the new rate-limiting behavior, and the matching narrative rows.

## Capabilities

### New Capabilities

- `rate-limiting`: the CMS Webhook API ingestion endpoint SHALL be rate limited with a configurable fixed-window policy; excess requests SHALL be rejected with `429 Too Many Requests` and the anonymous discovery endpoints SHALL remain exempt.

### Modified Capabilities

- `users-api`: the "OpenAPI document" requirement gains the constraint that the served contract SHALL NOT disclose implementation details such as the reserved `cms-webhook` username or the shared credential store — the 403 description and security scheme description must use generic wording.
- `cms-webhook-api`: the "OpenAPI document" requirement gains the constraint that the served contract SHALL NOT disclose internal implementation details — specifically the outbox — in its operation descriptions, and SHALL document the real `403 Forbidden` response of `POST /cms/events` in generic wording.

## Impact

- **Code**: `src/Users/Users.Api/Endpoints/EntityEndpoints.cs` (403 description), `src/Users/Users.Api/Program.cs` and `src/CmsWebhook/CmsWebhook.Api/Program.cs` (Basic scheme description, rate limiter registration + middleware), `src/CmsWebhook/CmsWebhook.Api/Endpoints/CmsEventEndpoints.cs` (outbox wording in summary/201 description, new 403 response), `src/CmsWebhook/CmsWebhook.Api/appsettings.json` (rate-limiting settings).
- **Tests**: `tests/Users/Users.Api.Tests/UsersApiOpenApiTests.cs`, `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiOpenApiTests.cs` (leak scan + 403 presence), `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiEventIngestionTests.cs` (429 behavior, discovery endpoints exempt).
- **Docs**: `docs/api-contract.md` (403/429 rows and mirrored wording), `docs/configuration.md` (rate-limiting configuration keys and defaults).
- **Out of scope**: internal docs (`docs/architecture.md`, `docs/dsl_glossary.md`) that explain the reserved-user and outbox design on purpose; runtime error responses (the auth middleware returns empty 401/403 bodies with no message to sanitize); rate limiting on the Users API or on the anonymous discovery endpoints.
