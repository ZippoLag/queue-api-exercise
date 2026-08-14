## 1. Configuration resolution

- [x] 1.1 Replace the repository-marker walk in `Program.cs` with base-path resolution: relative data sources resolve against `Data:DbBasePath`, falling back to the content root; absolute and in-memory data sources pass through unchanged; create the resolved directory when missing before opening the store
- [x] 1.2 Delete `FindRepositoryRoot` and the `QueueApi.slnx` marker walk
- [x] 1.3 Ensure user-secrets are wired for Development (`UserSecretsId` on `CmsWebhook.Api.csproj`)

## 2. Environment configuration

- [x] 2.1 Add `appsettings.Staging.json` and `appsettings.Production.json` with non-sensitive defaults only (no committed connection strings or secrets)
- [x] 2.2 Document the precedence chain and the double-underscore environment-variable convention

## 3. Tooling alignment

- [x] 3.1 Update `scripts/init-db.sh` default `DB_PATH` to the API project's `db/` directory
- [x] 3.2 Change `.gitignore` from `db/*.db` to `**/db/*.db*`

## 4. Tests

- [x] 4.1 Precedence tests: environment variable overrides file values; environment file overrides base; user-secrets apply in Development and are ignored outside it
- [x] 4.2 Base-path tests: relative data source resolves against the configured base path; absolute and in-memory sources pass through; startup succeeds from a deployment-like directory with no repository marker; the database directory is created when missing

## 5. Documentation

- [x] 5.1 Rewrite `docs/configuration.md`: precedence chain, per-environment matrix, secrets guidance (user-secrets in dev, environment variables in staging/prod), `Data:DbBasePath`
- [x] 5.2 Align the persistence section of `docs/architecture.md`
- [x] 5.3 Update `README.md` quickstart and deployment guidance
