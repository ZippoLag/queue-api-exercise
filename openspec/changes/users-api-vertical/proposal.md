## Why

The architecture has designed the User API from day one — the read side that serves entity data to users, plus the administrator's enable/disable control over which entities regular users see — but no project exists yet; the docs mark it "planned" and `CmsEntity.IsVisibleByAdmin` exists specifically for it. This change delivers the complete Users API vertical: the reserved users, the `/entities` read endpoint, and the admin-only enable/disable endpoints, on top of the existing shared auth and the entity store.

## What Changes

- Add a new **Users module** mirroring the CmsWebhook layering: `src/Users/Users.Api`, `Users.Application`, `Users.Domain`, `Users.Infrastructure`, with tests under `tests/Users/` (module boundaries refined in design).
- Endpoints (per the planned design in `docs/architecture.md`):
  - `GET /entities` — list currently published entities: a **regular user** sees only entities not disabled by an administrator (`IsVisibleByAdmin == true`); the **administrator** sees all entities, including disabled ones.
  - `POST /entities/{id}/disable` — administrator only; flips the entity's admin-visibility flag off. No request body; empty success response.
  - `POST /entities/{id}/enable` — administrator only; flips the flag on. No request body; empty success response.
  - Anonymous `/health` liveness probe and always-on `/openapi/v1.json` + Scalar UI, matching the CmsWebhook API (see `openapi-consumer-ui`).
- **No user-management endpoints**: users are manually-created only. Extend the existing tooling (`scripts/init-db.sh` / `tools/AuthDbInit`) to seed the reserved **`administrator`** user and a **`regular-user`** for testing, alongside the existing `cms-webhook` user.
- Authorization model: Basic auth via the shared `QueueApi.Auth` credential store. The `administrator` username is the admin (glossary: the only user authorized to define entity visibility); `cms-webhook` credentials are rejected by the Users API (reserved for the CMS API).
- **Cross-module integration point**: admin disable/enable must survive subsequent CMS events — the outbox worker's upsert must preserve `IsVisibleByAdmin` (today `EfCmsEntityRepository.UpsertAsync` overwrites the whole row with the event's fresh entity). Designed in `design.md`.
- CQRS: reads via dedicated query handlers on a read-optimized EF configuration; enable/disable via command handlers — writes and reads stay independent.

## Capabilities

### New Capabilities

- `users-api`: the Users API vertical — published-entity listing with the administrator visibility rule, admin-only enable/disable of entities, and the reserved `administrator`/regular-user role semantics on the shared credential store.

### Modified Capabilities

- `auth`: "Credential store is provisioned by an initialization script" — the script now also seeds the reserved `administrator` and `regular-user` users (today it seeds only `cms-webhook`).

## Impact

- **New projects**: `src/Users/*` (Api/Application/Domain/Infrastructure) + `tests/Users/*`, added to `QueueApi.slnx`.
- **Shared auth**: reused as-is via `QueueApi.Auth`; no changes to the hashing/verification mechanism.
- **CmsWebhook**: `CmsWebhook.Infrastructure`/`Application` — entity upsert preserves the administrator visibility flag across CMS events (spec delta confirmed in the specs phase).
- **Tooling**: `scripts/init-db.sh` and `tools/AuthDbInit` seed the administrator and regular-user.
- **Docs**: `docs/architecture.md` moves the User API section from planned to implemented; `docs/dsl_glossary.md` and `README.md` aligned.
