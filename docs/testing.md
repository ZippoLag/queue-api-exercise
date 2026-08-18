# Testing

**General concept — what is tested.** A .NET solution with per-module unit/integration suites, a solution-wide coverage gate, an end-to-end suite that exercises both APIs over one shared store, and a CI workflow that runs them all on every push and pull request.

**In this project** the suite lives under [`tests/`](https://github.com/ZippoLag/queue-api-exercise/tree/main/tests), one project per module: `tests/CmsWebhook/CmsWebhook.*.Tests`, `tests/Users/Users.*.Tests`, `tests/Shared/QueueApi.Auth.Tests` — plus `tests/E2E/QueueApi.E2E.Tests`, which deliberately lives **outside** `QueueApi.slnx` so the blanket solution run stays a fast per-module suite.

**Why this shape.** Unit coverage must include all corner cases and must cite the source business rule; the E2E project is excluded from the solution so the fast per-module gate stays fast, and is run explicitly (see [End-to-end testing](#end-to-end-e2e-testing)).

## TDD loop

Development follows TDD as much as possible: write the failing test for the business rule → implement → run the suite → confirm the coverage gate still passes (see [Coverage ratchet](#the-coverage-ratchet)).

## CI quality gates

Every push and pull request runs the **CI workflow** (`.github/workflows/ci.yml`, on `ubuntu-latest`):

| Gate | Enforced by | Where the threshold lives |
|------|-------------|---------------------------|
| Compiler/analyzer warnings fail the build | `TreatWarningsAsErrors` in root `Directory.Build.props` | — |
| SDK family pinned | `global.json` (`.NET 9`, `rollForward: latestFeature`) | — |
| Test suite passes with coverage collection | `dotnet test --collect:"XPlat Code Coverage"` | — |
| **Coverage ratchet** | `scripts/check-coverage.sh` | `.config/coverage-min.txt` |
| **End-to-end smoke tests** | dedicated `end-to-end` job (`dotnet test tests/E2E/QueueApi.E2E.Tests`) | — |
| Spec discipline | `openspec validate --all` (pinned CLI) | — |
| **Terraform footprint review** | `tf-validate` job (`terraform fmt -check` + `terraform validate`) | `infra/aws/` |
| **Deploy on `main`** | `deploy` job (push to `main` only, after the `build-and-test` and `end-to-end` jobs) | [Deployment](deployment-aws.md) |

The `build-and-test` job runs the per-module unit/integration suites and the coverage ratchet; the **`end-to-end` job** runs both APIs against one shared store twice — once through the in-process test host and once against the real deployment path. A push to `main` that passes the `build-and-test` and `end-to-end` jobs additionally runs the **`deploy` job** (see [Deployment](deployment-aws.md)).

## The coverage ratchet

`scripts/check-coverage.sh` merges every test project's `coverage.cobertura.xml` into a **unique-line union** — each source line counts once, covered if *any* test project covers it (the honest "every line tested by someone" measure; per-report summing would double-count shared assemblies) — and fails when the rate drops below the committed threshold in `.config/coverage-min.txt` (**100.0%** — the measured rate is deterministic at 100.00%, so any uncovered line now fails CI). The number only ever moves **up**: to raise it deliberately, raise coverage, then edit the threshold file.

### Raising the coverage threshold

The committed number in `.config/coverage-min.txt` is a **ratchet**: it currently sits at **100.0%** — the union metric measures deterministically at 100.00% (956/956) across consecutive clean runs, so any newly-added uncovered line fails CI. To raise (or, in the future, adjust) it deliberately:

1. Improve coverage (new tests) and run `bash scripts/check-coverage.sh` to confirm the new unique-line rate.
2. Edit `.config/coverage-min.txt` to a value at or below the new measured rate (leave a small margin for machine-to-machine variance).
3. Commit the threshold change together with the tests that justify it.

### Determinism practices

The union metric is **deterministic**: repeated clean runs report the same unique-line rate (100.00%, 956/956 across consecutive runs). Two practices keep it that way:

- Integration tests that record events wait for the async outbox worker to finish (e.g. await the event's `Processed` status with `AsNoTracking`) before disposing the test factory.
- The entry-point shims (`tools/AuthDbInit/Program.cs`, exercised via the assembly entry point; `CmsWebhook.Api/Program.cs` and `Users.Api/Program.cs`, exercised through `WebApplicationFactory`) are covered rather than excluded.

Unknown path prefixes fail the script loudly so the normalization table (documented in the script header) is extended rather than silently mis-measured.

## End-to-end (E2E) testing

The `end-to-end` CI job validates that both APIs interoperate over **one shared store**, in two layers:

1. **Test host** (`tests/E2E/QueueApi.E2E.Tests`) — xunit scenarios via `WebApplicationFactory`, running the CMS Webhook and Users APIs in-process against one seeded credential store and one CMS database. Scenarios cover the full vertical: CMS event ingestion → outbox processing → the regular-user listing → the administrator's disable/enable → `cms-webhook` rejected on the Users API → a CMS update event not resetting the administrator's disable.
2. **Real processes** (`scripts/smoke-e2e.sh`) — the same vertical against the deployment path: `dotnet publish -c Release` both APIs, seed a real credential store through `scripts/init-db.sh`, start both executables as real processes over real SQLite files (Production environment, stores supplied via environment variables), and drive the flow over real HTTP with curl status assertions. A `trap` kills the processes and removes the temp stores on every exit path.

**Vertical coverage.** Both layers assert the acceptance path — ingest → outbox processing → listing → administrator visibility control — and the deterministic rejection contract of both APIs: `401` for a request without an `Authorization` header, `400` for a non-RFC 3339 timestamp, `400` for a non-object payload, `400` for an empty or whitespace-only route id, `403` for the reserved `cms-webhook` user on the Users API, and `404` for an unknown entity id. Rejected ingestions are proven to record nothing: their unique entity id never appears on the Users API listing. The status-code × layer inventory below is the single reviewable source of truth for what the smoke vertical asserts — a contract change that adds or alters a status code must update this table and the matching layer(s) in the same change (spec: "End-to-end smoke gates cover the documented contract").

| Status | In-process E2E | Real-process smoke |
|---|---|---|
| `200` (health, listing) | ✓ | ✓ |
| `201` (ingest accepted) | ✓ | ✓ |
| `204` (enable/disable, incl. trimmed padded id) | ✓ | ✓ |
| `400` (invalid timestamp / non-object payload / whitespace-only id) | ✓ | ✓ |
| `401` (request without credentials) | ✓ | ✓ |
| `403` (reserved `cms-webhook` on the Users API) | ✓ | ✓ |
| `404` (unknown entity id) | ✓ | ✓ |
| `429` (rate limit) | ✗ — API integration suite only (overridden permit limit) | ✗ — timing-sensitive, excluded by design |

**Conventions:**

- The E2E project lives outside `QueueApi.slnx` on purpose; the slnx carries a comment pointing here.
- Both APIs declare their `Program` entry-point type in the global namespace, so the E2E project references the two API projects with `Aliases` (`extern alias cmswebhook` / `users`) while referencing the infrastructure and shared-auth projects directly for global visibility.
- Async outbox processing is awaited by polling (the regular-user listing for entity presence, the administrator listing for a written version) with a timeout, so scenarios never assert before the worker has applied the event.
- Each scenario gets a fresh `E2EEnvironment` (temp stores + both hosts), so tests stay independent and parallel-safe; the smoke script's stores live in a `mktemp -d` directory removed by its cleanup trap.

## Reproduce the checks locally

The CI steps are plain `dotnet` commands; run them in order from the repo root:

```bash
dotnet restore QueueApi.slnx
dotnet build QueueApi.slnx --no-restore          # warnings fail the build
dotnet test QueueApi.slnx --no-build --no-restore --collect:"XPlat Code Coverage"
bash scripts/check-coverage.sh                   # aggregate coverage gate
openspec validate --all                          # spec discipline gate

# the end-to-end job runs the smoke tests explicitly (not part of the solution run)
dotnet test tests/E2E/QueueApi.E2E.Tests/QueueApi.E2E.Tests.csproj
bash scripts/smoke-e2e.sh                      # same scenario against real processes and stores
```

## See also

- [Development style](development-style.md) — the TDD-first approach and code conventions behind the suite.
- [Debugging](debugging.md) — how the debug-mode stacks relate to the E2E in-process hosts.
- [Deployment](deployment-aws.md) — the CI `deploy` job that runs after these gates on `main`.
