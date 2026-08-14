## Context

See proposal.md - Why. `docs/architecture.md` already designs the User API endpoints (`GET /entities`, `POST /entities/{id}/disable|enable`) and `CmsEntity.IsVisibleByAdmin` exists for it. The shared `QueueApi.Auth` store holds `UserCredential { Id, Username, PasswordHash }` — no role field — and users are provisioned manually via `scripts/init-db.sh` + `tools/AuthDbInit`, which today seeds a single user. The outbox worker writes the entity store through `EfCmsEntityRepository.UpsertAsync`, which overwrites the whole row.

## Goals / Non-Goals

**Goals:**
- Deliver the three planned endpoints with the administrator/regular-user role model.
- Reuse the shared auth mechanism and the existing CMS entity store — no new databases.
- Keep CQRS separation: read-optimized queries for listing; command handlers for enable/disable.
- The administrator's visibility flag survives subsequent CMS events.
- Always-on OpenAPI + Scalar, matching the CmsWebhook API (see `openapi-consumer-ui`).

**Non-Goals:**
- User-management endpoints (create/delete/change password via the API) — users stay manually provisioned.
- Role columns/claims or any change to the credential store schema.
- Entity writes by regular users; pagination/filtering on `GET /entities` (v1 returns the full list).

## Decisions

### 1. Role model: reserved usernames, no schema change

`administrator` is the admin; every other valid username is a regular user; `cms-webhook` is rejected on the Users API (reserved for the CMS API). Implemented with two authorization policies: the fallback policy requires authentication and `Name != cms-webhook`; the admin endpoints additionally require `Name == administrator`. Reserved names are compared exactly as provisioned (case-sensitive), matching the store's username semantics.

- *Alternative:* add a role column/claims to the store — rejected; schema + provisioning change with no current need, and the glossary's reserved-username model already matches (`cms-webhook` precedent).

### 2. New Users module mirroring CmsWebhook's layering

`src/Users/Users.Api` (minimal-API endpoints), `Users.Application` (ports + query/command handlers), and `Users.Infrastructure` (own `UsersDbContext` over the shared `cms_entities` table; `AsNoTracking` for reads, single-writer for commands). There is deliberately **no `Users.Domain` project**: the module reuses `CmsWebhook.Domain.CmsEntity` directly (a shared domain type in the modular monolith); a Domain project is introduced only if the module grows its own rules. Tests under `tests/Users/`.

- *Alternative:* duplicate the entity mapping in the Users module — rejected; drift risk on a shared table.
- *Alternative:* reuse `CmsDbContext` directly — rejected; couples modules at the infrastructure level and pulls the event log into the Users module's surface.

### 3. Admin flag preservation (cross-module invariant)

Tracing the current flow: the processor loads the entity tracked on the same `CmsDbContext`, `CmsEventProcessingRules` mutates that instance in place (never touching `IsVisibleByAdmin`), and `UpsertAsync` finds the same tracked instance — so the flag already survives an upsert **by accident of in-place mutation, not by design**. The invariant is unprotected: a refactor that builds fresh entities (as the create path does) would silently reset a disabled entity to visible.

Fix, two parts:
1. A **regression test** pinning the invariant — a processed event does not reset `IsVisibleByAdmin`.
2. A **defensive copy** in `EfCmsEntityRepository.UpsertAsync`: carry the existing row's `IsVisibleByAdmin` onto the incoming entity before `SetValues`, so the invariant holds for any producer of the entity instance.

Note the `delete`-then-`publish` edge: deleting an entity removes its disabled state; a later `publish` recreates it visible-by-default, which is acceptable (the entity no longer existed).

- *Alternative:* make the outbox pipeline visibility-aware in the rules — rejected; the flag is a Users-domain concern, not an event-processing concern, and the defensive copy keeps the invariant at the storage boundary.

### 4. Endpoint contracts

- `GET /entities` returns the filtered list; each item includes the entity **id** and its **administrator-visibility flag** (so the administrator can discover which entities to disable/enable and see current state), alongside `latest-version`, `last-updated`, and `payload`. The shape is uniform for both roles — regular users only ever receive enabled items, so their flag is always true. This deliberately extends the architecture doc's sketch, which omitted the id and made the endpoints unusable from the API.
- `POST /entities/{id}/disable|enable` return `204 No Content` (empty success), are idempotent, take no body, and return `404` for unknown ids.
- Startup fails fast when the store lacks the `administrator` user (mirroring the CmsWebhook startup check).

### 5. Seeding

`tools/AuthDbInit` seeds the three reserved users (`cms-webhook`, `administrator`, `regular-user`) idempotently; `scripts/init-db.sh` takes three password arguments (cms, admin, regular) with local-development defaults. This is a **BREAKING** change to the script's signature (was `[username] [password]`).

### 6. Health, OpenAPI, Scalar

Anonymous `/health`, `/openapi/v1.json`, and always-on Scalar, copied from the CmsWebhook pattern so both APIs behave identically for consumers.

## Risks / Trade-offs

- [Two EF contexts over one SQLite file] → WAL journal mode + busy timeout are already the standing convention; reads are `AsNoTracking`; commands retain single-writer semantics.
- [Upsert fix could regress CMS event tests] → existing CmsWebhook tests must stay green; a new test asserts the flag survives an upsert.
- [Reserved-username roles cap future role growth] → acceptable for v1; a role column can be added later without changing the API contract.
- [Second 403 policy (cms-webhook rejection) adds auth complexity] → declarative policies + integration test coverage keep it contained.

## Migration Plan

1. Re-run `scripts/init-db.sh` against existing dev stores to seed the new users (idempotent; no data loss).
2. No `cms_entities` migration — `IsVisibleByAdmin` already exists.

## Open Questions

None — the spec, approach, and task breakdown are settled. Status codes and the 404/idempotency behavior are fixed in the spec.
