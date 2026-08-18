# AWS Deployment

The AWS deployment is the production footprint of the two APIs: both run as plain .NET publishes on a single EC2 node behind Caddy, provisioned entirely as infrastructure-as-code with Terraform. This document is the runbook — how to create, deploy to, operate, and tear down the environment.

For local development and debugging, see the README [Quickstart](../README.md#quickstart) and [Debugging](debugging.md). For the environment-variable matrix that also applies here, see [Configuration](configuration.md).

## Overview

**General concept — what "deploy to AWS" means here.** The APIs are ordinary ASP.NET Core processes. Deploying them to AWS means: put the built binaries on a server, run them as services, put a TLS-terminating proxy in front, and store the SQLite databases somewhere durable. No containers, no orchestrator, no load balancer.

**In this project** the whole footprint lives under [`infra/aws/`](https://github.com/ZippoLag/queue-api-exercise/tree/main/infra/aws) as Terraform, and a copy-pastable script ([`scripts/bootstrap-aws.sh`](https://github.com/ZippoLag/queue-api-exercise/blob/main/scripts/bootstrap-aws.sh)) creates it from the AWS console.

**Why this shape.**

- **One node.** Both APIs share the same SQLite store, and SQLite is a single-writer file — they must run on one host. Scaling out is explicitly out of scope until the store moves off SQLite.
- **No load balancer.** TLS terminates on the instance via Caddy, so the ~$16/mo ALB the APIs don't need is gone.
- **No containers.** The APIs are plain `dotnet publish` outputs run as systemd services — the simplest thing that works, and the easiest to reason about on a single node.

**Topology**

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

**Component notes**

- **One `t4g.small` node** hosts both APIs as systemd services.
- **TLS terminates on the instance** (Caddy): Let's Encrypt certificates when a domain is configured, or a self-signed internal certificate over the public IP when not. Plain HTTP redirects to HTTPS.
- **Secrets live in SSM Parameter Store** (`SecureString`): fresh passwords are generated at environment creation — the committed local-development defaults are never used.
- **The store engine stays SQLite.** The node never sets `Db__Provider` (the environment form of the `Db:Provider` configuration key — see [Configuration](configuration.md)), so every EF registration uses the `sqlite` default; no value is required in the deployment.
- **No SSH port**: deploys travel via SSM Run Command; the security group exposes 80/443 (plus 8443 in the domainless variant, which serves the Users API over the public IP).
- **CI deploys `main`**: a green push publishes both APIs to the S3 artifact bucket and ships them to the node (see [GitHub CI deploy](#github-ci-deploy)).

## Cost

The footprint is deliberately minimal, so the running cost is a few dollars a month.

| Resource | ~$/mo | Notes |
|---|---|---|
| EC2 `t4g.small` (2 vCPU / 2 GiB) | **$0** | free trial (750 h/mo) through 2026-12-31 |
| EC2 `t4g.micro` (1 GiB) — after the trial | ~$7.50 | documented downgrade (see [Post-trial downgrade](#post-trial-downgrade)) |
| EBS 8 GB gp3 (stores) | ~$0.65 | survives redeploys and stop/start |
| Elastic IP (attached) | $0 | |
| Caddy + Let's Encrypt | $0 | replaces the ALB |
| SSM Parameter Store (standard tier) | $0 | |
| S3 artifact bucket | ~$0 | tiny traffic |
| Route 53 hosted zone | $0.50 | only when `DOMAIN` is set |
| **Total today** | **~$1.20** | ~$0.65 without a domain |
| **Total after the trial** | **~$8.70** | after the `t4g.micro` downgrade |

## First-time setup: bootstrap

**General concept — what "bootstrap" means.** Creating a whole environment by hand (VPC, security group, instance, EBS, SSM parameters, S3 bucket, OIDC role, first deploy) is error-prone. "Bootstrapping" means one script does all of it, idempotently, so you can re-run it safely.

**Instructions.** Open **AWS CloudShell** (the console's built-in terminal) and paste [`scripts/bootstrap-aws.sh`](https://github.com/ZippoLag/queue-api-exercise/blob/main/scripts/bootstrap-aws.sh). The script:

1. clones this repository,
2. installs Terraform,
3. generates fresh passwords,
4. creates the whole environment,
5. performs the first deploy.

**Change the `REGION` variable first — it defaults to `eu-west-3` (Paris).**

```bash
# in AWS CloudShell — edit REGION/ENV_NAME/DOMAIN at the top of the script first
bash <(curl -fsSL https://raw.githubusercontent.com/ZippoLag/queue-api-exercise/main/scripts/bootstrap-aws.sh)
```

Re-running is safe: passwords are reused and Terraform is idempotent.

**Why it works this way.**

- **No access keys.** CloudShell runs as your logged-in console identity, so there is nothing to copy or rotate.
- **Ephemeral tooling.** Terraform and its provider plugins (the ~600 MB AWS provider) are installed under `/tmp`, so they don't consume CloudShell's 1 GB persistent `$HOME` — only the small clone and its Terraform state live there.
- **First deploy is local.** On a fresh account the artifact bucket is empty, so the script installs the .NET SDK and builds locally for the first deploy. Pass `--infra-only` to skip that and let the CI deploy job publish and ship the artifacts instead: set the secrets from the printed report, then push to `main`.

## Deploying

**Automatically.** A push to `main` that passes the `build-and-test` and `end-to-end` gates deploys and verifies itself (`.github/workflows/ci.yml` → `deploy` job; requires the GitHub secrets below).

**Manually** (reuses the same path):

```bash
REGION=eu-west-3 ENV_NAME=demo \
S3_BUCKET=<from terraform output> INSTANCE_ID=<from terraform output> \
DOMAIN="" bash scripts/deploy-aws.sh
```

**Rollback.** `scripts/deploy-aws.sh --rollback` restores the previous artifacts kept on the node as `*.previous` (or re-run the deploy job with a prior S3 object version).

**Tear down.** `scripts/teardown-aws.sh` destroys the whole Terraform-managed footprint. Run it from CloudShell against the same clone that holds the local state (it disables the node's termination protection first); pass `--all` to also delete the local clone and the Terraform install.

## GitHub CI deploy

**General concept — OIDC instead of keys.** Storing long-lived AWS access keys in GitHub is a security liability. With OIDC, GitHub proves to AWS that a workflow run belongs to your repository, and AWS hands out short-lived credentials for exactly that run — no keys are ever stored.

**In this project** the `deploy` job authenticates to AWS with the **OIDC role Terraform creates** for `GITHUB_ORG`, scoped to the single `GITHUB_REPO` (`queue-api-deploy-<env>`). It publishes both APIs and the `AuthDbInit` tool to the S3 artifact bucket, ships them to the node via SSM Run Command, and verifies the live deployment.

**Why the OIDC role needs the numeric IDs.** Since 2025 GitHub includes the owner and repo **numeric IDs** in the OIDC subject claim (`repo:owner@<id>/repo@<id>:ref:...`), so a trust policy matching only the legacy `repo:owner/repo:*` form no longer matches and the assume call fails with `Not authorized to perform sts:AssumeRoleWithWebIdentity`. The Terraform `iam` module therefore requires `github_org_id` / `github_repo_id` (from `gh api repos/<org>/<repo> --jq '{owner: .owner.id, repo: .id}'`) and matches both the ID-qualified and legacy patterns — see `infra/aws/modules/iam/main.tf`.

**GitHub secrets and vars.** Set these after the first bootstrap run, reading the values from the bootstrap report / `terraform output` (Settings → Secrets and variables → Actions):

| Name | Kind | Value |
|------|------|-------|
| `AWS_ACCOUNT_ID` | Secret | your AWS account id: `aws sts get-caller-identity --query Account --output text` |
| `AWS_S3_BUCKET` | Secret | artifact bucket: `terraform output -raw artifact_bucket` |
| `AWS_INSTANCE_ID` | Secret | node instance id: `terraform output -raw instance_id` |
| `AWS_ENV_NAME` | Variable | optional — defaults to `demo` (must match what bootstrap used) |
| `AWS_REGION` | Variable | optional — defaults to `eu-west-3` (must match what bootstrap used) |
| `AWS_DOMAIN` | Variable | optional — defaults to empty (self-signed URLs on the Elastic IP) |

**Expected failure before setup.** Until the environment exists and the secrets are set, a push to `main` will fail the `deploy` job (and the OIDC role does not exist yet either) — that is expected. Once the bootstrap has run and the secrets are in place, re-run the failed job from the Actions tab (or push again) and CI publishes the artifacts to S3 and deploys/verifies itself.

## New environment checklist

**General concept — one environment per `ENV_NAME`.** The whole stack is parameterized by `ENV_NAME` (`demo`, `staging`, `prod`, …): SSM parameters, artifact bucket, instance, and OIDC role all carry the environment name. Creating another environment is the same bootstrap with a different name; this checklist is the ordered end-to-end wiring for the first time — from an empty container to a deployed environment.

**Prerequisites — tooling + authentication** (once per machine/container; see [Tooling](tooling.md)):

```bash
bash scripts/install-ai-sdlc.sh                  # AWS CLI, uv, gh, terraform, …
aws login --remote --region eu-west-3            # 12 h session, renewable for 90 days
gh auth login -p https -s repo,workflow,read:org
```

**1. Create the environment.** Edit the top of [`scripts/bootstrap-aws.sh`](https://github.com/ZippoLag/queue-api-exercise/blob/main/scripts/bootstrap-aws.sh): set `ENV_NAME` (e.g. `staging`), `REGION`, optional `DOMAIN` + `ROUTE53_ZONE_ID`, and the `GITHUB_ORG_ID` / `GITHUB_REPO_ID` (unchanged unless the repo moves — see the numeric-ID note above). Run it in AWS CloudShell (or anywhere with AWS credentials):

```bash
bash scripts/bootstrap-aws.sh --infra-only
```

The script prints the five GitHub values the CI deploy job needs (account id, artifact bucket, instance id, env name, region).

**2. Wire GitHub.** Set the reported values as repo secrets/variables:

```bash
gh secret set AWS_ACCOUNT_ID   --repo ZippoLag/queue-api-exercise --body <account-id>
gh secret set AWS_S3_BUCKET    --repo ZippoLag/queue-api-exercise --body <bucket>
gh secret set AWS_INSTANCE_ID  --repo ZippoLag/queue-api-exercise --body <instance-id>
gh variable set AWS_ENV_NAME   --repo ZippoLag/queue-api-exercise --body <env-name>
gh variable set AWS_REGION     --repo ZippoLag/queue-api-exercise --body <region>
```

**3. Deploy.** Push to `main`; CI runs the gates and the deploy job ships the artifacts and verifies the live deployment (watch it in the Actions tab).

**4. Verify** against the printed URLs (`-k` because the domainless certs are self-signed):

```bash
curl -k https://<public-ip>/health
curl -k https://<public-ip>:8443/health
```

**Common first-time failures** — each was root-caused in this repo, so the fix is already in place if you see the symptom:

| Symptom | Cause | Where fixed |
|---|---|---|
| `Not authorized to perform sts:AssumeRoleWithWebIdentity` | GitHub's 2025 OIDC `sub` claim carries numeric owner/repo IDs; a legacy-format trust policy no longer matches | `infra/aws/modules/iam/main.tf` (requires `github_org_id` / `github_repo_id`) |
| `cannot execute binary file: Exec format error` | x64 apphost built on the CI runner, executed on the ARM64 node | `scripts/deploy-aws.sh` (publishes with the node's RID) |
| `Couldn't find a valid ICU package` | AL2023 lacks the ICU libraries the .NET runtime needs | `user-data.sh.tftpl` (`dnf install -y libicu`) |

**Multi-environment note.** `AWS_ENV_NAME` is a single repo-level variable, so the CI deploy job targets **one environment per secrets set**. Parallel environments (e.g. `demo` and `staging` both deployable) need environment-scoped secrets with a job-level `environment:` (or per-environment workflows) — pointing `AWS_ENV_NAME` at another environment just moves the single deploy target.

## Manual operations

Each operation below is a documented, repeatable procedure. The stores are throwaway by design — treat them as such, and know that the APIs fail fast at startup if the credential store is missing (see [Architecture](architecture.md)).

### Password rotation

**Why it's not just "re-run init".** The seed tool is a **no-op over an existing store** (existing users are left unchanged), and the node has the runtime only — no SDK, so the local `scripts/init-db.sh` does not apply there. Rotating therefore needs a fresh store: update the SSM parameter, delete the store on the node, then re-deploy — the published `AuthDbInit` re-seeds it from the new value and restarts the services.

```bash
# 1. rotate each password in SSM (repeat for each password you rotate); passwords must be
#    randomly generated GUIDs (initial requirements). Generate a dashed 8-4-4-4-12 RFC 4122
#    version-4 GUID from openssl rand output (never the bare hex form the init tool rejects),
#    then store it:
NEW_PW="$(raw=$(openssl rand -hex 16); nib="${raw:16:1}"; printf -v vh '%x' "$((8 + (16#$nib & 3)))"; printf '%s-%s-%s-%s-%s' "${raw:0:8}" "${raw:8:4}" "4${raw:13:3}" "${vh}${raw:17:3}" "${raw:20:12}")"
aws ssm put-parameter --name /queue-api/<env>/cms-password --type SecureString \
  --value "$NEW_PW" --overwrite --region eu-west-3

# 2. on the node, delete the credential store so it re-seeds from the new value
rm /var/lib/queue-api/queue-auth.db

# 3. re-deploy (the AuthDbInit tool re-seeds and restarts the services)
bash scripts/deploy-aws.sh
```

### Caddy config changes

Edit `/etc/caddy/Caddyfile` on the node and apply with `systemctl restart caddy` (TLS blips for a second; the API services are separate units and keep running).

A `reload` will **not** work: the Caddyfile sets `admin off`, which disables the admin API that `caddy reload` uses to push the new config.

### EBS snapshots

The stores are throwaway by design, but a manual snapshot cadence is documented best-practice:

```bash
aws ec2 create-snapshot --volume-id <store-volume> --region eu-west-3
```

### Stop/start for cost savings

`Stop instance` (not terminate) keeps the EBS stores; the Elastic IP stays attached and the services come back automatically (they are enabled at first deploy). Note the EIP is billed (~$0.005/hr) while the instance is stopped, so this only saves the instance-hours — meaningful once the `t4g.small` free trial ends. Terminate-protection is on.

### Post-trial downgrade

After 2026-12-31, stop the instance, change the type to `t4g.micro` (console: Instance settings → Change instance type), start it. The measured footprint (~285 MB peak for both APIs) fits 1 GiB with ~55% headroom; if you want a hard cap on the two processes, add `DOTNET_gcServer=0` and `DOTNET_GCHeapHardLimit` to the systemd `EnvironmentFile`s.

## See also

- [Configuration](configuration.md) — the full environment-variable matrix (also usable without AWS), TLS, and how SSM values are rendered into the API processes' environment at boot.
- [Architecture](architecture.md) — why the system is shaped this way (single node, outbox, shared stores).
- README [Quickstart](../README.md#quickstart) — local development without AWS.
- [`infra/aws/`](https://github.com/ZippoLag/queue-api-exercise/tree/main/infra/aws) — the Terraform source of truth for this footprint.
