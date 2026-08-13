## Purpose

Defines the CMS webhook ingestion capability: how the CMS posts events, how they are validated and durably recorded, and how they are processed asynchronously into the system's store of CmsEntities.

## ADDED Requirements

### Requirement: Endpoint requires authentication

The `POST /cms/events` endpoint SHALL require HTTP Basic Authentication and SHALL be accessible only to the reserved cms user, following the CMS Webhook API authentication policy (all endpoints require authentication; only the cms user is authorized). Requests that fail authentication or authorization SHALL NOT record any event.

#### Scenario: Request without credentials

- **WHEN** a client sends `POST /cms/events` without an `Authorization` header
- **THEN** the API responds with `401 Unauthorized` and records no event

#### Scenario: Valid credentials of a non-cms user

- **WHEN** a client sends valid Basic credentials of a user other than the reserved cms user
- **THEN** the API responds with `403 Forbidden` and records no event

### Requirement: Accepts a single event or a batch

The `POST /cms/events` endpoint SHALL accept either a single **CmsRequest** object or an array of **CmsRequest** objects as its JSON body. When every contained event is valid, the endpoint SHALL respond with `201 Created`.

#### Scenario: Single event accepted

- **WHEN** a client sends a single valid **CmsRequest** object
- **THEN** the API responds with `201 Created`

#### Scenario: Batch of events accepted

- **WHEN** a client sends an array of valid **CmsRequest** objects
- **THEN** the API responds with `201 Created` and every event in the batch is recorded

### Requirement: Validates and sanitizes events

The endpoint SHALL validate and sanitize every received **CmsRequest** before recording it. A **CmsRequest** SHALL carry:

- a `type` that is exactly `publish`, `update`, `unPublish` or `delete` (case-sensitive);
- a non-empty `id` string;
- a `timestamp` parseable as an ISO 8601 / RFC 3339 date-time;
- for `publish`, `update` and `unPublish`: a `version` that is an integer of at least `1`, and a `payload` that is a JSON object.

A `delete` event SHALL NOT be required to carry `payload` or `version`, and extra fields on a `delete` event SHALL be ignored. A request that fails validation SHALL be rejected with `400 Bad Request` and nothing recorded.

#### Scenario: Unknown event type

- **WHEN** a client sends a **CmsRequest** whose `type` is not one of `publish`, `update`, `unPublish` or `delete`
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Missing event type

- **WHEN** a client sends a **CmsRequest** without a `type`
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Empty id

- **WHEN** a client sends a **CmsRequest** with an empty or whitespace-only `id`
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Invalid timestamp

- **WHEN** a client sends a **CmsRequest** whose `timestamp` is not parseable as an ISO 8601 / RFC 3339 date-time
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Version below one

- **WHEN** a client sends a `publish`, `update` or `unPublish` event with a `version` of zero, a negative number or a non-integer
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Payload is not a JSON object

- **WHEN** a client sends a `publish`, `update` or `unPublish` event whose `payload` is not a JSON object (e.g. an array, string or number)
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Missing payload on a non-delete event

- **WHEN** a client sends a `publish`, `update` or `unPublish` event without a `payload`
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Delete without payload or version

- **WHEN** a client sends a `delete` event carrying only `type`, `id` and `timestamp`
- **THEN** the API responds with `201 Created` and records the event

#### Scenario: Malformed JSON body

- **WHEN** a client sends a body that is not valid JSON
- **THEN** the API responds with `400 Bad Request` and records no event

### Requirement: Batch recording is atomic

When the body is an array, the endpoint SHALL treat the batch as all-or-nothing: if any event in the batch fails validation, the endpoint SHALL respond with `400 Bad Request` and SHALL NOT record any event from the batch.

#### Scenario: Batch contains an invalid event

- **WHEN** a client sends an array in which at least one event is invalid alongside valid ones
- **THEN** the API responds with `400 Bad Request` and none of the batch's events are recorded

### Requirement: Events are recorded before processing

The endpoint SHALL durably record each accepted **CmsRequest** as a **CmsEvent** in the event log with status `Pending` and respond with `201 Created` without waiting for processing to complete.

#### Scenario: Response does not wait for processing

- **WHEN** a valid event is posted
- **THEN** the API responds with `201 Created`, the event is recorded with status `Pending`, and its processing completes asynchronously afterwards

### Requirement: Events are processed asynchronously

The system SHALL process recorded events in the background, applying each event to the stored **CmsEntity** for its `id`. Processing SHALL happen shortly after recording (immediately, but asynchronously) and SHALL eventually complete for every recorded event, including events left `Pending` by a restart or a crash.

#### Scenario: Event is processed after acceptance

- **WHEN** a `publish` event has been accepted and processing runs
- **THEN** a **CmsEntity** for the event's `id` exists with the event's `payload` and `version` and is marked as published

#### Scenario: Pending events are recovered after restart

- **WHEN** the application restarts with events still in status `Pending`
- **THEN** those events are processed after startup

### Requirement: Processing follows the event business rules

When processing a **CmsEvent**, the system SHALL behave as follows:

- `publish`, `update` and `unPublish` referring to an `id` with no stored **CmsEntity** SHALL create it;
- an event whose `id`, `version` **and** `type` were already recorded SHALL be a no-op (idempotent handling of re-delivered events);
- an event of a different `type` for the same `id` and `version` SHALL be applied (e.g. a `publish` followed by an `unPublish` of the same version flips the entity to not published, and the reverse flips it back);
- `delete` SHALL hard-delete the stored **CmsEntity**; a `delete` for an unknown `id` SHALL do nothing;
- an event whose `version` is older than the stored **CmsEntity**'s current version SHALL be ignored as stale (out-of-order delivery).

#### Scenario: Publish creates a new entity

- **WHEN** a `publish` event refers to an `id` with no stored **CmsEntity**
- **THEN** a **CmsEntity** is created with the event's `payload` and `version` and is marked as published

#### Scenario: Identical re-delivered event is a no-op

- **WHEN** an event with the same `id`, `version` and `type` is recorded again
- **THEN** processing leaves the stored **CmsEntity** unchanged

#### Scenario: Publish then unpublish of the same version

- **WHEN** a `publish` for version `N` is followed by an `unPublish` for the same version `N`
- **THEN** the stored **CmsEntity** holds version `N`'s `payload` and is marked as not published

#### Scenario: Unpublish then publish of the same version

- **WHEN** an `unPublish` for version `N` is followed by a `publish` for the same version `N`
- **THEN** the stored **CmsEntity** holds version `N`'s `payload` and is marked as published

#### Scenario: Delete removes the entity

- **WHEN** a `delete` event refers to an `id` with a stored **CmsEntity**
- **THEN** the stored **CmsEntity** is hard-deleted

#### Scenario: Delete of an unknown id does nothing

- **WHEN** a `delete` event refers to an `id` with no stored **CmsEntity**
- **THEN** processing completes without error and no **CmsEntity** is created

#### Scenario: Stale event is ignored

- **WHEN** an event's `version` is lower than the stored **CmsEntity**'s current version
- **THEN** the stored **CmsEntity** is left unchanged

### Requirement: Unpublish never loses the latest version

The system SHALL apply an `unPublish` event even when no prior `publish` event was received for the entity: the `payload` and `version` are stored and the entity is marked as not published. This keeps the latest version in the store even when the entity was never published (initial requirements' corner case).

#### Scenario: Unpublish without a prior publish

- **WHEN** an `unPublish` event arrives for an `id` that was never published
- **THEN** a **CmsEntity** is stored with the event's `payload` and `version` and is marked as not published

### Requirement: The entity store tracks the latest version

The stored **CmsEntity** SHALL keep the latest version and its `payload`; processing a newer version SHALL replace the stored `payload` and version. Newly created **CmsEntities** SHALL be visible to regular users by default.

#### Scenario: Newer version replaces the stored payload

- **WHEN** a `publish` for version `N+1` is processed after version `N`
- **THEN** the stored **CmsEntity** holds version `N+1`'s `payload` and version

### Requirement: Processed and failing events are logged

The system SHALL log every processed event and every failing event. An event whose processing throws SHALL be marked `Failed` with its error recorded, SHALL NOT be retried automatically, and SHALL NOT stop the processing of subsequent events.

#### Scenario: A failing event is marked failed and processing continues

- **WHEN** processing of an event throws
- **THEN** the event is marked `Failed`, the failure is logged, and later events are still processed
