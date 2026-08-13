# Architecture

## System Overview

![alt text](system_overview.png)

The Queue-API-Exercise system is meant to have 2 REST APIs available: a webhook for handling CMS entity-related events and one to handle Users and Admin Users requests. Knowing this project may grow, I choose to pay the cost of an initial scaffolding big-bang with boilerplate and creating the solution as a modular monolith, ready to be split whenever necessary.

**Current implementation status:** only the **CmsWebhook API** exists and is consuming the shared auth capability; the **User API** and the CMS event endpoints are still planned (see the roadmapped sections below).

### Authentication & Authorization
Authentication is handled in the CmsWebhook API as Basic Auth (`username`+`password`) in all incoming requests. The mechanism is implemented once in the shared `QueueApi.Auth` library (`src/Shared/QueueApi.Auth`) so the future User API reuses the same scheme and store.

- `username` [10,20] characters in length, no other constraints. The reserved cms username is read from the `Auth:CmsUsername` configuration value (default `cms-webhook`) and the application fails to start if the configured value violates the length rule.
- Credentials are **not** hardcoded: they live in the shared SQLite credential store and are verified against a stored **PBKDF2 hash** (per-user random salt). Plaintext passwords are never persisted. The store is provisioned idempotently by `scripts/init-db.sh` (username and password passed as positional arguments); the API fails to start with a descriptive error if the store is unreachable or has not been initialized with the cms user.

> Note: `"cms-webhook"` is a special username reserved to be used by the CMS when connecting to the CMS API. It is the **only** username authorized to access the CMS API — valid credentials of any other user are rejected with `403`, all other failures with `401`. It is not valid for the Users API. `"administrator"` is a special username reserved to be used by the system administrator in the future Users API.

> Note: no signature verification is provided in current version

### Persistence
Persistence is a single `sqlite` relational database accessed via **EF Core** (per the initial requirements), which may be broken down into several data stores when a real database engine becomes necessary.

- **Implemented:** the shared credential store (`db/queue-auth.db`, `Users` table) holding username + PBKDF2 password hash per user. Its location is configurable via the `ConnectionStrings:AuthDb` configuration value (e.g. through an environment variable), so it can point at a different store without code changes.
- **Planned:** the `cms_event_log` table for CMS events (see CMS Webhook API below).

Caching is out of scope.

### Performance
Per the initial requirements, two decisions must be documented (TBD until the CMS event processing is implemented): the choice between asynchronous and synchronous event processing with its justification; and a read-only/writer configuration for the EF context with optimized EF read queries.

### Logging
Leveled console output (`Console` with explicit levels) is used for the small amount of logging present; richer logging (e.g. Serilog) remains TBD. Per the initial requirements' observability section, all processed events — including failing ones — must be logged; this applies to the CMS event processing once implemented.

## CMS Webhook API - v1
> **Planned:** as of the current implementation the CmsWebhook API exposes only authentication, startup validation and a placeholder `GET /` route. The `/cms/events` endpoint, event types, validations and event processing below describe the **planned** v1 behavior.

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
- `payload`: `json` object which contains the actual data for the **CmsEntity** which needs to be fed into our system's DB. Due to the confidential-data requirements, they will be stored as an encrypted json string using a key available as environment variable (TBD: move to a secret key vault storage).
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
This endpoint acts as an Outbox, hence it validates and sanitizes only the base **CmsRequest** values. The `payload` object is checked to be a valid `json` key/value object and nothing else. If these do not cause an error, the **CmsEvent** is recorded in the database and the endpoint returns `201` (Created), otherwise it returns `400`.

#### Event processing
When **CmsEvent**s are processed, a number of scenarios may arise depending on the `id` (entityId), `version` (entity version) and `payload`'s contents. These include, but are not limited to, the following:

1. `publish`, `update` and `unPublish`, when they refer to an `id` of an object that doesn't exist, they create it.
1. `publish`, `update` and `unPublish`, when an event with the same `id`, `version` **and** `type` was already recorded in the DB, do nothing (idempotent handling of re-delivered events). An event of a different `type` for the same `id` and `version` is **not** a duplicate: e.g. a `publish` followed by an `unPublish` of the same version must still flip the entity to "not published", and vice versa.
  > a special case could be comparing the incoming `payload` with the entity already existing, or checking the "is published" flag status, I'm simplifying by ignoring these scenarios.
1. `delete`, when referring to an `id` of an object that doesn't exist, does nothing.

> Note: `payload` is assumed to always be present for `publish`, `update` and `unPublish` events, as stated in the request definition (`delete` events omit it).

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
