# Queue API Exercise

A platform-agnostic **.NET 9** API solution that accepts messages from an external CMS, records them, and processes them asynchronously into a database. The current implementation provides the **CmsWebhook API** (`POST /cms/events`) with shared Basic-Auth credentials; a Users API reading the resulting entity store is planned.

## Quickstart

> The repo provides a dev container with all dependencies and VSCode extensions. Otherwise you need the **.NET 9 SDK**.

```bash
# from the project root
dotnet restore
dotnet build
./scripts/init-db.sh # one-time: seeds the local credential store (db/queue-auth.db) with the cms-webhook user
dotnet run --project src/CmsWebhook/CmsWebhook.Api
```

The API starts on `http://127.0.0.1:5264` and fails fast at startup if the credential store is missing or the CMS database is unreachable.

```bash
# sanity check: anonymous liveness probe (no credentials)
curl http://127.0.0.1:5264/health

# send a CMS event (expect 201; it is then processed asynchronously into the entity store)
curl -u cms-webhook:0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61 -X POST \
  -H "Content-Type: application/json" \
  -d '{"type":"publish","id":"entity-1","payload":{"title":"hello"},"version":1,"timestamp":"2024-01-01T00:00:00Z"}' \
  http://127.0.0.1:5264/cms/events
```

> The password above is the local-development default — DO NOT use it outside local development. Serve production over TLS (HTTPS). See [Configuration](docs/configuration.md).

## Continuous Integration

Every push and pull request runs the **CI workflow** (`.github/workflows/ci.yml`, one lean job on `ubuntu-latest`) with these quality gates:

| Gate | Enforced by | Where the threshold lives |
|------|-------------|---------------------------|
| Compiler/analyzer warnings fail the build | `TreatWarningsAsErrors` in root `Directory.Build.props` | — |
| SDK family pinned | `global.json` (`.NET 9`, `rollForward: latestFeature`) | — |
| Test suite passes with coverage collection | `dotnet test --collect:"XPlat Code Coverage"` | — |
| **Coverage ratchet** | `scripts/check-coverage.sh` | `.config/coverage-min.txt` |
| Spec discipline | `openspec validate --all` (pinned CLI) | — |

### Reproduce the checks locally

The CI steps are plain `dotnet` commands; run them in order from the repo root:

```bash
dotnet restore QueueApi.slnx
dotnet build QueueApi.slnx --no-restore          # warnings fail the build
dotnet test QueueApi.slnx --no-build --no-restore --collect:"XPlat Code Coverage"
bash scripts/check-coverage.sh                   # aggregate coverage gate
openspec validate --all                          # spec discipline gate
```

### The coverage ratchet

`scripts/check-coverage.sh` merges every test project's `coverage.cobertura.xml` into a **unique-line union** — each source line counts once, covered if *any* test project covers it (the honest "every line tested by someone" measure; per-report summing would double-count shared assemblies) — and fails when the rate drops below the committed threshold in `.config/coverage-min.txt` (**100.0%** — the measured rate is deterministic at 100.00%, so any uncovered line now fails CI). The number only ever moves **up**: to raise it deliberately, raise coverage, then edit the threshold file (see [Development style](docs/development-style.md)).

## Documentation

- [Architecture](docs/architecture.md) — system overview, design decisions, API and event-processing semantics
- [Domain glossary](docs/dsl_glossary.md) — domain specific language: terminology and nomenclature
- [Development style](docs/development-style.md) — development approach, AI assistance, and tooling setup
- [Configuration](docs/configuration.md) — credentials, environment variables, TLS
