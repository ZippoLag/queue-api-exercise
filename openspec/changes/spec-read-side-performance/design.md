## Context

The initial requirements' Performance section demands a read-only/writer configuration for the application context and optimized EF read queries (see `proposal.md` — Why). The implementation already satisfies it: the Users API registers one `UsersDbContext`; the read repository (`EfEntityQueryRepository`) runs every query with `AsNoTracking` and pushes the published-status filter into SQL, while the write repository (`EfEntityCommandRepository`) uses the default tracking context for the enable/disable commands. The CMS Webhook side uses a single writer context (`CmsDbContext`) for ingest and the outbox worker. `docs/architecture.md` §Performance documents the mechanics; what it does not yet state is *why* the read shape differs from the write shape and that full payloads are served by design.

## Goals / Non-Goals

**Goals:**
- Give the read-only/writer requirement a spec-level home so `openspec validate` and future changes treat it as contract, not folklore.
- Record the design intent: listings return full stored payloads (payloads are not editable through the API), and the read representation is deliberately not an exact match of the accepted event.
- Verify the read-only guarantee behaviorally with a no-tracking test.

**Non-Goals:**
- No change to the listing's payload behavior — full payloads remain, no projection, no pagination (the store is a single node and payloads are returned whole by design).
- No second/separate read-only `DbContext` type — the split stays per-query on the single context (see Decision 1).
- No benchmarks or load tests — the requirement asks for a documented, justified choice, which the async half already has.

## Decisions

**D1: Keep one `UsersDbContext` with per-query `AsNoTracking` for reads and default tracking for writes — do not introduce a separate read-only context.**

The CQRS split is already enforced by separate ports (`IEntityQueryRepository` vs `IEntityCommandRepository`); `AsNoTracking` on the read repository is the minimal mechanism that gives read-only semantics. Alternatives considered:
- *A dedicated read-only context* (`UseQueryTrackingBehavior(NoTracking)`, no `SaveChanges` surface): rejected — it would duplicate the `cms_entities` mapping, and the module already guards against mapping drift between `CmsDbContext` and `UsersDbContext` (see `UsersDbContext` remarks); one mapping, one source of truth.
- *Projection to the response DTO*: rejected — payloads are served whole by design (they are not meant to be edited), so stripping them would change the documented contract for no benefit.

**D2: The read shape intentionally differs from the write shape.**

`CmsEventRequest` (what the CMS sends) → `CmsEvent` (what is durably recorded) → `CmsEntity` (the processed state read by the Users API). The entity adds state the accepted event did not carry: the administrator-visibility flag (set only via the Users API) and the maintained update timestamp. The read model is therefore the *processed* truth, not an echo of the request — which is exactly why "read-only/writer" is about tracking vs. not tracking, not about identical schemas.

> **Timestamps — both clocks kept, one surfaced (confirmed decision, 2026-08-18):** the event log already keeps both `CmsEvent.Timestamp` (when the event happened in the CMS) and `CmsEvent.ReceivedAt` (when our system recorded it), plus `CmsEvent.ProcessedAt` (when processing finished) and the `Pending`/`Processed`/`Failed` status. The listing deliberately exposes only the external event time (`EntityListItem.UpdatedAt`, populated from the event's `timestamp` by `CmsEventProcessingRules`) — surfacing `receivedAt`/`processedAt` on `GET /entities` was considered and rejected for now: it is an API contract change with no consumer need identified. The spec phrases the field as "a maintained update timestamp" to stay accurate either way.

**D3: The async-processing half of the Performance requirement is already spec'd** (`cms-webhook-api/event-ingestion`: "Events are processed asynchronously"), so this change only fills the read-side gap.

**D4: The outbox worker's pending sweep stays a tracking query — do not add `AsNoTracking` in isolation (confirmed decision, 2026-08-18).**

The worker's `GetPendingAsync` (the outbox sweep) deliberately runs with default EF tracking. This is not an oversight: `EfCmsEventLogRepository.UpdateStatusAsync` advances each event via `FindAsync` + mutation + `SaveChanges`, and `FindAsync` first consults the change tracker, so it returns the already-loaded pending row **without a database round trip** — the tracking sweep is what makes the per-event status transition free. Making the sweep `AsNoTracking` in isolation would force `FindAsync` to fall through to the database, adding one `SELECT` per event — a regression, not an optimization. The requirement's "optimize your EF read queries" targets the API read side (`users-api`, already `AsNoTracking`); the sweep is part of the write pipeline on the single-writer context, where the documented design is one writer context. The sweep read is already optimized short of that: `WHERE status = Pending` pushed to SQL, FIFO `ORDER BY id`, the `cms_event_log(status)` index, and `ExistsProcessedAsync` is already non-tracking (`AnyAsync`).

> **Option 2 — revisit only when it becomes necessary** (many pending rows per sweep, or change-tracker memory pressure): switch the sweep to `AsNoTracking` **and** replace the load-then-mutate status transitions (`UpdateStatusAsync`) with `ExecuteUpdateAsync` — one UPDATE, no row load, joins the ambient per-event transaction; an unknown id affects zero rows and remains a silent no-op, preserving `MarkProcessed_WithUnknownEventId_DoesNothing`. Optionally reuse the `current` entity already loaded by `GetByIdAsync` inside `UpsertAsync` to remove the `GetByIdAsync` + `FindAsync` double-load of the entity row. Behavior is unchanged by this refactor, so the existing repository tests (which assert DB state, not tracker state) keep passing unchanged.

## Risks / Trade-offs

- [A future "optimization" adds a projection or strips payloads, silently changing the contract] → Mitigation: the spec explicitly requires full stored payloads, and the existing listing tests already assert payload content.
- [The spec names EF as the persistence technology while a provider-configuration change is in flight (`configurable-db-provider`)] → Mitigation: the requirement is phrased at the level of read-only/writer semantics ("not tracked", "single-writer", "no mutation"), which holds for any EF provider.
- [Non-tracking reads miss identity resolution for navigations] → Not applicable: the entity has no navigations; the listing maps to a flat DTO.
