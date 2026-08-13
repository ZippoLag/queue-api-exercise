## Why

The CMS webhook pipeline is the core deliverable of the initial requirements but remains unimplemented: `CmsWebhook.Api` exposes only authentication plus a placeholder `GET /`, and `CmsWebhook.Domain`/`CmsWebhook.Application` are empty stubs. `docs/architecture.md` defines the endpoint, event types, validations and processing rules, but left the processing model ambiguous (events applied synchronously on arrival vs merely logged). This change delivers the full `POST /cms/events` vertical — validated ingestion into its own database, immediate-but-asynchronous processing into the entity store, and full test coverage — with the ambiguity resolved (in-process outbox), and records every decision in `docs/architecture.md` and `docs/dsl_glossary.md`.

## What Changes

- Add `POST /cms/events` to `CmsWebhook.Api`, requiring Basic Auth through the existing shared `QueueApi.Auth` store (only the reserved cms user is authorized — unchanged behavior).
- Accept a single **CmsRequest** object or a batch array of them. Validate and sanitize per the architecture: required fields, `type` ∈ `{publish, update, unPublish, delete}`, `version ≥ 1`, ISO 8601/RFC 3339 `timestamp`, `payload` a valid JSON object. `delete` events omit `payload`/`version`. A batch is all-or-nothing: any invalid event → `400`, nothing persisted; otherwise `201`.
- Persist every accepted **CmsEvent** as received into a `cms_event_log` table in a **new, dedicated CMS database** (`db/queue-cms.db`, configured via `ConnectionStrings:CmsDb`), independent of the shared auth credential store.
- Process events **immediately but asynchronously**: an in-process `BackgroundService` (outbox worker) consumes recorded events via `System.Threading.Channels` with a startup/periodic sweep of unprocessed rows for durability, and applies the architecture's event-processing rules to maintain a `cms_entities` table (latest version, payload, published flag, admin-visibility flag). No external broker — decision D2.
- Store payloads as plain JSON text in the private CMS DB (decision D1: encryption-at-rest dropped for v1; the architecture's encryption paragraph is revised accordingly).
- Follow **strict CQRS and clean architecture**: write side only — a command handler in the Application layer, repositories + EF Core `CmsDbContext` + the outbox worker in a new `CmsWebhook.Infrastructure` project. The read side (User API) is deferred.
- Log every processed event and every failing event (observability requirement from the initial requirements), continuing the existing leveled console logging.
- Update `docs/architecture.md` (processing model, entity store, second DB, CQRS boundaries, no-encryption decision) and `docs/dsl_glossary.md` (new terms: `CmsEventLog`, `CmsEntity` processing vocabulary, outbox worker).
- **Out of scope (deferred):** Users/entities REST APIs, administrator and regular-test-user seeding, payload encryption/key vault, external message broker, OTEL/Serilog.

## Capabilities

### New Capabilities

- `cms-webhook-api/event-ingestion`: the `POST /cms/events` endpoint, its request validation/sanitization, outbox storage of received **CmsEvent**s in a dedicated CMS database, immediate-but-asynchronous processing into the `cms_entities` store, and the observability (logging) of processed and failing events.

### Modified Capabilities

None — the existing `cms-webhook-api` auth requirements and the `auth` capability are unchanged.

## Impact

- **New projects:** `src/CmsWebhook/CmsWebhook.Infrastructure` (EF Core `CmsDbContext`, repositories, outbox worker), plus `tests/CmsWebhook/CmsWebhook.Infrastructure.Tests`. Added to `QueueApi.slnx`.
- **`src/CmsWebhook/CmsWebhook.Domain`:** `CmsEvent`/`CmsRequest`/`CmsEntity` models, `CmsEventType` enum, validation.
- **`src/CmsWebhook/CmsWebhook.Application`:** ingest command/handler (write side), processing service contract, repository interfaces.
- **`src/CmsWebhook/CmsWebhook.Api`:** endpoint mapping, DI wiring, `ConnectionStrings:CmsDb` resolution (reusing the existing repo-root resolution), fail-fast startup for the CMS DB.
- **Config:** new `ConnectionStrings:CmsDb` (default `Data Source=db/queue-cms.db`); `db/` folder already tracked.
- **Dependencies:** `System.Threading.Channels` (BCL) and `Microsoft.EntityFrameworkCore.Sqlite` (already used by `QueueApi.Auth`). No new third-party packages.
- **Tests:** domain validation, application command/processing rules (including the unpublish corner case and the same-`id`/`version` different-`type` transitions), infrastructure worker/repository behavior, and API integration tests via the existing `WebApplicationFactory` pattern.
- **Docs:** `docs/architecture.md`, `docs/dsl_glossary.md`, `README.md` (CMS DB onboarding).
