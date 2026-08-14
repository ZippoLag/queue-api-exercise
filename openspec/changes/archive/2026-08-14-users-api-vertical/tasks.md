## 1. Seeding

- [x] 1.1 Extend `tools/AuthDbInit` to seed the `administrator` and `regular-user` users idempotently alongside `cms-webhook`
- [x] 1.2 Update `scripts/init-db.sh` to the three-password contract (cms, admin, regular) with local-development defaults
- [x] 1.3 Update `tools/AuthDbInit.Tests` for multi-user seeding (idempotency, no duplication)

## 2. CmsWebhook flag preservation

- [x] 2.1 Harden `EfCmsEntityRepository.UpsertAsync` with a defensive copy: carry the existing row's `IsVisibleByAdmin` onto the incoming entity before `SetValues`
- [x] 2.2 Add a CmsWebhook test pinning the invariant: a processed event does not reset `IsVisibleByAdmin`

## 3. Users module scaffold

- [x] 3.1 Create `src/Users/Users.Application`, `Users.Infrastructure`, and `Users.Api` projects (net9.0, XML docs enabled) referencing `QueueApi.Auth` and `CmsWebhook.Domain` (no `Users.Domain` project — the module reuses `CmsWebhook.Domain` directly)
- [x] 3.2 Add the projects and `tests/Users/*` test projects to `QueueApi.slnx`

## 4. Read side

- [x] 4.1 Implement the `GET /entities` query handler with a read-optimized context (`AsNoTracking`) over the shared `cms_entities` table, returning per item the entity id, administrator-visibility flag, latest-version, last-updated, and payload
- [x] 4.2 Apply the fallback authorization policy: authenticated, non-`cms-webhook`; admin sees all published entities, regular users see published and not disabled
- [x] 4.3 Integration tests: admin vs regular-user listing, unpublished excluded, anonymous → `401`

## 5. Admin write side

- [x] 5.1 Implement `POST /entities/{id}/disable` and `POST /entities/{id}/enable` command handlers (idempotent, `404` for unknown ids, no request body, `204` empty success)
- [x] 5.2 Apply the administrator-only policy to the enable/disable endpoints
- [x] 5.3 Integration tests: disable/enable flows, idempotency, `404`, `403` for regular users and for `cms-webhook`

## 6. Health, OpenAPI, Scalar

- [x] 6.1 Anonymous `/health`, `/openapi/v1.json`, and always-on Scalar UI mirroring the CmsWebhook pattern
- [x] 6.2 Tests: discovery endpoints anonymous; the OpenAPI contract describes `/entities`, `/entities/{id}/disable`, and `/entities/{id}/enable`

## 7. Docs and spec sync

- [x] 7.1 Move the User API section of `docs/architecture.md` from planned to implemented
- [x] 7.2 Update `docs/dsl_glossary.md` and `README.md` with the reserved users and endpoints
