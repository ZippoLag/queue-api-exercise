## Why

The repo has no path from a green `main` to a running service. The Deployment section of the README stops at env-var listings — no service recommendation, no topology, no pipeline — and a first deploy hits five walls: no container image, Kestrel's default loopback-only bind in Production (`localhost:5000`) that makes ALB health checks fail, committed default passwords tempting their way into production, an undocumented single-node topology forced by the shared SQLite store, and no CI/CD step that deploys at all. A junior developer cannot deploy without inventing most of the stack. This change delivers the lowest-cost AWS footprint and the pipeline that puts `main` on it.

## What Changes

- **Infrastructure as code** (Terraform, under `infra/aws/`): single-AZ VPC with one public subnet, security groups (ALB → instance only; no public SSH), an ALB terminating HTTPS with an ACM certificate, Route 53 records, one `t4g.micro` EC2 instance with an attached EBS volume holding the stores, and SSM Parameter Store entries for secrets. Both APIs run on the **single node** (systemd services from `dotnet publish` output) — the shared SQLite store requires one host; multi-instance scaling is explicitly out of scope until the store is swapped.
- **TLS for Basic Auth**: the ALB terminates HTTPS (ACM), redirects HTTP → HTTPS, and health-checks the anonymous `/health` endpoint; both APIs run plain HTTP behind the ALB (documented pattern; Basic Auth never travels unencrypted).
- **Runtime binding**: `ASPNETCORE_URLS=http://0.0.0.0:8080` (CMS API) and `:8081` (Users API) — the loopback-only Production default is the #1 silent deployment failure — documented in the README and wired in the user-data.
- **Secrets**: the three user passwords and the connection strings live in SSM Parameter Store (standard tier, free) as `SecureString`; the instance role reads them at boot and writes the systemd `EnvironmentFile`. **Fresh random passwords are generated at deploy time** — the committed local-development defaults are never used outside local dev.
- **Deploy pipeline**: a new CI job on push to `main` (after the existing `build-and-test` and `end-to-end` jobs pass) publishes both APIs, uploads the artifacts to an S3 bucket, and applies them via SSM Run Command (no SSH port opened); a post-deploy verification curls both `/health` endpoints and re-runs the smoke flow against the live deployment.
- **Docs**: README Deployment section rewritten with the AWS topology, cost table, and step-by-step deploy walkthrough; `docs/configuration.md` gains the `ASPNETCORE_URLS` binding requirement and the SSM secret channel.
- No application code changes. No **BREAKING** changes.

## Capabilities

### New Capabilities

- `aws-deployment`: the AWS footprint and deploy pipeline — one node hosting both APIs over one shared SQLite store, TLS termination via ALB/ACM, secrets via SSM, and a CI job that deploys `main` and verifies it.

### Modified Capabilities

- `ci-quality-gates`: the CI workflow gains a deployment requirement — a push to `main` that passes the existing gates deploys the two APIs to AWS and verifies them — extending the workflow's current build/test/coverage/spec scope.

## Impact

- **New files**: `infra/aws/` (Terraform modules, user-data/systemd templates, SSM parameter definitions), a deploy script (`scripts/deploy-aws.sh` or equivalent), `.github/workflows/deploy.yml` (or a deploy job in `ci.yml`).
- **Edited files**: `.github/workflows/ci.yml`, `README.md`, `docs/configuration.md`.
- **No changes** to `src/` application code, tests, or the .NET dependency graph.
- **AWS resources** (permanent, minimum): EC2 `t4g.micro`, EBS 8GB gp3, ALB, ACM cert, Route 53 zone, SSM parameters, S3 artifact bucket, IAM roles — estimated **$20–26/mo**, or near $0 in the first-year free tier (ALB excluded from free tier).
- **Dependencies / sequencing**: independent of `containerize-apis` (the EC2 path publishes executables directly); if that change lands first, a Fargate variant of this footprint becomes the upgrade path but is **not** part of this change.
