## MODIFIED Requirements

### Requirement: Validates and sanitizes events

The endpoint SHALL validate and sanitize every received **CmsRequest** before recording it. Sanitization SHALL guarantee that accepted values are valid and safe to store: every kept value SHALL be non-null with a valid value, kept strings SHALL be non-empty (whitespace-only values SHALL be rejected), and accepted values SHALL be stored so they cannot alter the stored shape or inject into the storage layer. A **CmsRequest** SHALL carry:

- a `type` that is exactly `publish`, `update`, `unPublish` or `delete` (case-sensitive);
- a non-empty `id` string, trimmed when recorded;
- a `timestamp` that is an ISO 8601 / RFC 3339 date-time in the form of the requirements' example `2024-01-01T00:00:00Z` — ending in `Z` or a numeric UTC offset (`+hh:mm` / `-hh:mm`), with optional fractional seconds; date-only, culture-formatted, and offset-less timestamps SHALL be rejected;
- for `publish`, `update` and `unPublish`: a `version` that is an integer of at least `1`, and a `payload` that is a JSON object.

The `payload`'s internal contents and format SHALL NOT be inspected, validated, or transformed: only its being a JSON object is enforced, and it is recorded verbatim. A `delete` event SHALL NOT be required to carry `payload` or `version`, and extra fields on a `delete` event SHALL be ignored. A request that fails validation SHALL be rejected with `400 Bad Request` and nothing recorded.

#### Scenario: Unknown event type

- **WHEN** a client sends a **CmsRequest** whose `type` is not one of `publish`, `update`, `unPublish` or `delete`
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Missing event type

- **WHEN** a client sends a **CmsRequest** without a `type`
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Empty id

- **WHEN** a client sends a **CmsRequest** with an empty or whitespace-only `id`
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Timestamp follows the requirements' example format

- **WHEN** a client sends a **CmsRequest** whose `timestamp` is an ISO 8601 / RFC 3339 date-time like `2024-01-01T00:00:00Z` or `2024-01-01T00:00:00+02:00`
- **THEN** the API accepts the event and records it

#### Scenario: Invalid timestamp

- **WHEN** a client sends a **CmsRequest** whose `timestamp` is not a valid ISO 8601 / RFC 3339 date-time — for example unparseable text, a date-only value (`2024-01-01`), a culture-formatted value (`01/01/2024`), or an offset-less value (`2024-01-01T00:00:00`)
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Version below one

- **WHEN** a client sends a `publish`, `update` or `unPublish` event with a `version` of zero, a negative number or a non-integer
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Payload is not a JSON object

- **WHEN** a client sends a `publish`, `update` or `unPublish` event whose `payload` is not a JSON object (e.g. an array, string or number)
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Payload contents are opaque

- **WHEN** a client sends a `publish`, `update` or `unPublish` event whose `payload` is a JSON object with arbitrary internal contents (unknown keys, nested shapes, any value types)
- **THEN** the API accepts the event and records the payload verbatim without inspecting its contents

#### Scenario: Missing payload on a non-delete event

- **WHEN** a client sends a `publish`, `update` or `unPublish` event without a `payload`
- **THEN** the API responds with `400 Bad Request` and records no event

#### Scenario: Delete without payload or version

- **WHEN** a client sends a `delete` event carrying only `type`, `id` and `timestamp`
- **THEN** the API responds with `201 Created` and records the event

#### Scenario: Malformed JSON body

- **WHEN** a client sends a body that is not valid JSON
- **THEN** the API responds with `400 Bad Request` and records no event
