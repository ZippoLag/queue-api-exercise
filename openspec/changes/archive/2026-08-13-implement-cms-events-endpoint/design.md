## Context

See proposal.md - Why. Current state that shapes the approach:

- `CmsWebhook.Api` is auth-only: Basic Auth from the shared `QueueApi.Auth` SQLite credential store (`db/queue-auth.db`, `ConnectionStrings:AuthDb`), a fallback authorization policy (authenticated + reserved cms user), a fail-fast startup check, and a placeholder `GET /`.
- `CmsWebhook.Domain` and `CmsWebhook.Application` are empty stubs; there is no Infrastructure project. `docs/architecture.md` defines the endpoint, event types, validations and processing rules, and was recently corrected (dedup keyed on `id`+`version`+`type`, version starts at `1`, the unpublish corner case, delete omits payload/version, sanitization, observability, performance notes).
- Conventions (from archived changes `add-sqlite-auth-db`, `add-basic-auth`): EF Core + SQLite, dependency-averse (KISS), provider-neutral EF, connection strings resolved against the repo root via the `QueueApi.slnx` marker, no migrations tooling, fail-fast startup, xUnit + Moq + FluentAssertions, XML docs with spec citations, WebApplicationFactory-based integration tests swapping the DB context.
- The user resolved the two open forks: payloads are **not** encrypted in v1, and processing is an **in-process outbox** (no external broker).

## Goals / Non-Goals

**Goals:**
- `POST /cms/events` vertical: validated ingestion → durable outbox row → immediate-but-asynchronous processing into a `cms_entities` store, with full test coverage.
- Strict CQRS and clean-architecture boundaries: write side in Application/Domain/Infrastructure, HTTP in Api; read side cleanly deferred.
- Zero new third-party dependencies (`System.Threading.Channels` and `Microsoft.EntityFrameworkCore.Sqlite` are BCL/already-used).
- Dedicated CMS database independent from the auth credential store; fail-fast startup mirroring the auth pattern.
- Decisions recorded in `docs/architecture.md` and `docs/dsl_glossary.md`.

**Non-Goals:**
- Users/entities REST APIs and administrator/regular-test-user seeding (deferred).
- Payload encryption/key vault, external message broker, OTEL, Serilog, migrations infrastructure.

## Decisions

### D1: Payloads are stored as plain JSON text — encryption dropped for v1

`docs/architecture.md` previously committed to encrypting payloads at rest with an environment-variable key. This change reverses that: `payload` is stored as JSON text in the private CMS database.

**Rationale:** the initial requirements demand confidentiality (data must not be served publicly) — satisfied by authentication plus the private DB — but never request encryption at rest. The archived `silence-dataprotection-warning` change established that this API deliberately holds no protected data; adding encryption would reverse that stance and introduce key management (key loss = data loss, rotation, no vault) for no stated requirement. The architecture doc's payload bullet is revised during apply.
**Alternatives considered:** AES-GCM with a base64 env-var key (rejected: key-management burden, contradicts the no-protected-data decision); ASP.NET Core Data Protection (rejected: needs a persisted key ring — the exact devcontainer problem `silence-dataprotection-warning` works around).

### D2: In-process outbox — endpoint writes, a hosted worker processes

The endpoint records accepted **CmsEvent**s to `cms_event_log` (status `Pending`) and returns `201`. A `BackgroundService` (`CmsEventProcessorWorker`) consumes and processes them. The database is the durable queue; an unbounded `System.Threading.Channels.Channel<long>` (BCL) is a notification fast-path: after each successful insert the endpoint offers the event id, and the worker processes it immediately. A startup sweep plus a periodic sweep (every few seconds) re-processes any rows still `Pending` (crash between commit and channel offer, restart with pending rows).

**Rationale:** single-node SQLite modular monolith, one producer, simple requirements — the outbox table already is the durable queue and survives restarts, which satisfies "immediately but asynchronously" (fast-path) and the spec's "eventually complete for every recorded event" (sweeps). No external infrastructure is justified.
**Alternatives considered:** RabbitMQ + MassTransit (rejected: real infra/ops cost, no consumer topology, single node, and the user's recommendation was confirmed); Marten (rejected outright: Postgres-only, incompatible with the SQLite persistence); polling-only (rejected as the sole mechanism: latency conflicts with "immediately"; retained as the durability fallback).

### D3: Dedicated CMS database via `ConnectionStrings:CmsDb`

New config key `ConnectionStrings:CmsDb` defaulting to `Data Source=db/queue-cms.db`, resolved against the repo root exactly like `AuthDb` (the connection-string resolution helper in `Program.cs` is generalized to handle both keys). A new `CmsDbContext` (EF Core, SQLite) owns `cms_event_log` and `cms_entities`. SQLite WAL mode and a busy timeout are set on the CMS connection so the endpoint's writes and the worker's writes do not contend (SQLite is single-writer).

**Rationale:** the user requires CmsEvents in their own db, and it keeps the shared auth store untouched.
**Alternative considered:** one DB file for everything (rejected: user requirement + separation of concerns).

### D4: Strict CQRS and clean architecture with a new Infrastructure project

New project `src/CmsWebhook/CmsWebhook.Infrastructure` (added to `QueueApi.slnx`) holds EF Core, repositories and the outbox worker. `CmsWebhook.Application` owns the write side: an ingest command (`IngestCmsEventsCommand`) with a handler and an `ICmsEventProcessor` contract, depending only on Domain interfaces. `CmsWebhook.Domain` owns `CmsRequest`/`CmsEvent`/`CmsEntity` models, the `CmsEventType` enum, and request validation. `CmsWebhook.Api` owns HTTP, DI, and config only. There is no read path yet — the query side arrives with the deferred User API, which is why the entity store is maintained now.

**Rationale:** strict CQRS = separate command and (future) query paths; a single command does not warrant a dispatcher library. The repo is dependency-averse.
**Alternatives considered:** MediatR (rejected: a dependency for one command; adopt later if the command/query surface grows); EF Core directly in Application (rejected: violates clean architecture).

### D5: Processing rules and failure policy

The worker applies, per event, the architecture's rules now in the spec: create-on-unknown-id (`publish`/`update`/`unPublish`); no-op when the same `id`+`version`+`type` was already recorded (idempotent re-delivery); apply when a different `type` arrives for the same `id`+`version` (flag flips); ignore events with `version` older than the entity's current version (stale/out-of-order); `delete` hard-deletes and is a no-op for unknown ids; `unPublish` always applies (the never-published corner case stores payload+version and marks not published). Each event is processed in a single transaction: load entity, apply, mark event `Processed`. A thrown error marks the event `Failed` with the error recorded, logs it at Error, and processing continues with the next event. `Failed` events are **not** retried automatically (startup/periodic sweeps only re-process `Pending` rows).

**Rationale:** the dedup/flag-flip distinction is the corrected architecture rule; the stale-version rule is required for the "keep track of the latest data version" requirement; no-auto-retry avoids a poison-message loop in a single local worker.
**Alternatives considered:** retry with backoff (rejected: poison risk and complexity; failed events are preserved for investigation).

### D6: Endpoint shape and validation mapping

Minimal API `MapPost("/cms/events", ...)` handler: deserialize the body as either a single **CmsRequest** or an array (all-or-nothing batch); Domain validation (type enum case-sensitive, non-empty `id`, ISO 8601/RFC 3339 parseable `timestamp`, `version ≥ 1` integer and `payload` JSON object required except `delete`, which ignores them); any invalid event → `400 Bad Request` with nothing persisted; otherwise persist all and return `201 Created`. The existing `GET /` placeholder remains.

**Rationale:** matches the repo's minimal-API style; Domain-owned validation is unit-testable in `CmsWebhook.Domain.Tests`.
**Alternative considered:** controllers (rejected: repo uses minimal APIs).

### D7: Observability stays on leveled console logging

Continue the existing `Console` leveled logging: every processed event logged at Information (entity id, type, version, outcome), recoverable edge cases (stale events, dedup no-ops) at Warning, failing events at Error with the exception. No OTEL (single process, no distributed traces to follow) and no Serilog (marginal benefit at this scale); both noted as future options in the architecture doc.

**Alternative considered:** adding Serilog now (rejected: dependency with little benefit for one endpoint + one worker).

### D8: Schema, without migrations

`cms_event_log`: `id` (PK), `entity_id`, `event_type`, `version` (nullable), `payload_json` (nullable), `timestamp`, `received_at`, `status` (`Pending`/`Processed`/`Failed`), `error` (nullable), `processed_at` (nullable). `cms_entities`: `id` (entityId, PK), `latest_version`, `payload_json`, `is_published`, `is_visible_by_admin` (default true, ready for the deferred User API), `updated_at`. The outbox records every accepted delivery — it is an audit log, so no unique index dedups inserts; dedup happens in processing (D5). Schema is created at startup via `EnsureCreated()` plus a reachability check, mirroring the auth fail-fast pattern (no migrations tooling is a standing convention).

**Rationale:** consistent with the existing no-migrations stance; nothing to seed in the CMS DB.
**Alternative considered:** extending `tools/AuthDbInit` (rejected: no seeds needed; `EnsureCreated` is standard EF and keeps schema creation with the API).

## Risks / Trade-offs

- [SQLite single-writer contention between endpoint and worker] → WAL mode + busy timeout on the CMS connection (D3); volumes are small.
- [Channel notification lost (crash between commit and offer) leaves events `Pending`] → startup + periodic sweeps guarantee eventual processing (D2).
- [Failed events are never retried automatically] → deliberate (D5): no poison loop; failures are preserved and logged for investigation; a retry policy can be layered on later.
- [Plaintext payloads if the DB file leaks] → accepted for v1 (D1): private file, local dev; revisit encryption with a real engine/key vault.
- [`EnsureCreated` cannot evolve the schema later] → accepted (D8); a real DB engine brings migrations, matching the provider-swap stance.
- [Worker lives in the API process] → acceptable for a single node; processing is in Infrastructure, so extraction to a separate host later is a DI change.
- [Stale events (older version) are silently ignored] → required for latest-version correctness (D5); logged at Warning.

## Migration Plan

1. Domain: models, `CmsEventType`, validation + unit tests (`CmsWebhook.Domain.Tests`).
2. Application: ingest command/handler, processor contract + tests (`CmsWebhook.Application.Tests`).
3. New `CmsWebhook.Infrastructure`: `CmsDbContext`, repositories, `CmsEventProcessorWorker`, connection-string/busy-timeout setup + tests (new test project).
4. Api: `POST /cms/events` mapping, DI wiring, `ConnectionStrings:CmsDb` resolution, startup `EnsureCreated` + fail-fast, `README.md` onboarding for the CMS DB.
5. Docs: revise `docs/architecture.md` (processing model D2, second DB D3, entity store D5/D8, CQRS D4, no-encryption D1, performance note resolved) and extend `docs/dsl_glossary.md` (CmsEventLog, outbox/processing vocabulary).
- **Rollback:** revert the change — the API returns to auth-only; the new `db/queue-cms.db` is a fresh file and the auth DB is untouched.

## Open Questions

None — the two material forks (encryption, async mechanism) were resolved with the user, and the remaining unknowns (retry policy, sweep interval tuning, later broker extraction) are recorded as risks/alternatives that do not change the specs, approach, or task breakdown.
