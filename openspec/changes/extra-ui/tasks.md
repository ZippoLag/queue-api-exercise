## 1. Scaffold the WASM client

- [x] 1.1 Create `src/Users/Users.Web` as a `blazorwasm` client (Wasm-only, .NET 9, no PWA) and add it to `QueueApi.slnx`
- [x] 1.2 Add `[assembly: ExcludeFromCodeCoverage]` to the client assembly (design D6) and set `AdministratorUsername` (default `administrator`) in its `appsettings.json`
- [x] 1.3 Add the `ProjectReference` to `Users.Web` and the `Microsoft.AspNetCore.Components.WebAssembly.Server` package to `Users.Api` (design D1)

## 2. Serve the UI from the Users API

- [x] 2.1 Add anonymous static-file serving + `UseBlazorFrameworkFiles()` + `MapFallbackToFile("index.html").AllowAnonymous()` to `Users.Api/Program.cs`, placed before the auth middleware (design D2)
- [x] 2.2 Add `WebApplicationFactory` tests: `GET /` returns the shell anonymously, a client-route path falls back to the shell, and existing endpoints/auth behavior is unchanged
- [ ] 2.3 Run the Users API test suite and `bash scripts/check-coverage.sh` — the 100% ratchet must hold

## 3. Implement the UI

- [ ] 3.1 Login page: collect username/password, set the Basic `Authorization` header on the same-origin `HttpClient`, surface `401`/`403` (including the `cms-webhook` rejection) as an inline error (design D3)
- [ ] 3.2 Entity table from `GET /entities` showing id, visibility flag, version, update time, and payload as JSON (spec: Users API hosts a browser UI)
- [ ] 3.3 Administrator toggle column (Disable/Enable per row) calling the existing enable/disable endpoints, re-fetching after `204`, inline error on `404`/`401`/`403`; regular user's table omits the column (design D5)
- [ ] 3.4 Verify the app locally against the seeded store: administrator sees the toggle and toggles an entity, regular user has no toggle column, `cms-webhook` sign-in shows the descriptive error

## 4. Deployment and pipeline

- [ ] 4.1 Extend `scripts/deploy-aws.sh`: the Users API publish must emit the client (project-reference publish) and the live verification gains a check that the users host root serves the UI shell
- [ ] 4.2 Add the UI-shell check to `scripts/smoke-e2e.sh`
- [ ] 4.3 Do a local `dotnet publish` of `Users.Api` for `linux-arm64` and confirm the UI shell and `_framework` files are in the output

## 5. Documentation and final validation

- [ ] 5.1 Update `docs/architecture.md`, `docs/api-contract.md`, `docs/deployment-aws.md`, and `README.md` for the served UI (origin root, role-based views, anonymous shell, deploy verification)
- [ ] 5.2 Final gates: `openspec validate --all`, `dotnet test QueueApi.slnx`, `bash scripts/check-coverage.sh`, and `openlore drift` all clean
