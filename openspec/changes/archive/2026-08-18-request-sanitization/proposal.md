## Why

The initial requirements say "Incoming data must be validated and sanitized", but the term "sanitize" is never defined and the two APIs apply it unevenly. The CMS Webhook API validates and sanitizes its incoming events, while the Users API accepts the route `id` of its enable/disable commands with no validation or sanitization at all — the same quality standard is not held across both APIs. In addition, the webhook's timestamp check is more lenient than the declared contract: `DateTimeOffset.TryParse` accepts date-only (`2024-01-01`), culture-formatted (`01/01/2024`) and offset-less (`2024-01-01T00:00:00`) values even though the spec, the OpenAPI document, the error message and the XML documentation all say the timestamp must be an ISO 8601 / RFC 3339 date-time like the requirements' example `2024-01-01T00:00:00Z`.

This change defines what "validated and sanitized" means, makes the timestamp contract honest, and raises the Users API to the same quality bar.

## What Changes

- **Define "sanitize"** — incoming data must be *valid* (no nulls, no empty or whitespace-only strings, correct types) and *safe to store* (values are persisted so they cannot alter the stored shape or inject into the storage layer). The `payload` of an event remains opaque: it is only checked to be a valid JSON object — its contents and internal format are not inspected, validated, or transformed.
- **Tighten the webhook timestamp contract (BREAKING)** — `POST /cms/events` SHALL accept exactly the ISO 8601 / RFC 3339 date-time form of the requirements' example (`2024-01-01T00:00:00Z`, optionally with a numeric UTC offset and fractional seconds) and SHALL reject date-only, culture-formatted, and offset-less timestamps with `400 Bad Request`. This makes the implementation match the already-declared contract in the spec, OpenAPI document, error message, and XML docs; clients relying on the lenient formats will now be rejected.
- **Users API parity** — `POST /entities/{id}/disable` and `POST /entities/{id}/enable` SHALL validate and sanitize the route `id` like the webhook does: an empty or whitespace-only id SHALL be rejected with `400 Bad Request` (previously it fell through to `404`), and the id SHALL be trimmed before the lookup so padded ids resolve to the stored entity.
- **OpenAPI contract accuracy** — the webhook's `400` response description gains the non-object `payload` failure mode it currently omits; the Users API enable/disable operations gain a documented `400` response.
- **Docs + XML sync** — `docs/architecture.md`, `docs/api-contract.md`, and `docs/dsl_glossary.md` define the sanitization rule and the strict timestamp; XML doc comments and tests cite the same wording.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cms-webhook-api/event-ingestion`: the "Validates and sanitizes events" requirement is redefined — it gains an explicit definition of sanitization (valid + safe-to-store values, payload stays opaque) and the timestamp rule is tightened to the ISO 8601 / RFC 3339 form of the requirements' example, rejecting lenient formats.
- `users-api`: the "Administrator enables and disables entity visibility" requirement gains id validation/sanitization (empty or whitespace-only id rejected with `400`, id trimmed before lookup), and the "OpenAPI document" requirement's status-code scenario gains the new `400` response for the enable/disable operations.

## Impact

- **Code**: `src/CmsWebhook/CmsWebhook.Domain/CmsRequestValidator.cs` (strict RFC 3339 timestamp parsing, sanitization remarks), `src/Users/Users.Api/Endpoints/EntityEndpoints.cs` (id validation + trim, OpenAPI `400`), `src/CmsWebhook/CmsWebhook.Api/Endpoints/CmsEventEndpoints.cs` (400 description mentions non-object payload).
- **Tests**: `tests/CmsWebhook/CmsWebhook.Domain.Tests/CmsRequestValidatorTests.cs` (timestamp format matrix — accept example/offsets, reject date-only/US/offset-less), `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiEventIngestionTests.cs`, `tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiOpenApiTests.cs` (400 description), `tests/Users/Users.Api.Tests/` (empty/whitespace id → 400, trim-before-lookup, OpenAPI 400).
- **Docs**: `docs/architecture.md` (Validations section: define sanitize, strict timestamp, Users API rule), `docs/api-contract.md` (400 rows for both APIs), `docs/dsl_glossary.md` (define "sanitization"), plus XML doc comments in the touched source and test files.
- **Out of scope**: payload content inspection or transformation (its contents stay opaque by design); authentication/authorization behavior; rate limiting; the archived `docs/archived/initial_requirements.md` (invariant).
