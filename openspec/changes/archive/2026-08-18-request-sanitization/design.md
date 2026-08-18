## Context

See proposal.md - Why. Today the webhook's timestamp check uses `DateTimeOffset.TryParse` with invariant culture, which accepts date-only, culture-formatted, and offset-less values despite the contract (spec, OpenAPI, error message, XML docs) declaring ISO 8601 / RFC 3339. "Sanitize" is undefined anywhere; the only acts today are id-trim and canonicalization, and the Users API performs no validation or sanitization of the route `id` of its enable/disable commands. The persistence layer is EF Core over SQLite; every value already flows through parameterized queries.

## Goals / Non-Goals

**Goals:**
- Make the timestamp parser enforce exactly the ISO 8601 / RFC 3339 form of the requirements' example (`2024-01-01T00:00:00Z`), so implementation and contract agree.
- Give "sanitize" a single, documented meaning shared by both APIs: accepted values are valid (non-null, non-empty, correct types) and safe to store; the `payload` stays opaque.
- Raise the Users API's enable/disable commands to the webhook's bar: reject empty/whitespace-only ids with `400`, trim before lookup.

**Non-Goals:**
- No payload content inspection, validation, transformation, or redaction — contents and format stay unknown by design (spec: "Payload contents are opaque").
- No manual string escaping: values are already persisted via EF Core parameterized queries, which is what makes storage injection-safe (see D2).
- No changes to authentication, authorization, rate limiting, or event processing rules.

## Decisions

### D1: Strict RFC 3339 timestamp parsing via `DateTimeOffset.TryParseExact`

Replace the lenient `DateTimeOffset.TryParse` in `CmsRequestValidator.TryValidate` with `DateTimeOffset.TryParseExact` against an explicit format array covering the requirements' example and RFC 3339 subset:

- `yyyy-MM-dd'T'HH:mm:ss'Z'` — `2024-01-01T00:00:00Z`
- `yyyy-MM-dd'T'HH:mm:sszzz` — `2024-01-01T00:00:00+02:00` (and `-hh:mm`)
- `yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'` — with optional fractional seconds
- `yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz`

**Why**: an exact format list is declarative, trivially testable, and cannot silently accept culture-formatted or date-only values the way the general `TryParse` does. The four formats keep acceptance aligned with the example while honoring RFC 3339's optional fractional seconds and numeric UTC offsets.

**Alternatives considered**:
- Keep `TryParse` + a regex pre-check — rejected: two mechanisms to keep in sync, regex hard to maintain, and `TryParse` still interprets offset-less values with the server's local offset.
- Parse `timestamp` as a `JsonElement` and use `JsonElement.GetDateTimeOffset()` — rejected: it would change the transport shape handling and its accepted ISO subset is not documented for our exact needs; the string-based pipeline stays.
- Accept lowercase `t`/`z` per RFC 3339 grammar — deferred: the requirements' schema and example use uppercase, and accepting both doubles the format array for no declared consumer need. Revisit if a client reports it.

### D2: "Safe to store" means parameterized persistence, not string escaping

The sanitization definition ("safe to store so values cannot alter the stored shape or inject into the storage layer") is satisfied by the existing EF Core parameterized writes plus validated types. There is nothing to escape in a parameterized query; adding manual escaping would be wrong and is explicitly out of scope. The design point is that *unvalidated* values (the current lenient timestamp, an untrimmed route id) are the injection/consistency risk, and validation + trim close it.

**Why**: document the mechanism so a future contributor does not "fix" the phrase by adding naive SQL-escaping.

### D3: Users API id validation lives in the endpoint (transport shape)

`EntityEndpoints.DisableAsync`/`EnableAsync` SHALL trim the route `id` and reject an empty-or-whitespace-only result with `400 Bad Request` before invoking the command handler; the trimmed id is passed on, and an unknown id still yields `404`.

**Why**: the route segment is transport shape, not a domain value — the same split the webhook already makes (its endpoint checks body shape and null elements; `CmsRequestValidator` checks field rules). Extending `SetEntityVisibilityCommandHandler` to return a tri-state (invalid/unknown/ok) would couple the command contract to HTTP concerns for a single field.

**Alternatives considered**:
- Validate in the Application command handler — rejected: it would need a new result shape and the rule is HTTP-shaped (a route segment), not a domain invariant.
- A shared sanitizer helper reused by both APIs — deferred: the rule is a two-line trim+check; the webhook's sanitization is embedded in `CmsRequestValidator` (Domain). Introduce shared code only when a second shared incoming value exists.

### D4: OpenAPI sync

- `CmsEventEndpoints.ConfigureOpenApiOperation`: the `400` description SHALL add the non-object-`payload` failure mode (today it lists type/id/version/timestamp only).
- `EntityEndpoints.ConfigureSetVisibilityOperation`: SHALL add a `400` response — "The id is empty or whitespace-only." — shared by disable and enable.

### D5: Docs + XML wording converge on one definition

`docs/dsl_glossary.md` gains a **Sanitization** entry defining the rule (valid + safe-to-store, payload opaque); `docs/architecture.md`'s Validations section and `docs/api-contract.md`'s 400 rows cite it; XML comments on `CmsRequestValidator`, `CmsRequest.Timestamp`, and `EntityEndpoints` use the same wording as the spec scenarios.

## Risks / Trade-offs

- **BREAKING timestamp acceptance** — clients sending date-only, culture-formatted, or offset-less timestamps now get `400`. → Mitigation: the rejection error message states the accepted form; the change is documented in `docs/api-contract.md` and the OpenAPI 400 description; the format matrix is covered by unit tests before the behavior ships.
- **Trim changes Users API lookup behavior** — ids padded with whitespace previously 404ed and will now resolve. → Mitigation: entity ids originate from webhook events whose ids are already trimmed, so no stored id can legitimately start/end with whitespace; a padded id is a client mistake that now behaves predictably.
- **TryParseExact silently rejecting a needed variant** — e.g. a future client sending lowercase `t`/`z`. → Mitigation: the accepted formats are explicit and documented; adding a format string is a one-line, test-covered change.

## Migration Plan

1. Tighten the webhook validator + unit tests (D1) and update the Users API endpoints + tests (D3) together; both are behavior changes behind the same "sanitize" definition.
2. Sync OpenAPI contracts (D4) and docs (D5) in the same change so contract and narrative never disagree with the new behavior.
3. No data migration: stored events/entities are unaffected; only acceptance of future requests changes.
4. Rollback: revert the validator formats and endpoint checks; nothing persisted depends on the new rules.

## Open Questions

None — decisions that could change the specs or approach (timestamp variants, where id validation lives, escaping vs. parameterization) were resolved above.
