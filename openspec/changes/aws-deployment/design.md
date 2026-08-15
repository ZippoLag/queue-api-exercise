## Context

See `proposal.md` — Why. Current state and hard constraints that shape the approach:

- Two APIs (CmsWebhook on :5264, Users on :5265) that **never call each other over HTTP** — they interoperate through two SQLite files (`queue-auth.db`, `queue-cms.db`). SQLite is a single-writer engine; the WAL + busy-timeout tuning assumes **one host, two processes**, not a network filesystem.
- The existing `scripts/smoke-e2e.sh` is a de-facto deployment playbook: it publishes both APIs, seeds a real credential store, sets `ASPNETCORE_ENVIRONMENT=Production` + `ConnectionStrings__*`, binds explicit URLs, and verifies the vertical. The AWS deployment must replicate exactly that contract.
- The CMS API's startup `EnsureCreated` provisions the shared schema; it must start first (or at least before the Users API reads the store).
- Basic Auth mandates TLS; the committed local-development passwords must never reach a deployed store.
- CI (`build-and-test` + `end-to-end`) is green and must gate any deploy.
- **Measured sizing** (local Release publish, Production env, real seeded store, smoke flow + 100-concurrent burst): both APIs together idle ~240 MB RSS, peak ~285 MB — a 1 GiB instance has ~55% headroom, so the footprint does not dictate a 2 GiB instance.

## Goals / Non-Goals

**Goals:**
- Lowest-cost AWS footprint that satisfies the spec: one node, both APIs, persistent stores, TLS, secrets via SSM, a console-copy-pastable bootstrap script, and a CI deploy-on-main with live verification.
- A junior developer can go from `main` to a live, verified deployment by pasting one script into AWS CloudShell.
- Infra as code: the footprint is reproducible and reviewable.

**Non-Goals:**
- Horizontal scaling, autoscaling, multi-AZ, multi-region — blocked by the shared SQLite store; recorded as the trigger for a future RDS migration (a separate change).
- Container-based deployment (Fargate/App Runner) — a documented upgrade path once `containerize-apis` lands, but the EC2 path is independent and cheaper today.
- Managed load balancing / ALB — the single node needs no load distribution; the earlier ALB existed only for TLS termination and is replaced by in-instance Caddy (~$16/mo saved).
- Database migration off SQLite; encryption-at-rest customization (EBS default encryption is used as-is); monitoring dashboards (CloudWatch logs only).

## Decisions

### D1: Compute — one `t4g.small` EC2 instance (free trial), both APIs as systemd services

Both published executables run on one instance via systemd units, stores on an attached EBS gp3 volume mounted at `/var/lib/queue-api`. Instance choice is a cost decision, not a sizing one — the measured combined footprint (~240 MB idle / ~285 MB peak) fits in 1 GiB with ~55% headroom:

- **`t4g.small` (2 vCPU / 2 GiB) is the default** while the free trial lasts (750 h/month through 2026-12-31): $0/mo and comfortable headroom.
- **`t4g.micro` (2 vCPU / 1 GiB) is the documented post-trial downgrade** (~$7.5/mo): stop → modify instance type → start; the EBS volume and stores survive. Optional guardrail when on 1 GiB: set `DOTNET_gcServer=0` (workstation GC) and/or `DOTNET_GCHeapHardLimit` in the systemd `EnvironmentFile`s to cap the two processes.

Alternatives considered: **Fargate** (~$9–10/mo compute + needs images) — the upgrade path once `containerize-apis` ships; **App Runner** — two services sharing a filesystem don't fit its model; **Elastic Beanstalk** — hidden abstractions make the single-node/shared-store story harder to reason about.

Systemd units mirror the smoke script contract:

```
[Service]
EnvironmentFile=/etc/queue-api/cms.env      # written from SSM at boot
ExecStart=/opt/queue-api/cms/CmsWebhook.Api
```

`cms-api` binds `0.0.0.0:8080`, `users-api` `0.0.0.0:8081` via `ASPNETCORE_URLS` — **the loopback-only Production default (`localhost:5000`) is the #1 silent failure** and is explicitly overridden everywhere (user-data, docs, smoke script parity).

### D2: Networking — instance-level TLS via Caddy, no ALB

One Elastic IP on the instance; security group allows inbound 80/443 from the internet and nothing else (**no public SSH** — deploys go through SSM, see D4). Caddy on the instance is the TLS terminator:

- With a domain (`DOMAIN` set): Caddy routes `cms.<domain>` → `localhost:8080` and `users.<domain>` → `localhost:8081`, terminates HTTPS with Let's Encrypt (HTTP-01 challenge; auto-issued and auto-renewed), and redirects HTTP → HTTPS. Route 53 records point the hostnames at the Elastic IP (hosted zone ~$0.50/mo).
- Without a domain (`DOMAIN=""`, the default): Caddy serves both routes with a **self-signed internal certificate** (`tls internal`) over the public IP, so HTTPS (and therefore Basic Auth safety) is enforced even with zero DNS/domain cost.

Rationale: the ALB in the earlier design existed purely for TLS termination — there is no load distribution on a single node. Removing it saves ~$16–17/mo. Alternatives: **ALB + ACM** — rejected on cost; **CloudFront** — adds cost/complexity with no benefit for an API with Basic Auth; **nginx** — equivalent, but Caddy's automatic Let's Encrypt issuance/renewal removes cert lifecycle management.

### D3: Secrets — SSM Parameter Store (standard tier), generated passwords, boot-time env files

Five parameters (`/queue-api/{cms,admin,regular}-password`, `/queue-api/auth-db`, `/queue-api/cms-db`) as `SecureString`; the instance role gets `ssm:GetParameters` on that path. **Password flow:** the bootstrap script generates fresh random passwords (`openssl rand -hex 16`) before `terraform apply` and passes them as variables, so the apply writes them into SSM; at first boot user-data renders the systemd `EnvironmentFile`s from those same SSM values and seeds the store via `scripts/init-db.sh`. **Re-deploys reuse the stored credentials untouched** — regenerating passwords per deploy would desynchronize them from the already-seeded store, since `init-db.sh` is idempotent and would not update an existing store. Rationale: standard-tier SSM is free, and this is exactly the "external secret store can be added later as another provider" seam the configuration spec anticipates. Alternatives: **Secrets Manager** (~$0.40/secret/mo) — better rotation, unnecessary at this scale; **committed/plaintext env files** — rejected outright.

### D4: Deploy pipeline — GitHub Actions → S3 artifact bucket → SSM Run Command

A `deploy` job (new, or appended to `ci.yml`) runs only on `main`, `needs: [build-and-test, end-to-end]`:

1. `dotnet publish` both APIs (Release).
2. `aws s3 sync` the publish outputs to a versioned S3 bucket (IAM OIDC role for GitHub, no stored keys).
3. SSM Run Command targets the instance, downloads the artifacts, atomically swaps `/opt/queue-api/{cms,users}` (old kept as `*.previous`), restarts `cms-api` then `users-api` (schema provisioned before reads), and tails logs.
4. Verification: poll both `/health`; then run the smoke flow (ingest → list → disable → enable → `cms-webhook` 403) against the live HTTPS endpoints.

The S3 bucket doubles as the artifact channel for the console bootstrap script (D6): the same objects CI syncs are what CloudShell downloads for a first/manual deploy — **no .NET SDK needed in CloudShell**. Rationale: no SSH port, IAM-only auth (OIDC + instance role), reuses the exact contract `smoke-e2e.sh` already proves. Alternatives: **CodeDeploy** — heavier to wire (deployment groups, IAM service roles, appspec); **SSH deploy action** — needs an open port 22 and stored keys; both rejected for cost/complexity.

### D5: IaC — Terraform under `infra/aws/`

Root module (provider, backend) + `network`, `tls` (Caddy config), `compute`, `secrets`, `iam` modules; remote state in an S3 backend (DynamoDB lock) so the team shares state safely — standard tier, near-zero cost. Rationale: Terraform has the largest EC2/SSM example base, which matters most for a junior maintainer. Alternatives: **CDK in C#** — in-language, but a heavier conceptual lift and thinner junior-facing docs; noted as viable if the maintainer prefers one language.

### D6: Console bootstrap script — `scripts/bootstrap-aws.sh` for AWS CloudShell

The one-copy-paste path from console to running service. CloudShell facts it designs around: git/aws-cli/jq preinstalled, **no Terraform and no .NET SDK** (both installed on demand), a persistent 1 GiB home directory, and it runs as the logged-in console identity — **no access keys ever**.

```
┌─ scripts/bootstrap-aws.sh ─────────────────────────────────┐
│ # ---- 1. EDIT HERE ----                                   │
│ REGION="eu-west-3"     # default Paris; change freely      │
│ ENV_NAME="demo"        # stack name (repeat for more envs) │
│ DOMAIN=""              # set → Route53 + Let's Encrypt     │
│ REPO_URL="…queue-api-exercise.git"   BRANCH="main"         │
│ # ---- 2. tooling ----                                     │
│ install terraform if missing   (releases.hashicorp.com)    │
│ git clone "$REPO_URL" → checkout "$BRANCH"                 │
│ # ---- 3. secrets ----                                     │
│ CMS_PW=$(openssl rand -hex 16)  (…admin, regular)          │
│ terraform -var="region=$REGION" -var="passwords=…"         │
│ # ---- 4. environment ----                                 │
│ terraform init && terraform apply -auto-approve            │
│ # ---- 5. artifacts + deploy ----                          │
│ aws s3 cp s3://<bucket>/latest/ → SSM Run Command          │
│   (reuses scripts/deploy-aws.sh); --build flag → dotnet    │
│   SDK install + local publish when the bucket is empty     │
│ # ---- 6. report ----                                      │
│ print URLs, region, and how to read passwords from SSM     │
└────────────────────────────────────────────────────────────┘
```

Idempotent by construction (terraform re-apply is a no-op; artifacts are re-shipped), so re-pasting is safe. Multi-environment: `ENV_NAME` + Terraform workspaces let the same script stand up `demo`/`staging`/`prod` stacks independently. Region flows into every `aws`/`terraform` invocation, so the `eu-west-3` default is honored even though CloudShell itself runs in a per-region sandbox.

## Risks / Trade-offs

- [Data loss if the instance terminates] → stores on EBS, instance terminate-protection, and a documented `ebs snapshot` cadence; the exercise's data is throwaway by design, so snapshots are a documented best-practice, not an automated job.
- [SQLite single-writer violated by accident (e.g. someone scales out)] → footprint is single-replica by construction; the spec declares it, the Terraform hardcodes `desired=1`, and the README explains the constraint.
- [Let's Encrypt issuance fails (HTTP-01 needs port 80; rate limits)] → port 80 is open by design (redirect target), Caddy retries/backoff is automatic, and the domainless self-signed fallback always works; documented in the README.
- [Self-signed certificate in the domainless demo] → browsers/curl need `-k` or an imported CA; acceptable for a demo, and the README says so. With a domain, Let's Encrypt is used instead.
- [1 GiB instance (post-trial downgrade) under sustained load] → measured headroom is ~55% at the demo's worst case; the documented `DOTNET_gcServer=0` / `DOTNET_GCHeapHardLimit` tuning caps the processes if ever needed.
- [Half-applied deploy] → atomic artifact swap + ordered restarts + post-deploy verification in the pipeline; failed verification leaves `.previous` intact for rollback.
- [Secret rotation is manual] → documented procedure (regenerate → re-seed → restart); Secrets Manager rotation listed as the future upgrade if passwords change often.
- [Terraform drift / unapplied changes] → `terraform plan` run in CI on PRs (a `tf-validate` job) so the footprint is always reviewable.

## Migration Plan

1. Paste `scripts/bootstrap-aws.sh` into CloudShell (region variable already defaults to `eu-west-3`) — it clones, creates the stack, seeds secrets, and performs the first deploy from CI-published artifacts (or `--build` when none exist yet).
2. If `DOMAIN` is set, point Route 53 records at the Elastic IP; Let's Encrypt issues the certificates automatically.
3. Subsequent deploys are automatic: a green push to `main` runs the CI deploy job; **rollback** = re-run the deploy job with the previous artifact (`.previous` on disk, or the prior S3 object version).

## Open Questions

- Exact hostnames (`cms.` / `users.`) — resolved at `terraform apply` when `DOMAIN` is set; the domainless default needs none.
- Terraform backend location (S3 bucket name) — decided at implementation time; local state is an acceptable first step for a single-maintainer exercise, and the bootstrap script can create the state bucket inline.
- Post-trial instance strategy (downgrade to `t4g.micro` vs. keep `t4g.small`) — a calendar decision, not a design one; both are supported by one tfvar.
