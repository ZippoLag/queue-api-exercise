## Why

The repo has no path from a green `main` to a running service. The Deployment section of the README stops at env-var listings — no service recommendation, no topology, no pipeline — and a first deploy hits four walls: Kestrel's default loopback-only bind in Production (`localhost:5000`) that makes remote health checks fail, committed default passwords tempting their way into production, an undocumented single-node topology forced by the shared SQLite store, and no CI/CD step that deploys at all. A junior developer cannot deploy without inventing most of the stack. This change delivers the lowest-cost AWS footprint — free-tier priced — and the pipeline that puts `main` on it, bootstrapped by a single copy-pastable script run from the AWS console.

## What Changes

- **Infrastructure as code** (Terraform, under `infra/aws/`): single-AZ VPC with one public subnet, a security group exposing only ports 80/443 (**no public SSH**), one free-tier-eligible `t4g.small` EC2 instance (2 vCPU / 2 GiB) with an attached EBS volume holding the stores, an Elastic IP, and SSM Parameter Store entries for secrets. Both APIs run on the **single node** (systemd services from `dotnet publish` output) behind a **Caddy reverse proxy on the instance** that terminates HTTPS and routes the two hostnames to the two APIs — the shared SQLite store requires one host; multi-instance scaling is explicitly out of scope until the store is swapped.
- **TLS for Basic Auth**: Caddy terminates HTTPS with Let's Encrypt certificates (auto-issued and auto-renewed), redirects HTTP → HTTPS, and routes `cms.<domain>` → `localhost:8080` and `users.<domain>` → `localhost:8081`; with no domain configured, Caddy serves a self-signed internal certificate so HTTPS is still enforced over the public IP. Both APIs listen on plain HTTP on private ports behind the proxy (documented pattern; Basic Auth never travels unencrypted).
- **Cheapest-possible footprint**: no ALB — the earlier design's ~$16–17/mo load balancer existed only for TLS termination and is replaced by in-instance Caddy. Measured app footprint is ~240 MB idle / ~285 MB peak for both APIs, so 1 GiB suffices. **~$1.20/mo today** (the `t4g.small` free trial runs through 2026-12-31) and **~$8.70/mo after** (documented downgrade to `t4g.micro` with optional .NET GC heap tuning).
- **Runtime binding**: `ASPNETCORE_URLS=http://0.0.0.0:8080` (CMS API) and `:8081` (Users API) — the loopback-only Production default is the #1 silent deployment failure — documented in the README and wired in the user-data.
- **Secrets**: the three user passwords and the connection strings live in SSM Parameter Store (standard tier, free) as `SecureString`; the instance role reads them at boot and writes the systemd `EnvironmentFile`. **Fresh random passwords are generated at deploy time** — the committed local-development defaults are never used outside local dev.
- **Console bootstrap script**: `scripts/bootstrap-aws.sh` is designed to be pasted into **AWS CloudShell** — one copy-paste clones the repository, installs Terraform on demand, generates fresh passwords, creates the whole environment (`terraform apply`), and deploys the latest CI-published artifacts (S3 → SSM Run Command, no build required in CloudShell). Region is a single variable at the top with **`eu-west-3` (Paris) as default**; an optional `--build` flag installs the .NET SDK and builds locally when no CI artifacts exist yet.
- **Deploy pipeline**: a new CI job on push to `main` (after the existing `build-and-test` and `end-to-end` jobs pass) publishes both APIs, uploads the artifacts to a versioned S3 bucket, and applies them via SSM Run Command (no SSH port opened); a post-deploy verification curls both `/health` endpoints and re-runs the smoke flow against the live deployment. The same S3 artifacts feed the console bootstrap script's first deploy.
- **Docs**: README Deployment section rewritten with the AWS topology, cost table, and step-by-step deploy walkthrough (including the one-paste CloudShell path); `docs/configuration.md` gains the `ASPNETCORE_URLS` binding requirement and the SSM secret channel.
- No application code changes. No **BREAKING** changes.

## Capabilities

### New Capabilities

- `aws-deployment`: the AWS footprint and deploy pipeline — one node hosting both APIs over one shared SQLite store, TLS termination via Caddy on the instance, secrets via SSM, a console-copy-pastable bootstrap script, and a CI job that deploys `main` and verifies it.

### Modified Capabilities

- `ci-quality-gates`: the CI workflow gains a deployment requirement — a push to `main` that passes the existing gates publishes both APIs to the S3 artifact bucket, deploys them to AWS, and verifies the live deployment — extending the workflow's current build/test/coverage/spec scope.

## Impact

- **New files**: `infra/aws/` (Terraform modules, user-data/systemd templates, Caddyfile template, SSM parameter definitions), `scripts/bootstrap-aws.sh`, a deploy script (`scripts/deploy-aws.sh`), `.github/workflows/deploy.yml` (or a deploy job in `ci.yml`).
- **Edited files**: `.github/workflows/ci.yml`, `README.md`, `docs/configuration.md`.
- **No changes** to `src/` application code, tests, or the .NET dependency graph.
- **AWS resources** (permanent, minimum): EC2 `t4g.small` (free trial through 2026-12-31, then `t4g.micro` downgrade), EBS 8GB gp3, Elastic IP, in-instance Caddy, Route 53 records (only when a domain is configured), SSM parameters, S3 artifact bucket, IAM roles — estimated **~$1.20/mo today with a Route 53 zone, ~$0.65/mo domainless** (EC2 trial + EBS; SSM/Let's Encrypt/Elastic IP free) and **~$8.70/mo after** the trial (EC2 `t4g.micro` + EBS + optional Route 53 zone).
- **Dependencies / sequencing**: independent of `containerize-apis` (the EC2 path publishes executables directly); if that change lands first, a Fargate variant of this footprint becomes the upgrade path but is **not** part of this change.
