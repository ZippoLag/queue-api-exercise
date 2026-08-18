## Why

The DocFX site is generated from `docs/**` markdown plus the XML doc comments of `src/**`, so wording errors in either surface ship to readers. Two classes of inaccuracy exist today: the `delete` event is described in `docs/api-contract.md`'s Event semantics table as "removed/unpublished for good" — which conflates delete with `unPublish` (delete hard-removes the entity from the store unrecoverably, while `unPublish` keeps the data, merely hidden), and the API reference still carries XML doc comments calling the Users API "deferred"/"(future)", although it has been implemented. The in-flight `sanitize-openapi-docs` change deliberately leaves this narrative content alone (its `docs/api-contract.md` sync touches only rows that mirror OpenAPI wording), so these wording fixes need their own change.

## What Changes

- **`docs/api-contract.md` Event semantics table** — reword the `delete` row from *"The entity was removed/unpublished for good."* to precise, unrecoverable-removal wording (e.g. *"The entity was deleted — removed from the store unrecoverably."*), so delete and `unPublish` are never conflated. The surrounding "Why delete and unPublish differ" note already states the distinction and stays.
- **Stale XML doc comments that render in the DocFX API reference** — remove "deferred"/"(future)" from comments that describe the now-implemented Users API:
  - `src/CmsWebhook/CmsWebhook.Domain/CmsEntity.cs` — class `<remarks>` ("used by the deferred Users API") and `IsVisibleByAdmin` summary ("from the (future) Users API").
  - `src/CmsWebhook/CmsWebhook.Infrastructure/CmsDbContext.cs` — `<remarks>` ("the processed state the deferred Users API will read").
- **Regression scan** — add a check (test or CI grep) that fails when the banned delete-row wording or "deferred/future Users API" phrasing reappears in `docs/**` or `src/**` XML comments, so the DocFX site cannot silently regress.
- **Verify the DocFX build** — run `dotnet docfx build` (if the tool is available) to confirm the site renders with the corrected wording and no broken links.

No runtime behavior, schemas, or OpenAPI contract changes.

## Capabilities

### New Capabilities

- `documentation-site`: the documentation rendered by the DocFX site — the conceptual markdown and the API reference generated from XML doc comments — SHALL describe event semantics precisely (delete = unrecoverable hard removal, never conflated with `unPublish`) and SHALL reflect the implemented system state rather than stale "future"/"deferred" phrasing.

### Modified Capabilities

None.

## Impact

- **Docs**: `docs/api-contract.md` (Event semantics table, `delete` row).
- **XML comments**: `src/CmsWebhook/CmsWebhook.Domain/CmsEntity.cs`, `src/CmsWebhook/CmsWebhook.Infrastructure/CmsDbContext.cs`.
- **Tests/tooling**: a wording-regression scan (location chosen at apply time — either a test project or a CI grep step).
- **Out of scope**: `docs/archived/**` (invariant, never modified), the OpenSpec specs themselves, runtime behavior, and the OpenAPI documents (owned by `sanitize-openapi-docs`).
