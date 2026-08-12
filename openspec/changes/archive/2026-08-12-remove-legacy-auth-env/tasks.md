## 1. API: remove the AUTH_CMS_USERNAME override

- [x] 1.1 In `Program.cs`, `ResolveCmsUsername` reads `configuration["Auth:CmsUsername"] ?? "cms-webhook"` without consulting `AUTH_CMS_USERNAME`, keeping the `[10,20]` validation and its startup failure; update the comment above it

## 2. Tooling: remove the AUTH_CMS_PASSWORD fallback

- [x] 2.1 In `scripts/init-db.sh`, the password resolves to the positional argument or the local-development default (no `AUTH_CMS_PASSWORD`), the warning branch triggers on `[ "$#" -lt 2 ]`, and the header comment drops the env-var mention

## 3. Tests

- [x] 3.1 In `CmsWebhookApiAuthTests`, replace the `AUTH_CMS_USERNAME` set/restore in `CreateClient_WhenConfiguredUsernameLengthIsInvalid_ThrowsAtStartup` with the `Auth__CmsUsername` process environment variable, and update the test's `<remarks>` to cite the config knob instead of the legacy variable

## 4. README

- [x] 4.1 Remove the `$AUTH_CMS_PASSWORD` fallback mention from the `init-db.sh` instructions and the "legacy `AUTH_CMS_USERNAME` takes precedence" clause from the credentials note, stating the single config source (`Auth:CmsUsername` / `Auth__CmsUsername`)

## 5. Validation

- [x] 5.1 Grep confirms no `AUTH_CMS_*` references remain outside historical/planning artifacts (gitignored caches excluded)
- [x] 5.2 `dotnet build` succeeds with 0 warnings and `dotnet test` passes the full suite
- [x] 5.3 Manual onboarding flow: `scripts/init-db.sh` runs with and without a password argument (idempotent), the API starts, and authenticated requests return 200 / 401 / 401 as expected
