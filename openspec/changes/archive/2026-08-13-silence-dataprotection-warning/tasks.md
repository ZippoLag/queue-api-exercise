## 1. Configuration Change

- [x] 1.1 Add `"Microsoft.AspNetCore.DataProtection": "Error"` to the `Logging.LogLevel` object in `src/CmsWebhook/CmsWebhook.Api/appsettings.json` (design decision 1; silences the `Warning`-level key-ring message, keeps errors visible)

## 2. Verification

- [x] 2.1 Run `dotnet build QueueApi.slnx` — 0 warnings, 0 errors
- [x] 2.2 Run `dotnet test` on the whole solution — all tests pass and no `FileSystemXmlRepository[60]` warning appears in the output
- [x] 2.3 Run the API (`dotnet run --project src/CmsWebhook/CmsWebhook.Api`) once and confirm the Data Protection warning no longer appears at startup
- [x] 2.4 Code review of the config change (category spelling matches the logged namespace, override present in the correct file)
