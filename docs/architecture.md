# Architecture

## Approach
In tandem of the KISS principle, it would be an oversight in my years of experience to not treat this project as if it had plans to grow in the future, meaning I will aim to keep a clear separation of boundaries and domains within a Modular Monolith, following a Ports+Adapters and Clean architecture. Then, given the fact that from the start there are requirements for event handling and distinct flows (CMS VS Users), following an Event-Driven architecture (not Event-Sourcing for now) with CQRS also in place feels natural. Observability via logging and possibly OTEL will be approached as soon as justified.

To get usable value ASAP, I will focus on implementing visible API implementation first, adding inner domain and infrastructure (and simple UI?) later as needed.

## System Overview

![alt text](system_overview.png)

The Queue-API-Exercise system is meant to have 2 REST APIs available: a webhook for handling CMS entity-related events and one to handle Users and Admin Users requests. Knowing this project may grow, I choose to pay the cost of an initial scaffolding big-bang with boilerplate and creating the solution as a modular monolith, ready to be split whenever necessary.

**Current implementation status:** the **CmsWebhook API** is fully implemented for v1 — shared auth, the `/cms/events` ingestion endpoint, and the asynchronous outbox processing into the entity store (see below). The **User API** (read side) and administrator/user seeding are still planned.

### Authentication & Authorization
Authentication is handled in the CmsWebhook API as Basic Auth (`username`+`password`) in all incoming requests. The mechanism is implemented once in the shared `QueueApi.Auth` library (`src/Shared/QueueApi.Auth`) so the future User API reuses the same scheme and store.

- `username` [10,20] characters in length, no other constraints. The reserved cms username is read from the `Auth:CmsUsername` configuration value (default `cms-webhook`) and the application fails to start if the configured value violates the length rule.
- Credentials are **not** hardcoded: they live in the shared SQLite credential store and are verified against a stored **PBKDF2 hash** (per-user random salt). Plaintext passwords are never persisted. The store is provisioned idempotently by `scripts/init-db.sh` (username and password passed as positional arguments); the API fails to start with a descriptive error if the store is unreachable or has not been initialized with the cms user.

> Note: `"cms-webhook"` is a special username reserved to be used by the CMS when connecting to the CMS API. It is the **only** username authorized to access the CMS API — valid credentials of any other user are rejected with `403`, all other failures with `401`. It is not valid for the Users API. `"administrator"` is a special username reserved to be used by the system administrator in the future Users API.

> Note: no signature verification is provided in current version

> Note: every endpoint requires Basic auth **except** the anonymous `/health` liveness probe and the `/openapi/v1.json` contract endpoint. Both are marked `.AllowAnonymous()` so load balancers, orchestrators, and clients can probe/discover them without credentials; every other endpoint still rejects anonymous requests with `401`.

### Persistence
Persistence uses `sqlite` relational databases accessed via **EF Core** (per the initial requirements). Two independent stores exist, each configurable through its own `ConnectionStrings` value (e.g. via environment variables) so it can point elsewhere — or at another engine via an EF Core provider swap — without code changes:

- **Implemented:** the shared credential store (`db/queue-auth.db`, `Users` table) holding username + PBKDF2 password hash per user, configured via `ConnectionStrings:AuthDb` and provisioned idempotently by `scripts/init-db.sh`.
- **Implemented:** the dedicated CMS database (`db/queue-cms.db`) holding the `cms_event_log` outbox and the `cms_entities` store, configured via `ConnectionStrings:CmsDb` and created automatically at startup (`EnsureCreated`, no init step). SQLite WAL journal mode and a busy timeout are enabled so the webhook's writes and the outbox worker's writes coexist on the single-writer file.

Relative `Data Source=` values resolve against the configured `Data:DbBasePath`, falling back to the application's content root — in local development the web project's content root is its own directory, so the stores land under `src/CmsWebhook/CmsWebhook.Api/db/`. Absolute and `:memory:` data sources are used as-is, and the resolved directory is created at startup when missing. There is **no repository-marker walk** (the `QueueApi.slnx` hunt is gone): a published deployment simply points `Data__DbBasePath` (or `ConnectionStrings__*`) at a writable location through environment variables — see [Configuration](configuration.md).

Caching is out of scope.

### Performance
Per the initial requirements, the event-processing decision is documented and justified: processing is **asynchronous** — the webhook records events and responds `201` immediately, while a background worker applies them (see the outbox model below). The write side uses a single writer context. The read-only/query-optimized EF configuration applies to the read side (User API), which is still planned and will optimize its EF read queries then.

### Logging
Leveled console output (`Console` with explicit levels) is used; richer logging (e.g. Serilog) and OTEL remain TBD. Per the initial requirements' observability section, all processed events — including failing ones — are logged: processed events at Information, stale/duplicate events at Warning, failures at Error with their exception.

## CMS Webhook API - v1
The `/cms/events` endpoint, event types, validations and event processing below describe the **implemented** v1 behavior.

The **CmsWebhook** is intended to be a _webhooks API_ so it needs a quick response to the external system that's _notifying_ us of already-happened events. All **CmsEvent**s received will be stored in a `cms_event_log` table.

> For the current **v1**, validations will be minimal. Whether **v2** will incorporate more complex validations in these endpoints or push this logic to async workers is TBD.

### `/cms/events` Request POST
Endpoint which handles the `POST` operation and expects the following **CmsRequest** as `json` object as body:
```json
{ "type": "eventType", "id": "entityId", "payload": {...}, "version": 1, "timestamp": "2024-01-01T00:00:00Z"}
```

> Note: All fields are required (not null and with a valid value), except `payload` and `version` which `delete` events omit (see the batch example below).

#### Fields:
- `id`: `string` entity's id.
- `payload`: `json` object which contains the actual data for the **CmsEntity** which needs to be fed into our system's DB. Payloads are stored as plain JSON text in the private CMS database — design decision: confidentiality is enforced by authentication and the private store (per the initial requirements); encryption-at-rest and a key vault are deferred until a real database engine is adopted.
- `type`: `string` operation performed upon the entity
- `timestamp`: `string` ISO 8601 (aka RFC 3339) date-time information of _when_ the event happened in the external CMS
- `version`: `int` version number coming from the external system; the first version of an entity is `1` (per the initial requirements)

> Note: Alternatively, the body may consist of an array of the same kind of objects, e.g.:
>```json
>[
> { "type": "publish", "id": "X", "payload": {...}, "version": 2, "timestamp": "2024-01-01T00:00:00Z"},
> { "type": "delete", "id": "Y", "timestamp": "2024-01-01T00:00:00Z" },
> { "type": "unPublish", "id": "Z", "payload": {...}, "version": 4, "timestamp": "2024-01-01T00:00:00Z"},
>]
>```

#### Event Types
- `publish` marks the **CmsEntity** as "published" and updates it
- `update` replaces the **CmsEntity**'s content with the newer details received without modifying the "published" flag
- `unPublish`: marks the **CmsEntity** as "not published" so it is no longer visible by any User, and updates its contents. It applies even when no prior `publish` event was received for that entity: per the initial requirements' corner case, the payload and version are still stored so the latest version is never lost from the database.
- `delete`: removes the **CmsEntity** by deleting it from the persistence layer

#### Validations
This endpoint acts as an Outbox, hence it validates and sanitizes only the base **CmsRequest** values. The `payload` object is checked to be a valid `json` key/value object and nothing else. If these do not cause an error, the **CmsEvent** is recorded in the database and the endpoint returns `201` (Created), otherwise it returns `400`. A batch is all-or-nothing: if any event in the array is invalid, the whole batch is rejected with `400` and nothing is recorded.

#### Event processing
When **CmsEvent**s are processed, a number of scenarios may arise depending on the `id` (entityId), `version` (entity version) and `payload`'s contents. These include, but are not limited to, the following:

1. `publish`, `update` and `unPublish`, when they refer to an `id` of an object that doesn't exist, they create it.
1. `publish`, `update` and `unPublish`, when an event with the same `id`, `version` **and** `type` was already recorded in the DB, do nothing (idempotent handling of re-delivered events). An event of a different `type` for the same `id` and `version` is **not** a duplicate: e.g. a `publish` followed by an `unPublish` of the same version must still flip the entity to "not published", and vice versa.
  > a special case could be comparing the incoming `payload` with the entity already existing, or checking the "is published" flag status, I'm simplifying by ignoring these scenarios.
1. `delete`, when referring to an `id` of an object that doesn't exist, does nothing.
1. an event whose `version` is older than the entity's current stored version is ignored as stale (out-of-order delivery), so the stored entity always keeps the latest version.

> Note: `payload` is assumed to always be present for `publish`, `update` and `unPublish` events, as stated in the request definition (`delete` events omit it).

#### Outbox processing model
Events are processed **immediately but asynchronously** (design decision): after recording the **CmsEvent** with status `Pending`, the endpoint signals an in-process `CmsEventProcessorWorker` through a `System.Threading.Channels` fast-path; the worker also sweeps pending rows at startup and periodically, so events survive crashes and restarts. Each event is processed in its own transaction and advances to `Processed`, or to `Failed` with its error recorded (failed events are not retried automatically and are logged at Error; processing continues with the next event). Processing maintains the `cms_entities` store — the latest version, payload, published flag and the administrator-visibility flag the User API will read. The write side (ingest command + processing) lives in `CmsWebhook.Application`/`CmsWebhook.Infrastructure` following strict CQRS; the read side is the planned User API.

### Healthcheck
`GET /health` is an **anonymous liveness probe** returning `200 OK` with a JSON body (`{"status":"Healthy"}`) while the application is running, and `503 Service Unavailable` when unhealthy. It exists so load balancers and orchestrators can probe the API without credentials, and is the first endpoint exempt from the fallback authorization policy. It is implemented with the built-in `AddHealthChecks()` + `MapHealthChecks("/health")` and a small JSON response writer — liveness only, no deep or database checks (the app already fails fast at startup when either store is unreachable).

### OpenAPI contract
`GET /openapi/v1.json` serves an **OpenAPI document generated from the endpoint code at runtime** (`Microsoft.AspNetCore.OpenApi`), so the contract can never drift from the implementation — code is the source of truth. The endpoint is anonymous. The **Scalar** API reference UI (`Scalar.AspNetCore`) is mapped as the browsable Swagger replacement (`/scalar/v1`) and is **always-on — served in every environment**, anonymous like the raw contract it renders (it shows exactly the same public JSON; change `openapi-consumer-ui`). Endpoints are organized into per-feature classes (`Endpoints/HealthEndpoints.cs`, `Endpoints/CmsEventEndpoints.cs`) exposing `MapXxx(this IEndpointRouteBuilder)` extension methods, with `WithSummary`/`WithDescription`/`WithTag` metadata enriching the contract.

## User API
> **Planned:** not yet implemented; no User API project exists in the solution yet. The endpoints below describe the intended design.

The **UserAPI** is meant to serve clients interested in knowing their entities' data.

### `/entities` GET
Handles the `GET` operation by returning a list of all currently published entities which:
- If **User** is not an **Administrator**, it only displays the entities which have not been disabled by an administrator.
- If **User** is an **Administrator**, it displays all entities.

#### Response
```json
[
    {
        "latest-version": 1,
        "last-updated": "(timestamp)",
        "payload": {...}
    }, ...
]
```

### `/entities/{id}/disable` POST
Only accepts requests from the `administrator` user, and internally results in the "is visible" flag to be disabled. Requires no request body and returns an empty success response (if it doesn't fail).

### `/entities/{id}/enable` POST
Only accepts requests from the `administrator` user, and internally results in the "is visible" flag to be enabled. Requires no request body and returns an empty success response (if it doesn't fail).

> Note: enabling and disabling the visibility is independent from publishing status and regular update operations performed on the entity.
