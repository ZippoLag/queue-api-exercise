## Context

See `proposal.md` — Why. Current state and hard constraints that shape the approach:

- Two APIs (CmsWebhook on :5264, Users on :5265) that **never call each other over HTTP** — they interoperate through two SQLite files (`queue-auth.db`, `queue-cms.db`). SQLite is a single-writer engine; the WAL + busy-timeout tuning assumes **one host, two processes**, not a network filesystem.
- The existing `scripts/smoke-e2e.sh` is a de-facto deployment playbook: it publishes both APIs, seeds a real credential store, sets `ASPNETCORE_ENVIRONMENT=Production` + `ConnectionStrings__*`, binds explicit URLs, and verifies the vertical. The AWS deployment must replicate exactly that contract.
- The CMS API's startup `EnsureCreated` provisions the shared schema; it must start first (or at least before the Users API reads the store).
- Basic Auth mandates TLS; the committed local-development passwords must never reach a deployed store.
- CI (`build-and-test` + `end-to-end`) is green and must gate any deploy.

## Goals / Non-Goals

**Goals:**
- Lowest-cost AWS footprint that satisfies the spec: one node, both APIs, persistent stores, TLS, secrets via SSM, and a CI deploy-on-main with live verification.
- A junior developer can follow the README to go from `main` to a live, verified deployment.
- Infra as code: the footprint is reproducible and reviewable.

**Non-Goals:**
- Horizontal scaling, autoscaling, multi-AZ, multi-region — blocked by the shared SQLite store; recorded as the trigger for a future RDS migration (a separate change).
- Container-based deployment (Fargate/App Runner) — a documented upgrade path once `containerize-apis` lands, but the EC2 path is independent and cheaper today.
- Database migration off SQLite; encryption-at-rest customization (EBS default encryption is used as-is); monitoring dashboards (CloudWatch logs only).

## Decisions

### D1: Compute — one `t4g.micro` EC2 instance, both APIs as systemd services

Both published executables run on one instance via systemd units, stores on an attached EBS gp3 volume mounted at `/var/lib/queue-api`. Rationale: cheapest option (free-tier eligible; ~$8/mo after), and the shared-files topology is the app's native shape. Alternatives considered: **Fargate** (~$9–10/mo compute + needs images) — the upgrade path once `containerize-apis` ships; **App Runner** — simplest for one service but two services sharing a filesystem don't fit its model; **Elastic Beanstalk** — hidden abstractions make the single-node/shared-store story harder to reason about.

Systemd units mirror the smoke script contract:

```
[Service]
EnvironmentFile=/etc/queue-api/cms.env      # written from SSM at boot
ExecStart=/opt/queue-api/cms/CmsWebhook.Api
```

`cms-api` binds `0.0.0.0:8080`, `users-api` `0.0.0.0:8081` via `ASPNETCORE_URLS` — **the loopback-only Production default (`localhost:5000`) is the #1 silent failure** and is explicitly overridden everywhere (user-data, docs, smoke script parity).

### D2: Networking — ALB + ACM TLS, two hostnames, HTTP→HTTPS redirect

One ALB in the public subnet; two listeners (80, 443); ACM certificate; two target groups (one per API, health-checking `/health`); two Route 53 records (`cms.` and `users.` subdomains) routing to the ALB. Security group allows only ALB→instance on 8080/8081; **no public SSH** (deploys go through SSM, see D4). Rationale: ALB is the cheapest proper TLS termination for Basic Auth, and per-API hostnames keep the APIs' root-path routes unambiguous. Alternatives: **path-based routing on one hostname** (`/cms/*`, `/users/*`) — rejected: would require URL-prefix stripping or proxy rewrites, coupling the two apps' contracts; **CloudFront** — adds cost/complexity with no benefit for an API with Basic Auth.

### D3: Secrets — SSM Parameter Store (standard tier), generated passwords, boot-time env files

Five parameters (`/queue-api/{cms,admin,regular}-password`, `/queue-api/auth-db`, `/queue-api/cms-db`) as `SecureString`; the instance role gets `ssm:GetParameters` on that path; user-data renders the systemd `EnvironmentFile`s at first boot; a deploy-time step **generates fresh random passwords** and seeds the store with them via `scripts/init-db.sh`. Rationale: standard-tier SSM is free, and this is exactly the "external secret store can be added later as another provider" seam the configuration spec anticipates. Alternatives: **Secrets Manager** (~$0.40/secret/mo) — better rotation, unnecessary at this scale; **committed/plaintext env files** — rejected outright.

### D4: Deploy pipeline — GitHub Actions → S3 artifact bucket → SSM Run Command

A `deploy` job (new, or appended to `ci.yml`) runs only on `main`, `needs: [build-and-test, end-to-end]`:

1. `dotnet publish` both APIs (Release).
2. `aws s3 sync` the publish outputs to a versioned S3 bucket (IAM OIDC role for GitHub, no stored keys).
3. SSM Run Command targets the instance, downloads the artifacts, atomically swaps `/opt/queue-api/{cms,users}` (old kept as `*.previous`), restarts `cms-api` then `users-api` (schema provisioned before reads), and tails logs.
4. Verification: poll both `/health`; then run the smoke flow (ingest → list → disable → enable → `cms-webhook` 403) against the live HTTPS endpoints.

Rationale: no SSH port, IAM-only auth (OIDC + instance role), reuses the exact contract `smoke-e2e.sh` already proves. Alternatives: **CodeDeploy** — more AWS-native but heavier to wire (deployment groups, IAM service roles, appspec); **SSH deploy action** — needs an open port 22 and stored keys; both rejected for cost/complexity.

### D5: IaC — Terraform under `infra/aws/`

Root module (provider, backend) + `network`, `alb`, `compute`, `secrets`, `iam` modules; remote state in an S3 backend (DynamoDB lock) so the team shares state safely — standard tier, near-zero cost. Rationale: Terraform has the largest EC2/ALB/SSM example base, which matters most for a junior maintainer. Alternatives: **CDK in C#** — in-language, but a heavier conceptual lift and thinner junior-facing docs; noted as viable if the maintainer prefers one language.

## Risks / Trade-offs

- [Data loss if the instance terminates] → stores on EBS, instance terminate-protection, and a documented `ebs snapshot` cadence; the exercise's data is throwaway by design, so snapshots are a documented best-practice, not an automated job.
- [SQLite single-writer violated by accident (e.g. someone scales out)] → footprint is single-replica by construction; the spec declares it, the Terraform hardcodes `desired=1`, and the README explains the constraint.
- [ALB cost (not free-tier, ~$16–20/mo)] → accepted as the unavoidable floor for TLS; the README cost table is explicit, and an HTTP-only dev/demo variant is documented as the only way to go cheaper (not recommended — Basic Auth).
- [Half-applied deploy] → atomic artifact swap + ordered restarts + post-deploy verification in the pipeline; failed verification leaves `.previous` intact for rollback.
- [Secret rotation is manual] → documented procedure (regenerate → re-seed → restart); Secrets Manager rotation listed as the future upgrade if passwords change often.
- [Terraform drift / unapplied changes] → `terraform plan` run in CI on PRs (a `tf-validate` job) so the footprint is always reviewable.

## Migration Plan

1. Apply Terraform (create VPC/ALB/EC2/SSM params; user-data seeds credentials at first boot with generated passwords).
2. Point Route 53 records at the ALB; ACM validates the cert.
3. First manual deploy through the pipeline's S3+SSM path; verify with the smoke flow.
4. Green `main` thereafter deploys automatically; **rollback** = re-run the deploy job with the previous artifact (`.previous` on disk, or the prior S3 object version).

## Open Questions

- Exact hostnames (`cms.` / `users.` subdomains) and AWS region/account — configuration values resolved at first `terraform apply`, no effect on specs/approach/tasks.
- Terraform backend location (S3 bucket name) — decided at implementation time; local state is an acceptable first step for a single-maintainer exercise.
