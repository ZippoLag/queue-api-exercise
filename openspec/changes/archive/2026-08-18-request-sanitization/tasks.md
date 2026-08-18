## 1. CMS Webhook validation and sanitization

- [x] 1.1 Replace the lenient `DateTimeOffset.TryParse` in `CmsRequestValidator.TryValidate` with `DateTimeOffset.TryParseExact` against the four RFC 3339 formats of design D1 (example `2024-01-01T00:00:00Z`, numeric offset, optional fractional seconds), so date-only, culture-formatted and offset-less timestamps are rejected; update the validator's `<remarks>` to state the sanitization definition (valid + safe-to-store, payload opaque)
- [x] 1.2 Update `CmsRequestValidatorTests`: timestamp accept matrix (`2024-01-01T00:00:00Z`, `+02:00` offset, fractional seconds) and reject matrix (date-only `2024-01-01`, culture-formatted `01/01/2024`, offset-less `2024-01-01T00:00:00`, unparseable, out-of-range), plus a payload-opacity test (arbitrary nested JSON object accepted and recorded verbatim)
- [x] 1.3 Extend `CmsWebhookApiEventIngestionTests` invalid-request theory with the newly-rejected timestamp forms (date-only, culture-formatted, offset-less) asserting `400` and nothing recorded
- [x] 1.4 Add the non-object `payload` failure mode to the OpenAPI `400` description in `CmsEventEndpoints.ConfigureOpenApiOperation` and assert it in `CmsWebhookApiOpenApiTests`

## 2. Users API parity

- [x] 2.1 Trim the route `id` and reject empty-or-whitespace-only ids with `400 Bad Request` in `EntityEndpoints.DisableAsync`/`EnableAsync`, passing the trimmed id to the command handler (unknown id still `404`); document the rule in the endpoint's XML comments
- [x] 2.2 Add Users API integration tests: empty and whitespace-only id → `400` with no entity modified, whitespace-padded id resolves to the stored entity (trim-before-lookup), unknown id still `404`
- [x] 2.3 Add the `400` response ("The id is empty or whitespace-only.") to `EntityEndpoints.ConfigureSetVisibilityOperation` and assert it in `UsersApiOpenApiTests` (disable and enable both declare `400`)

## 3. Docs and glossary sync

- [x] 3.1 Add a **Sanitization** entry to `docs/dsl_glossary.md` defining the rule: accepted values are valid (non-null, non-empty, correct types) and safe to store; the event `payload` remains opaque (valid JSON object, contents and format not inspected)
- [x] 3.2 Update the Validations section of `docs/architecture.md`: strict RFC 3339 timestamp per the requirements' example, the sanitization definition, and the Users API route-id rule
- [x] 3.3 Update `docs/api-contract.md`: add the `400` row to the Users API enable/disable table (empty or whitespace-only id) and confirm the CMS Webhook `400` row already lists the non-object `payload` failure mode
- [x] 3.4 Align XML doc comments in `CmsRequest.cs` (`Timestamp`), `CmsRequestValidator.cs`, `CmsEventEndpoints.cs` and `EntityEndpoints.cs` with the new wording

## 4. Verification

- [x] 4.1 Run `dotnet build` and the full test suite (both API test projects), and `openspec validate --all`
