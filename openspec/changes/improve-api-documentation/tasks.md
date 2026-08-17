# Tasks

## 1. CMS Webhook API OpenAPI contract

- [x] 1.1 Declare the `POST /cms/events` request body via a document transformer: a `oneOf` schema with a single-event-object branch and a batch-array branch, per-field descriptions, and the `type` enum (`publish`, `update`, `unPublish`, `delete`, case-sensitive)
- [x] 1.2 Declare the correct responses for `POST /cms/events` (201 accepted, 400 invalid body, 401 unauthorized) with descriptions, replacing the generated default 200
- [x] 1.3 Add a basic HTTP security scheme in `components` and apply a security requirement to every operation except `/health` and the OpenAPI/Scalar endpoints
- [x] 1.4 Add request/response examples to `POST /cms/events` and verify the `servers` entry uses the request scheme over HTTPS (add a server transformer if the generator hardcodes http)

## 2. Users API OpenAPI contract

- [x] 2.1 Declare the `GET /entities` response schema (array of items with `id`, `isVisibleByAdmin`, `latestVersion`, `updatedAt`, `payload`) and its responses (200, 401)
- [x] 2.2 Correct the `POST /entities/{id}/disable` and `/enable` responses to 204/401/403/404 with descriptions, replacing the generated default 200
- [x] 2.3 Add the basic HTTP security scheme in `components` and a security requirement on every protected operation (including the administrator-only enable/disable)
- [x] 2.4 Add request/response examples and verify the `servers` entry scheme over HTTPS

## 3. Conceptual API documentation

- [x] 3.1 Create `docs/api-contract.md`: per-endpoint acceptable requests (single object or batch for ingestion), expected responses with status codes, event-type semantics (`publish`/`update`/`unPublish`/`delete`, the capital-P wire value, the no-prior-publish corner case, delete = hard delete vs unPublish = hidden), role behavior (regular/administrator/cms-webhook), and failure modes
- [x] 3.2 Wire `docs/api-contract.md` into the DocFX `toc.yml` and the README documentation list, linking to `/openapi/v1.json` and the Scalar UI as the machine contract

## 4. Tests

- [x] 4.1 Extend `CmsWebhookApiOpenApiTests` to assert the contract: `POST /cms/events` documents 201/400/401, the requestBody `oneOf` covers the single-object and batch forms, the `type` enum includes `unPublish`, and the basic security scheme is declared
- [x] 4.2 Extend `UsersApiOpenApiTests` to assert the contract: `GET /entities` documents 200/401 with the item schema, disable/enable document 204/401/403/404, and the basic security scheme is declared
- [x] 4.3 Keep the existing anonymous-access, Scalar-UI, and auth-carve-out tests passing (regression)

## 5. Verification

- [x] 5.1 Run `dotnet build` + `dotnet test` (coverage ratchet stays at 100.0%)
- [ ] 5.2 Inspect the locally served `/openapi/v1.json` for both APIs and confirm the corrected contract; verify on the live demo environment after a CI deploy
- [x] 5.3 Run `openspec validate --all` and the DocFX build (new page renders in the site nav, no broken links)
