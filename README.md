# Queue API Exercise

A platform-agnostic **.NET 9** API solution that accepts messages from an external CMS, records them, and processes them asynchronously into a database. It provides two APIs on top of shared Basic-Auth credentials and the same stores: the **CmsWebhook API** (`POST /cms/events`) ingests CMS events, and the **Users API** (`GET /entities`, admin-only `POST /entities/{id}/disable|enable`) serves the resulting entity store to regular users and the administrator.

**Docs site:** [queue-api-exercise docs](https://ZippoLag.github.io/queue-api-exercise/) — generated API reference and conceptual docs, rebuilt on every push to `main`.

## Quickstart

### Via Docker Compose

One command runs everything — the credential-store seeding plus both APIs against one shared volume
(requires Docker installed on your **host OS**; the [dev container](.devcontainer/devcontainer.json) is
itself a Docker container and does not include a Docker daemon):

```bash
docker compose up        # first run builds the images; starts init + both APIs
```

- **CMS Webhook API** → http://127.0.0.1:5264
- **Users API** → http://127.0.0.1:5265

The stores live in the `queue-db` named volume. `docker compose down` stops the stack and keeps the
stores; `docker compose down -v` also deletes them, and the next `docker compose up` re-seeds the
credential store automatically.

### Without Docker compose (manual execution)

The following works whether you're running from within the provided devcontainer in a console, or in your host OS (provided you have the **.NET 9 SDK** and bash available):

```bash
# from the project root
dotnet restore
dotnet build
# one-time: seeds the local credential store (src/CmsWebhook/CmsWebhook.Api/db/queue-auth.db) with the
# cms-webhook, administrator and regular-user users (passwords default to the local-development defaults)
./scripts/init-db.sh
dotnet run --project src/CmsWebhook/CmsWebhook.Api   # CMS Webhook API on http://127.0.0.1:5264
dotnet run --project src/Users/Users.Api             # Users API on http://127.0.0.1:5265
```

### Using/Testing the APIs:
> The local stores live under `src/CmsWebhook/CmsWebhook.Api/db/`; the Users API points its base path at the
> same directory so both APIs share the credential and entity stores (relative data sources are resolved
> against `Data:DbBasePath` or the content root — see [Configuration](docs/configuration.md)).

Both APIs fail fast at startup if the credential store is missing (or, for the Users API, lacks the
`administrator` user).

```bash
# sanity check: anonymous liveness probes (no credentials)
curl http://127.0.0.1:5264/health
curl http://127.0.0.1:5265/health

# send a CMS event (expect 201; it is then processed asynchronously into the entity store)
curl -u cms-webhook:0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61 -X POST \
  -H "Content-Type: application/json" \
  -d '{"type":"publish","id":"entity-1","payload":{"title":"hello"},"version":1,"timestamp":"2024-01-01T00:00:00Z"}' \
  http://127.0.0.1:5264/cms/events

# list entities as a regular user (expect 200 with the published, enabled entities)
curl -u regular-user:6d5c4b3a-2f1e-4d0c-9b8a-7f6e5d4c3b2a http://127.0.0.1:5265/entities

# the administrator sees all published entities and can hide one from regular users (expect 204)
curl -u administrator:a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d http://127.0.0.1:5265/entities
curl -u administrator:a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d -X POST http://127.0.0.1:5265/entities/entity-1/disable
curl -u administrator:a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d -X POST http://127.0.0.1:5265/entities/entity-1/enable
```

> The passwords above are the local-development defaults — DO NOT use them outside local development. Serve
> production over TLS (HTTPS). See [Configuration](docs/configuration.md).

## Deployment

The API is a plain .NET publish with no repository-marker dependency — `dotnet publish` and run the produced executable from any directory. Before starting, point the stores at writable locations and select the environment via environment variables (full chain and matrix in [Configuration](docs/configuration.md)):

```bash
export ASPNETCORE_ENVIRONMENT=Production   # or Staging
export Data__DbBasePath=/var/lib/queue-api
export ConnectionStrings__AuthDb="Data Source=/var/lib/queue-api/auth.db"
export ConnectionStrings__CmsDb="Data Source=/var/lib/queue-api/cms.db;Default Timeout=30"
# secrets are never committed: user-secrets in Development, environment variables in Staging/Production
```

## Continuous Integration

Every push and pull request runs the **CI workflow** (`.github/workflows/ci.yml`, two jobs on `ubuntu-latest`) with these quality gates:

| Gate | Enforced by | Where the threshold lives |
|------|-------------|---------------------------|
| Compiler/analyzer warnings fail the build | `TreatWarningsAsErrors` in root `Directory.Build.props` | — |
| SDK family pinned | `global.json` (`.NET 9`, `rollForward: latestFeature`) | — |
| Test suite passes with coverage collection | `dotnet test --collect:"XPlat Code Coverage"` | — |
| **Coverage ratchet** | `scripts/check-coverage.sh` | `.config/coverage-min.txt` |
| **End-to-end smoke tests** | dedicated `end-to-end` job (`dotnet test tests/E2E/QueueApi.E2E.Tests`) | — |
| Spec discipline | `openspec validate --all` (pinned CLI) | — |

The `build-and-test` job runs the per-module unit/integration suites and the coverage ratchet; the
**`end-to-end` job** runs both APIs against one shared store twice — once through the in-process test
host (the smoke tests live outside `QueueApi.slnx` on purpose, so the blanket solution run stays fast)
and once against the real deployment path (`scripts/smoke-e2e.sh`: publishes both APIs, seeds a real
credential store with `scripts/init-db.sh`, and drives the vertical over real HTTP).

### Reproduce the checks locally

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

### The coverage ratchet

`scripts/check-coverage.sh` merges every test project's `coverage.cobertura.xml` into a **unique-line union** — each source line counts once, covered if *any* test project covers it (the honest "every line tested by someone" measure; per-report summing would double-count shared assemblies) — and fails when the rate drops below the committed threshold in `.config/coverage-min.txt` (**100.0%** — the measured rate is deterministic at 100.00%, so any uncovered line now fails CI). The number only ever moves **up**: to raise it deliberately, raise coverage, then edit the threshold file (see [Development style](docs/development-style.md)).

## Documentation

- **Hosted site**: <https://ZippoLag.github.io/queue-api-exercise/> — a DocFX-generated static site (API reference from the XML doc comments + the conceptual Markdown below), rebuilt on every push to `main`
- [Architecture](docs/architecture.md) — system overview, design decisions, API and event-processing semantics
- [Domain glossary](docs/dsl_glossary.md) — domain specific language: terminology and nomenclature
- [Development style](docs/development-style.md) — development approach, AI assistance, and tooling setup
- [Configuration](docs/configuration.md) — credentials, environment variables, TLS

The **canonical documentation sources remain these Markdown files and the OpenSpec specs** (`openspec/specs`) — the hosted site is a generated view of them, never a separate copy.
