# API Contract

The two APIs share a Basic-Auth credential store and two SQLite databases, but serve different clients:

- **CMS Webhook API** — the *organization* side: it ingests the events the external CMS sends (`POST /cms/events`).
- **Users API** — the *consumer* side: it serves the resulting entity store to regular users and the administrator (`GET /entities`, `POST /entities/{id}/disable|enable`).

**Machine contract.** The authoritative, always-in-sync contract is the OpenAPI document each API serves anonymously at `/openapi/v1.json`, with the browsable Scalar UI at `/scalar/v1` (both reachable without credentials; everything else requires Basic Auth). This page is the narrative companion — it explains the *meaning* of the endpoints, events, roles, and failure modes, and links to the machine contract for the exact schemas.

## Authentication

Both APIs require **HTTP Basic authentication** on every endpoint except `/health`, `/openapi/v1.json` and `/scalar/v1`. Credentials are validated against the shared credential store (seeded by `scripts/init-db.sh`; see [Configuration](configuration.md)).

| Username | CMS Webhook API | Users API |
|---|---|---|
| `cms-webhook` (reserved, 10–20 chars) | ✅ sends events | ❌ rejected (403) — reserved for the CMS side |
| `regular-user` | ❌ not authorized | ✅ lists published, enabled entities |
| `administrator` | ❌ not authorized | ✅ lists *all* entities (incl. disabled) and can disable/enable |

## CMS Webhook API — `POST /cms/events`

Ingests events from the external CMS. The body is a **single event object or a batch array of event objects** — both forms are accepted (a batch is all-or-nothing: one invalid element rejects the whole batch and nothing is recorded).

**Event shape** (see `/openapi/v1.json` for the schema): `type`, `id`, `payload`, `version`, `timestamp`.

- `type` — one of `publish`, `update`, `unPublish`, `delete`, **case-sensitive** (note the capital `P` in `unPublish`, matching the external system's wire format).
- `id` — the external entity's id.
- `payload` — the entity data as a JSON object. Required for every type except `delete`.
- `version` — the entity's version from the external system; the first version is `1`, and each change increments it. Required for every type except `delete`.
- `timestamp` — ISO 8601 / RFC 3339 date-time of when the event happened in the CMS.

**Event semantics**

| Type | Meaning | Effect on the entity store |
|---|---|---|
| `publish` | The entity is published with newer details. | Marks published; updates content to the event's version. |
| `update` | An existing entity was changed. | Updates content without touching the published flag. |
| `unPublish` | The entity was unpublished (not removed from the CMS — disabled). | Keeps the entity **in** the store, marks it unpublished (hidden from regular users). |
| `delete` | The entity was removed/unpublished for good. | **Hard-deletes** the entity from the store. |

**Why delete and unPublish differ.** The CMS unpublishes an entity by disabling it, not removing it — so `unPublish` keeps the data (hidden), while `delete` removes it. Both are honored faithfully on the store.

**The version corner case.** An entity can be modified (version X → X+1) and then unpublished *without any prior published version*. Because `unPublish` always applies even without a preceding `publish`, the store never loses the latest version — the entity exists, unpublished, at version X+1, exactly as the initial requirements demand.

**Responses**

| Code | Meaning |
|---|---|
| `201 Created` | Accepted and recorded in the outbox for processing (a batch is all-or-nothing). |
| `400 Bad Request` | Not valid JSON; neither an object nor an array of objects; or a validation failure (unknown `type`, missing/invalid `id`, `version` or `timestamp`, non-object `payload`). |
| `401 Unauthorized` | Missing or invalid credentials. |

## Users API — `GET /entities`

Lists the published entities visible to the caller. The administrator sees **all** published entities (including those disabled via the API); a regular user sees only published entities **not** disabled by an administrator. Each item carries `Id`, `IsVisibleByAdmin`, `LatestVersion`, `UpdatedAt` and `Payload` (see the OpenAPI document for the schema).

| Code | Meaning |
|---|---|
| `200 OK` | The visible entities (an array; empty when none are visible). |
| `401 Unauthorized` | Missing or invalid credentials. |
| `403 Forbidden` | The caller is the reserved `cms-webhook` user, which is not authorized on this API. |

## Users API — `POST /entities/{id}/disable` and `/enable`

Administrator-only commands that override an entity's **visibility** for regular users. This is an API-side overwrite: it does **not** touch the CMS or the entity's published flag. Both are idempotent and take no request body.

| Code | Meaning |
|---|---|
| `204 No Content` | The visibility change was applied (idempotent). |
| `401 Unauthorized` | Missing or invalid credentials. |
| `403 Forbidden` | The caller is not the administrator. |
| `404 Not Found` | No entity with this id is known. |

## Liveness — `GET /health`

Anonymous liveness probe on both APIs: `200 OK` when healthy, `503 Service Unavailable` when not. Used by load balancers and the deployment pipeline's verification.

## See also

- [Architecture](architecture.md) — why the system is shaped this way (single shared store, outbox, CQRS).
- [Domain glossary](dsl_glossary.md) — terminology (`CmsRequest`, `CmsEvent`, entity semantics).
- The served OpenAPI documents and Scalar UIs at `/openapi/v1.json` and `/scalar/v1` on each API.
