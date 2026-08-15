# infra/aws — Terraform footprint

The lowest-cost AWS deployment for the two APIs: **one `t4g.small` node** (free-trial
eligible through 2026-12-31) hosting both APIs over one shared SQLite store, TLS
terminated on the instance by **Caddy**, secrets in **SSM Parameter Store**, and a
versioned **S3 artifact bucket** feeding both the CI deploy and the console bootstrap
script.

## Layout

```
infra/aws/
├── main.tf        # root wiring
├── variables.tf   # region (default eu-west-3), env, domain, instance, secrets
├── outputs.tf     # URLs, instance id, artifact bucket, SSM parameter names
├── backend.tf     # local state default; documented S3/DynamoDB remote switch
└── modules/
    ├── network/   # VPC, public subnet, SG (80/443/8443 only, no SSH), Elastic IP
    ├── tls/       # Caddyfile template + optional Route 53 records
    ├── compute/   # EC2 + EBS store volume + user-data (runtime, Caddy, env files)
    ├── secrets/   # SSM SecureString parameters
    └── iam/       # instance role, GitHub OIDC deploy role, S3 artifact bucket
```

## Driving it

Normally you do **not** run `terraform` by hand — paste `scripts/bootstrap-aws.sh`
into AWS CloudShell (it installs Terraform, generates the passwords, applies, and
performs the first deploy). For manual use:

```bash
cd infra/aws
terraform init
terraform apply -var="region=eu-west-3" -var="cms_password=…" \
  -var="admin_password=…" -var="regular_password=…"
```

`terraform plan` runs in CI on every PR (`tf-validate` job) so the footprint stays
reviewable; local state is fine for a single maintainer — see `backend.tf` for the
shared-state switch.
