## 1. Terraform footprint

- [x] 1.1 Scaffold `infra/aws/` — provider config (region as a variable, default `eu-west-3`), S3 remote state backend (DynamoDB lock), root module wiring
- [x] 1.2 Network module — single-AZ VPC, one public subnet, security group exposing only 80/443 (no public SSH), Elastic IP
- [x] 1.3 TLS module — Caddy install + Caddyfile template (routes `cms.<domain>` → 8080 and `users.<domain>` → 8081, HTTP→HTTPS redirect, Let's Encrypt when `DOMAIN` set, `tls internal` self-signed when not), optional Route 53 records
- [x] 1.4 Compute module — `t4g.small` EC2 with terminate-protection (downgrade to `t4g.micro` supported via variable), 8GB gp3 EBS mounted at `/var/lib/queue-api`, user-data that installs the .NET 9 runtime + Caddy and writes the systemd `EnvironmentFile`s from SSM (credential seeding happens at the first deploy via the published AuthDbInit — the node has the runtime only)
- [x] 1.5 Systemd unit templates for `cms-api` (binds `0.0.0.0:8080`, starts first) and `users-api` (binds `0.0.0.0:8081`) using `EnvironmentFile`
- [x] 1.6 Secrets + IAM — SSM Parameter Store `SecureString` parameters (three passwords, two connection strings), instance role (`ssm:GetParameters`, SSM managed-instance core for Run Command, EBS/S3 perms), GitHub OIDC role for the deploy job, versioned S3 artifact bucket
- [x] 1.7 CI `tf-validate` job — `terraform fmt -check` + `terraform validate` on PRs so the footprint stays reviewable

## 2. Deploy pipeline

- [x] 2.1 Write `scripts/deploy-aws.sh` — publish both APIs + AuthDbInit (Release), upload artifacts to the S3 bucket, run SSM Run Command to download + atomically swap `/opt/queue-api/{cms,users,auth-db-init}` (old kept as `.previous`), seed the store idempotently, restart `cms-api` then `users-api`, tail logs on failure
- [x] 2.2 Wire the CI deploy job — runs on push to `main` only, `needs: [build-and-test, end-to-end]`, uses the GitHub OIDC role (no stored keys), invokes `scripts/deploy-aws.sh`
- [x] 2.3 Live verification step in the pipeline — poll both `/health` endpoints, then run the smoke flow (ingest → regular-user listing → `cms-webhook` 403 → admin disable/enable) against the live HTTPS endpoints; fail the job on any check failure
- [x] 2.4 Rollback path — `scripts/deploy-aws.sh --rollback` restores the `.previous` artifacts and restarts; documented in the README Deployment section

## 3. Console bootstrap script

- [x] 3.1 Write `scripts/bootstrap-aws.sh` — top-of-script variables `REGION` (default `eu-west-3`), `ENV_NAME`, `DOMAIN`, `REPO_URL`/`BRANCH`; installs Terraform when missing; clones the repo; generates fresh passwords; `terraform apply` with the chosen region
- [x] 3.2 Artifact fetch + first deploy — download the latest CI-published artifacts from the S3 bucket and ship them via SSM Run Command (reusing `scripts/deploy-aws.sh`, task 2.1)
- [x] 3.3 `--build` fallback — when the artifact bucket is empty, install the .NET SDK in CloudShell and build/publish both APIs locally before deploying
- [x] 3.4 Idempotency + report — verify a re-run is a safe no-op on infrastructure; print the URLs, region, and how to read the generated passwords from SSM

## 4. Documentation

- [x] 4.1 Rewrite the README Deployment section — topology diagram (single node, Caddy TLS, shared store), cost table (~$1.20/mo during the t4g.small free trial, ~$8.70/mo after with the `t4g.micro` downgrade), and the step-by-step from pasting `scripts/bootstrap-aws.sh` in CloudShell to a smoke-verified deploy
- [x] 4.2 Update `docs/configuration.md` — `ASPNETCORE_URLS` binding requirement, SSM Parameter Store as the Staging/Production secret channel, the "never the committed defaults" rule, and the `eu-west-3` region default
- [x] 4.3 Document the manual operations — password rotation procedure, EBS snapshot cadence, instance stop/start for cost savings, and the post-trial instance downgrade (stop → modify → start) with the optional GC heap tuning
