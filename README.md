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

For debugging (breakpoints, hot reload) see [Debugging](#debugging) — the production-image stack is
**not** the debugging surface.

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

## Debugging

Three surfaces, in order of simplicity. Only **one runs at a time**: the composed stack and host
launches bind the same host ports (`5264`/`5265`).

All three dev surfaces share **one store**: `src/CmsWebhook/CmsWebhook.Api/db/` — the debug containers
bind-mount that same folder. Only the production-image stack (`docker compose up` without `-f`) uses
the `queue-db` volume, so data written there is invisible to the dev surfaces (and vice versa).

### 1. Host — simplest

F5 in VS Code — the `Both APIs (host)` compound profile or the per-project profiles in
`.vscode/launch.json` — or from a console:

```bash
./scripts/init-db.sh          # once: seeds the credential store
# in two terminals:
dotnet run --project src/CmsWebhook/CmsWebhook.Api   # CMS Webhook API on :5264
dotnet run --project src/Users/Users.Api             # Users API on :5265
```

Breakpoints bind, hot reload applies, and the stores live under `src/CmsWebhook/CmsWebhook.Api/db/` —
the same files the debug containers use.

### 2. Devcontainer

Same as host debugging, from the devcontainer console (ports `5264`/`5265` are forwarded to your host
browser). The devcontainer has no Docker daemon, so it is for this surface only — not the containers
below.

> Note: if you are using the devcontainer, you will not be able tu successfully run the project in your host OS or using `docker compose` if you happen to try.

### 3. Containers — full stack parity, hot reload

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

Runs both APIs **from source** with `dotnet watch` (Debug builds, hot reload) against the **same host
`db/` stores your F5 runs use** — entities written in either surface appear in the other. Requires
Docker on the host and Docker Compose ≥ 2.24.4 (the override uses the `!reset` merge tag). The
`.vscode/tasks.json` `compose: up (debug)` task runs the same command; tear down with the same `-f`
file set (`... down`).

**Attaching to the containers** — the `Attach: ... (container)` profiles in `.vscode/launch.json`
target processes on your host's Docker engine, so they require VS Code running **on the host OS** (the
devcontainer has no Docker access). Run the `compose: up (debug)` task, then attach. The `host`
profiles work from any VS Code instance with the .NET SDK — including inside the devcontainer.

> **Linux hosts only**: files the debug containers create in `db/` are owned by root; after a debug
> session run `sudo chown -R $(id -u) src/CmsWebhook/CmsWebhook.Api/db` to restore host ownership.
> Docker Desktop (macOS/Windows) maps uids and needs none of this.

**The trap when mixing modes:**

- **Port collision** — the stack and host launches both bind `5264`/`5265`; stop one before starting the other.

> **DevContainer port collision**: note that the devcontainer is configured to always forward the ports, so if you have the devcontainer open you will not be able to succesfully run these projects outside of it in your host OS, whether you're using `docker compose...` or `dotnet run...`.

## Deployment

The APIs deploy to **AWS** as plain .NET publishes on a single node — no containers, no load
balancer, ~$1–9/mo. The full footprint is infrastructure-as-code under [`infra/aws/`](infra/aws/)
(Terraform); a copy-pastable script bootstraps it from the AWS console.

### One paste, from the AWS console

Open **AWS CloudShell** (the console's built-in terminal) and paste [`scripts/bootstrap-aws.sh`](scripts/bootstrap-aws.sh):
it clones this repository, installs Terraform, generates fresh passwords, creates the whole
environment, and performs the first deploy. **Change the `REGION` variable first — it defaults to
`eu-west-3` (Paris).** Re-running is safe (passwords are reused, terraform is idempotent).

```bash
# in AWS CloudShell — edit REGION/ENV_NAME/DOMAIN at the top of the script first
bash <(curl -fsSL https://raw.githubusercontent.com/ZippoLag/queue-api-exercise/main/scripts/bootstrap-aws.sh)
```

> CloudShell runs as your logged-in console identity — no access keys. Terraform and its provider
> plugins (the ~600 MB AWS provider) are installed under `/tmp` (ephemeral), so they don't consume
> CloudShell's 1 GB persistent `$HOME` — only the small clone and its Terraform state live there. On a
> fresh account the artifact bucket is empty, so the script installs the .NET SDK and builds locally
> for the first deploy; pass `--infra-only` to skip that and let the CI deploy job (GitHub Actions)
> publish and ship the artifacts instead (set the secrets from the printed report, then push to `main`).

### Topology

```
        client (https://cms.<domain> / users.<domain>   or   https://<public-ip>[:8443])
                                  │
                     ┌────────────▼────────────┐
                     │  Caddy on the instance  │   TLS termination: Let's Encrypt (with a domain)
                     │  HTTP→HTTPS redirect    │   or self-signed `tls internal` (domainless)
                     └────────────┬────────────┘
                      ┌───────────┴───────────┐
                      ▼                       ▼
                 localhost:8080           localhost:8081
                 CmsWebhook.Api           Users.Api
                      └───────────┬───────────┘
                                  ▼
                   SQLite stores on EBS (/var/lib/queue-api)
```

- **One `t4g.small` node** hosts both APIs as systemd services (the shared SQLite store requires
  one host; scaling out is explicitly out of scope until the store moves off SQLite).
- **TLS terminates on the instance** (Caddy): the ~$16/mo ALB the APIs don't need is gone.
- **Secrets live in SSM Parameter Store** (`SecureString`): fresh passwords are generated at
  environment creation — the committed local-development defaults are never used.
- **No SSH port**: deploys travel via SSM Run Command; the security group exposes 80/443 (plus 8443
  in the domainless variant, which serves the Users API over the public IP).
- **CI deploys `main`**: a green push publishes both APIs to the S3 artifact bucket and ships them
  to the node (see [Continuous Integration](#continuous-integration)).

### Cost

| Resource | ~$/mo | Notes |
|---|---|---|
| EC2 `t4g.small` (2 vCPU / 2 GiB) | **$0** | free trial (750 h/mo) through 2026-12-31 |
| EC2 `t4g.micro` (1 GiB) — after the trial | ~$7.50 | documented downgrade (see below) |
| EBS 8 GB gp3 (stores) | ~$0.65 | survives redeploys and stop/start |
| Elastic IP (attached) | $0 | |
| Caddy + Let's Encrypt | $0 | replaces the ALB |
| SSM Parameter Store (standard tier) | $0 | |
| S3 artifact bucket | ~$0 | tiny traffic |
| Route 53 hosted zone | $0.50 | only when `DOMAIN` is set |
| **Total today** | **~$1.20** | ~$0.65 without a domain |
| **Total after the trial** | **~$8.70** | after the `t4g.micro` downgrade |

### Deploying

- **Automatically**: a push to `main` that passes the `build-and-test` and `end-to-end` gates deploys
  and verifies itself (`.github/workflows/ci.yml` → `deploy` job; requires the GitHub secrets below).
- **Manually** (reuses the same path):

  ```bash
  REGION=eu-west-3 ENV_NAME=demo \
  S3_BUCKET=<from terraform output> INSTANCE_ID=<from terraform output> \
  DOMAIN="" bash scripts/deploy-aws.sh
  ```

- **Rollback**: `scripts/deploy-aws.sh --rollback` restores the previous artifacts kept on the
  node as `*.previous` (or re-run the deploy job with a prior S3 object version).
- **Tear down**: `scripts/teardown-aws.sh` destroys the whole Terraform-managed footprint. Run it
  from CloudShell against the same clone that holds the local state (it disables the node's
  termination protection first); pass `--all` to also delete the local clone and the Terraform
  install.

### CI deploy — GitHub secrets and vars

The `deploy` job authenticates to AWS with the **OIDC role Terraform creates** for `GITHUB_ORG`,
scoped to the single `GITHUB_REPO` (`queue-api-deploy-<env>`), so no access keys are ever stored
in GitHub. It still needs three
**repository secrets** (Settings → Secrets and variables → Actions) — set them after the first
bootstrap run, reading the values from the bootstrap report / `terraform output`:

| Name | Kind | Value |
|------|------|-------|
| `AWS_ACCOUNT_ID` | Secret | your AWS account id: `aws sts get-caller-identity --query Account --output text` |
| `AWS_S3_BUCKET` | Secret | artifact bucket: `terraform output -raw artifact_bucket` |
| `AWS_INSTANCE_ID` | Secret | node instance id: `terraform output -raw instance_id` |
| `AWS_ENV_NAME` | Variable | optional — defaults to `demo` (must match what bootstrap used) |
| `AWS_REGION` | Variable | optional — defaults to `eu-west-3` (must match what bootstrap used) |
| `AWS_DOMAIN` | Variable | optional — defaults to empty (self-signed URLs on the Elastic IP) |

Until the environment exists and the secrets are set, a push to `main` will fail the `deploy` job
(and the OIDC role does not exist yet either) — **that is expected**. Once the bootstrap has run
and the secrets are in place, re-run the failed job from the Actions tab (or push again) and CI
publishes the artifacts to S3 and deploys/verifies itself.

### Manual operations

- **Password rotation** — the seed tool is a **no-op over an existing store** (existing users are left
  unchanged), and the node has the runtime only (no SDK, so the local `scripts/init-db.sh` does not
  apply there). Rotating therefore needs a fresh store: update the SSM parameter, delete the store on
  the node, then re-deploy — the published `AuthDbInit` re-seeds it from the new value and restarts
  the services:
  `aws ssm put-parameter --name /queue-api/<env>/cms-password --type SecureString --value "$(openssl rand -hex 16)" --overwrite --region eu-west-3`, then on the node
  `rm /var/lib/queue-api/queue-auth.db` (repeat the `put-parameter` for each password you rotate),
  then re-run `scripts/deploy-aws.sh`.
- **Caddy config changes** — edit `/etc/caddy/Caddyfile` on the node and apply with
  `systemctl restart caddy` (TLS blips for a second; the API services are separate units and keep
  running). A `reload` will **not** work: the Caddyfile sets `admin off`, which disables the admin
  API that `caddy reload` uses to push the new config.
- **EBS snapshots** — the stores are throwaway by design, but a manual snapshot cadence is
  documented best-practice: `aws ec2 create-snapshot --volume-id <store-volume> --region eu-west-3`.
- **Stop/start for cost savings** — `Stop instance` (not terminate) keeps the EBS stores; the
  Elastic IP stays attached and the services come back automatically (they are enabled at first
  deploy). Note the EIP is billed (~$0.005/hr) while the instance is stopped, so this only saves the
  instance-hours — meaningful once the t4g.small free trial ends. Terminate-protection is on.
- **Post-trial downgrade** — after 2026-12-31, stop the instance, change the type to `t4g.micro`
  (console: Instance settings → Change instance type), start it. The measured footprint (~285 MB
  peak for both APIs) fits 1 GiB with ~55% headroom; if you want a hard cap on the two processes,
  add `DOTNET_gcServer=0` and `DOTNET_GCHeapHardLimit` to the systemd `EnvironmentFile`s.

For the full environment-variable matrix (also usable without AWS), see
[Configuration](docs/configuration.md).

## Continuous Integration

Every push and pull request runs the **CI workflow** (`.github/workflows/ci.yml`, on `ubuntu-latest`) with these quality gates:

| Gate | Enforced by | Where the threshold lives |
|------|-------------|---------------------------|
| Compiler/analyzer warnings fail the build | `TreatWarningsAsErrors` in root `Directory.Build.props` | — |
| SDK family pinned | `global.json` (`.NET 9`, `rollForward: latestFeature`) | — |
| Test suite passes with coverage collection | `dotnet test --collect:"XPlat Code Coverage"` | — |
| **Coverage ratchet** | `scripts/check-coverage.sh` | `.config/coverage-min.txt` |
| **End-to-end smoke tests** | dedicated `end-to-end` job (`dotnet test tests/E2E/QueueApi.E2E.Tests`) | — |
| Spec discipline | `openspec validate --all` (pinned CLI) | — |
| **Terraform footprint review** | `tf-validate` job (`terraform fmt -check` + `terraform validate`) | `infra/aws/` |
| **Deploy on `main`** | `deploy` job (push to `main` only, after the `build-and-test` and `end-to-end` jobs) | [Deployment](#deployment) |

The `build-and-test` job runs the per-module unit/integration suites and the coverage ratchet; the
**`end-to-end` job** runs both APIs against one shared store twice — once through the in-process test
host (the smoke tests live outside `QueueApi.slnx` on purpose, so the blanket solution run stays fast)
and once against the real deployment path (`scripts/smoke-e2e.sh`: publishes both APIs, seeds a real
credential store with `scripts/init-db.sh`, and drives the vertical over real HTTP). A push to
`main` that passes the `build-and-test` and `end-to-end` jobs additionally runs the **`deploy`
job**: it publishes both APIs and the `AuthDbInit` tool to the S3 artifact bucket, ships them to the
AWS node via SSM Run Command (OIDC role, no stored keys), and verifies the live deployment (see
[Deployment](#deployment)).

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
