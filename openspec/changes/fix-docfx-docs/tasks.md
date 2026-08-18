## 1. Fix the delete-event wording

- [ ] 1.1 Reword the `delete` row of the Event semantics table in `docs/api-contract.md` from "The entity was removed/unpublished for good." to "The entity was deleted — removed from the store unrecoverably." (keep the existing third column "**Hard-deletes** the entity from the store." and the "Why delete and unPublish differ" paragraph unchanged)

## 2. Fix stale XML doc comments

- [ ] 2.1 In `src/CmsWebhook/CmsWebhook.Domain/CmsEntity.cs`: class `<remarks>` — change "used by the deferred Users API" to "used by the Users API"; `IsVisibleByAdmin` `<summary>` — change "from the (future) Users API" to "from the Users API"
- [ ] 2.2 In `src/CmsWebhook/CmsWebhook.Infrastructure/CmsDbContext.cs`: `<remarks>` — change "the processed state the deferred Users API will read" to "the processed state the Users API reads"
- [ ] 2.3 Build the solution (`dotnet build`) to confirm the XML comment edits introduce no compiler warnings (warnings are errors)

## 3. Regression scan

- [ ] 3.1 Create `scripts/check-docs-wording.sh`: grep the DocFX sources (`docs/**` markdown and `src/**` XML comments) for the banned phrases — "removed/unpublished for good", "deferred Users API", "(future) Users API" — exiting non-zero on any match, and excluding `docs/archived/**`
- [ ] 3.2 Run the script locally and confirm it passes after the wording fixes and fails if a banned phrase is reintroduced
- [ ] 3.3 Add a step to `.github/workflows/docs.yml` running `bash scripts/check-docs-wording.sh` before the `dotnet docfx build` step

## 4. Verification

- [ ] 4.1 Run `dotnet build` and `dotnet test` for the whole solution; confirm zero failures
- [ ] 4.2 Run `dotnet docfx build` (if the tool is available) and confirm the site renders the corrected delete wording and the fixed API reference comments with no broken links; otherwise confirm `docfx.json` covers the changed sources unchanged
