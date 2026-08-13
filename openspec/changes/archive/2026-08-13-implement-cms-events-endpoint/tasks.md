## 1. Domain Layer

- [x] 1.1 Create `CmsEventType` enum with `publish`, `update`, `unPublish`, `delete` (spec: Validates and sanitizes events)
- [x] 1.2 Create `CmsRequest` model and its validation: required `type` (case-sensitive enum), non-empty `id`, ISO 8601/RFC 3339 parseable `timestamp`, `version ≥ 1` integer and JSON-object `payload` required for non-delete events, `delete` omits/ignores `payload` and `version` (design D6)
- [x] 1.3 Create `CmsEvent` (recorded event) and `CmsEntity` (stored entity: id, latest version, payload, published flag, admin-visibility flag, updated-at) domain models, following `docs/dsl_glossary.md` nomenclature
- [x] 1.4 Add unit tests in `CmsWebhook.Domain.Tests` covering every validation scenario in the spec (unknown/missing type, empty id, invalid timestamp, version < 1, non-object payload, missing payload, delete without payload/version, malformed JSON)

## 2. Application Layer

- [x] 2.1 Define repository/port interfaces (`ICmsEventLogRepository`, `ICmsEntityRepository`) in the Application layer
- [x] 2.2 Implement `IngestCmsEventsCommand` + handler: accepts single/batch, all-or-nothing persistence of validated events with status `Pending`, returns success/failure outcome for HTTP mapping (spec: Accepts a single event or a batch, Batch recording is atomic)
- [x] 2.3 Implement the `ICmsEventProcessor` contract and rule logic: create on unknown id, no-op on identical `id`+`version`+`type`, apply on different type for same id/version, ignore stale versions, hard-delete for `delete`, unpublish-always-applies corner case (spec: Processing follows the event business rules, Unpublish never loses the latest version, The entity store tracks the latest version)
- [x] 2.4 Add unit tests in `CmsWebhook.Application.Tests` for the ingest command (single, batch, atomicity, validation mapping) and for every processing rule including the same-version publish/unpublish transitions and the never-published unpublish corner case

## 3. Infrastructure Layer

- [x] 3.1 Create `src/CmsWebhook/CmsWebhook.Infrastructure` project and add it to `QueueApi.slnx`
- [x] 3.2 Implement `CmsDbContext` with `cms_event_log` (id, entity_id, event_type, version, payload_json, timestamp, received_at, status, error, processed_at) and `cms_entities` (id, latest_version, payload_json, is_published, is_visible_by_admin, updated_at) per design D8
- [x] 3.3 Implement the repositories with transactional per-event processing and status transitions (`Pending` → `Processed`/`Failed`), `Failed` recording the error (design D5)
- [x] 3.4 Implement `CmsEventProcessorWorker` (`BackgroundService`): `System.Threading.Channels` consumption, startup + periodic sweep of `Pending` rows, SQLite WAL + busy timeout on the CMS connection (design D2, D3)
- [x] 3.5 Add `tests/CmsWebhook/CmsWebhook.Infrastructure.Tests` (added to `QueueApi.slnx`): repository persistence/status transitions, worker sweep recovery, failure marking with processing continuing

## 4. API Layer

- [x] 4.1 Generalize the `Program.cs` connection-string resolution to serve both `ConnectionStrings:AuthDb` and `ConnectionStrings:CmsDb` (design D3)
- [x] 4.2 Wire DI in `CmsWebhook.Api`: `CmsDbContext`, repositories, ingest command handler, worker; add `ConnectionStrings:CmsDb` to `appsettings.json`
- [x] 4.3 Add the `POST /cms/events` minimal API handler: single or batch body, `201` on acceptance, `400` on any invalid input, inheriting the existing authentication/authorization policy (spec: Endpoint requires authentication)
- [x] 4.4 Add startup fail-fast for the CMS DB: `EnsureCreated()` + reachability check with a descriptive error, mirroring the auth pattern (design D8)
- [x] 4.5 Extend the API integration tests (`CmsWebhookApiFactory` swaps `CmsDbContext` to a temp store): 401/403 on the new endpoint, valid single/batch → 201, each validation failure → 400 with nothing recorded, batch atomicity, and event recorded as `Pending` then processed into `cms_entities`
- [x] 4.6 Update `README.md` onboarding: CMS database (`db/queue-cms.db`) auto-created, no extra init step beyond the existing auth `scripts/init-db.sh`

## 5. Documentation Updates

- [x] 5.1 Update `docs/architecture.md`: revise the payload bullet to plain JSON storage (design D1), document the outbox processing model (D2), the second database (D3), CQRS layer boundaries (D4), processing/status rules (D5), and resolve the performance/observability notes with the chosen async mechanism and logging approach (D7)
- [x] 5.2 Update `docs/dsl_glossary.md`: add entries for `CmsEventLog`, the outbox worker, and event-processing vocabulary per the decided design

## 6. Verification

- [x] 6.1 Run `dotnet build` on the solution and confirm it compiles without warnings
- [x] 6.2 Run the full test suite (`dotnet test`) and confirm all projects pass
