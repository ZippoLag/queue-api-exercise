## 1. Terraform footprint

- [ ] 1.1 Scaffold `infra/aws/` — provider config, S3 remote state backend (DynamoDB lock), root module wiring
- [ ] 1.2 Network module — single-AZ VPC, one public subnet, security groups (ALB→instance on 8080/8081 only; no public SSH)
- [ ] 1.3 ALB module — listener 80 (redirect→443) + 443 (ACM cert), two target groups health-checking `/health`, two Route 53 records (`cms.` / `users.`)
- [ ] 1.4 Compute module — `t4g.micro` EC2 with terminate-protection, 8GB gp3 EBS mounted at `/var/lib/queue-api`, user-data that installs the .NET 9 runtime, writes the systemd `EnvironmentFile`s from SSM, and seeds the credential store via `scripts/init-db.sh` with generated passwords
- [ ] 1.5 Systemd unit templates for `cms-api` (binds `0.0.0.0:8080`, starts first) and `users-api` (binds `0.0.0.0:8081`) using `EnvironmentFile`
- [ ] 1.6 Secrets + IAM — SSM Parameter Store `SecureString` parameters (three passwords, two connection strings), instance role (`ssm:GetParameters`, EBS/S3 perms), GitHub OIDC role for the deploy job
- [ ] 1.7 CI `tf-validate` job — `terraform fmt -check` + `terraform plan` on PRs so the footprint stays reviewable

## 2. Deploy pipeline

- [ ] 2.1 Write `scripts/deploy-aws.sh` — publish both APIs (Release), upload artifacts to the S3 bucket, run SSM Run Command to download + atomically swap `/opt/queue-api/{cms,users}` (old kept as `.previous`), restart `cms-api` then `users-api`, tail logs on failure
- [ ] 2.2 Wire the CI deploy job — runs on push to `main` only, `needs: [build-and-test, end-to-end]`, uses the GitHub OIDC role (no stored keys), invokes `scripts/deploy-aws.sh`
- [ ] 2.3 Live verification step in the pipeline — poll both `/health` endpoints, then run the smoke flow (ingest → regular-user listing → `cms-webhook` 403 → admin disable/enable) against the live HTTPS endpoints; fail the job on any check failure
- [ ] 2.4 Rollback path — document and exercise re-running the deploy with the previous artifact (`.previous` / prior S3 object version)

## 3. Documentation

- [ ] 3.1 Rewrite the README Deployment section — topology diagram (single node, shared store), cost table (~$20–26/mo, ALB not free-tier), and the step-by-step from `terraform apply` to first smoke-verified deploy
- [ ] 3.2 Update `docs/configuration.md` — `ASPNETCORE_URLS` binding requirement, SSM Parameter Store as the Staging/Production secret channel, and the "never the committed defaults" rule
- [ ] 3.3 Document the manual operations — password rotation procedure, EBS snapshot cadence, instance stop/start for cost savings
