# Queue API Exercise

A platform-agnostic **.NET 9** API solution that accepts messages from an external CMS, records them, and processes them asynchronously into a database. It provides two APIs on top of shared Basic-Auth credentials and the same stores: the **CMS Webhook API** (`POST /cms/events`) ingests CMS events, and the **Users API** (`GET /entities`, admin-only `POST /entities/{id}/disable|enable`) serves the resulting entity store to regular users and the administrator, and hosts a browser UI at its origin root.

**Docs site:** [queue-api-exercise docs](https://ZippoLag.github.io/queue-api-exercise/) — generated API reference and conceptual docs, rebuilt on every push to `main`.

> Note from author: the request to develop this project originally came as an exercise (hence the name); my first approach was classic: read the problem statement throughly, draft requirements and architecture by hand, format it into an initial solution design document and begin implementation discovery via TDD. Due to Life™ circumstances (and since I was _encouraged_ to use AI), my efforts went into setting-up a C#-optimized AISDLC and use it to refine requirements and guide agentic development. It has been an interesting exercise, and I'm quite content with the workflow I whipped-up (blogpost to be linked here soon), as well as with the result I steered it into developing.

## Quickstart

### Via Docker Compose

One command runs everything — the credential-store seeding plus both APIs against one shared volume
(requires Docker installed on your **host OS**):

```bash
docker compose up        # first run builds the images; starts init + both APIs
```

- **CMS Webhook API** → http://127.0.0.1:5264
- **Users API** → http://127.0.0.1:5265

The stores live in the `queue-db` named volume. `docker compose down` stops the stack and keeps the
stores; `docker compose down -v` also deletes them, and the next `docker compose up` re-seeds the
credential store automatically.

For debugging (breakpoints, hot reload) see [Debugging](#debugging) — the production-image stack is
**not** the debugging surface.

### Without Docker compose (manual execution)

The following works whether you're running from within the provided devcontainer in a console, or in your host OS (provided you have the **.NET 9 SDK** and bash available):

```bash
# from the project root
dotnet restore
dotnet build
# one-time: seeds the local credential store with the cms-webhook, administrator and regular-user users
./scripts/init-db.sh
dotnet run --project src/CmsWebhook/CmsWebhook.Api   # CMS Webhook API on http://127.0.0.1:5264
dotnet run --project src/Users/Users.Api             # Users API on http://127.0.0.1:5265
```

### Using/Testing the APIs

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

The Users API also serves a browser UI at `http://127.0.0.1:5265/` — the shell loads anonymously; sign in as `administrator` to see the toggle column or `regular-user` to see the table without it.

> The passwords above are the local-development defaults — DO NOT use them outside local development. Serve
> production over TLS (HTTPS). See [Configuration](docs/configuration.md) for the canonical listing.

## Debugging

Three debugging surfaces — host, devcontainer, and debug containers — one at a time, sharing one store,
with the port-collision and store-model traps documented. See [Debugging](docs/debugging.md).

## Deployment

The APIs deploy to **AWS** as plain .NET publishes on a single node — no containers, no load balancer,
~$1–9/mo. The full runbook (bootstrap, topology, cost, secrets, deploy/rollback/teardown, manual
operations) lives in [Deployment](docs/deployment-aws.md).

## Continuous Integration

Every push and pull request runs quality gates: warnings as errors, the test suite with the 100%
coverage ratchet, end-to-end smoke tests, spec discipline, and Terraform validation — with a deploy on
`main` when they pass. See [Testing](docs/testing.md).

## Documentation

- **Hosted site**: <https://ZippoLag.github.io/queue-api-exercise/> — a DocFX-generated static site (API reference from the XML doc comments + the conceptual Markdown below), rebuilt on every push to `main`
- [Architecture](docs/architecture.md) — system overview, design decisions, API and event-processing semantics
- [Domain glossary](docs/dsl_glossary.md) — domain specific language: terminology and nomenclature
- [Development style](docs/development-style.md) — development approach, AI assistance, and code conventions
- [Configuration](docs/configuration.md) — credentials, environment variables, TLS
- [Debugging](docs/debugging.md) — the three debugging surfaces and their traps
- [Testing](docs/testing.md) — test layout, coverage ratchet, CI gates
- [Tooling](docs/tooling.md) — Freebuff/OpenSpec/OpenLore installation and MCP wiring
- [Deployment](docs/deployment-aws.md) — AWS deployment runbook
- [API contract](docs/api-contract.md) — what each endpoint accepts, returns, and how it can fail (the machine-readable contract lives in each API's `/openapi/v1.json`, browsable via Scalar at `/scalar/v1`)

The **canonical documentation sources remain these Markdown files and the OpenSpec specs** (`openspec/specs`) — the hosted site is a generated view of them, never a separate copy.
