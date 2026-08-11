# Architecture

## System Overview

![alt text](system_overview.png)

The Queue-API-Exercise system is meant to have 2 REST APIs available: a webhook for handling CMS entity-related events and one to handle Users and Admin Users requests. Knowing this project may grow, I choose to pay the cost of an initial scaffolding big-bang with boilerplate and creating the solution as a modular monolith, ready to be split whenever neccesary.

### Authentication & Authorization
Authentication is handlded in both APIs as Basic Auth (sername+password) in all incoming requests:
- `username` [10,20] characters in length, no other constraints
- `password` randomly generated GUID

> Note: `"cms"` is a special username reserved to be used by the CMS when connecting to the CMS API, it's the **only** username enabled to connect to the CMS API, all others return 403, it is not valid for the Users API. `"admin"` is a special username reserved to be used by the system administrator in the Users API.

> Note: no signature verification is provided in current version

### Persistence
Persistence layer will be a single `PostgreSQL` DB, this may be broken down into 2 distinct data stores (one for incoming events, other for the current projected entity state). Caching is out of scope.

### Logging
TBD.

## CMS Webhook API - v1
The **CmsWebhook** is intended to be a _webhooks API_ so it needs a quick response to the external system that's _notifying_ us of already-happened events. All **CmsEvent**s received will be stored in a `cms_event_log` table.

> For the current **v1**, validations will be minimal. Whether **v2** will incorporate more complex validations in these endpoints or push this logic to async workers is TBD.

### `/cms/events` Request POST
Edpoint which handles the `POST`operation and expects the following **CmsRequest** as `json` object as body:
```json
{ "type": "eventType", "id": "eventId", "payload": {...}, "version": 0, "timestamp": "2024-01-01T00:00:00Z"}
```

> Note: All fields are required (not mull and with a valid value).

#### Fields:
- `id`: `string` entity's id.
- `payload`: `json` object which contains the actual data for the **CmsEntity** which needs to be fed into our system's DB. Due to security requirements, they will be stored as an encripted json string using a key available as environment variable (TBD: move to a secret key vault storage).
- `type`: `string` operation performed upon the entity
- `timestamp`: `string` ISO 8601 (aka RFC 3339) date-time information of _when_ the event happened in the external CMS
- `version`: `int` number of the version number coming from the external system

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
- `unPublish`: marks the **CmsEntity** as "not published" so it is no longer visible by any User, and updates it's contents
- `delete`: removes the **CmsEntity** by deleting it from the persistence layer

#### Validations
This endpoint acts as an Outbox, hence it validates only the base **CmsRequest** values. The `payload` object is checked to be a valid `json` key/value object and nothing else. If these do not cause an error, the **CmsEvent** is recorded in the database and the endpoint returns `201` (Created), otherwise it returns `400`.

#### Event processing
When **CmsEvent**s are processed, a number of scenarios may arise depending on the `eventId`, `entityVersion` and `payload`'s contents. These include, but are not limited to, the following:

1. `publish`, `update` and `unPublish`, when they refer to an `id` of an object that doesn't exist, they create it.
1. `publish`, `update` and `unPublish`, when they refer to a combination of `id` and `version` that already exist in the DB, do nothing.
  > a special case could be comparing the incoming `payload` with the entity already existing, or checking the "is published" flag status, I'm simplifying by ignoring these scenarios.
1. `delete`, when referring to an `id` of an object that doesn't exist, does nothing.

> Note: `payload` is assumed to always be present, as stated in the request definition.

## User API
The **UserAPI** is meant to serve clients interested in knowing their entities' data.

### `/entities` GET
Handles the `GET` operation by returns a list of all currently published entities which:
- If **User** is not an **Admin**, it only displays the entities which have not been disabled by an admin.
- If **User** is an **Admin**, it displays all entities.

#### Response
```json
[
    {
        "latest-version": 0,
        "last-updated": "(timestamp)",
        "payload": {...}
    }, ...
]
```

### `/entities/{id}/disable` POST
Only accepts requests from the `admin` user, and inernally results in the "is visible" flag to be disabled. Requires no request body and returns an empty success response (if it doesn't fail).

### `/entities/{id}/enable` POST
Only accepts requests from the `admin` user, and inernally results in the "is visible" flag to be enabled. Requires no request body and returns an empty success response (if it doesn't fail).

> Note: enabling and disabling the visibility is independent from publishing status and regular update operations performed on the entity.