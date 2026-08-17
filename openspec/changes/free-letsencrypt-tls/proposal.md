## Why

The domainless AWS deployment serves TLS with a self-signed internal Caddy certificate (`tls internal`), so every Scalar-generated curl fails with `curl: (60) SSL certificate problem` unless the tester knows to add `-k`. Anyone trying the deployed endpoints "in their current state" hits this friction, and browsers warn on every visit. A free path exists: a free wildcard DNS hostname (`sslip.io`/`nip.io`) plus Caddy's automatic Let's Encrypt provisioning, at zero cost.

## What Changes

- **Document and enable the free-DNS + Let's Encrypt option** for the domainless deployment: the Caddyfile template already switches to automatic Let's Encrypt when `DOMAIN != ""` (no Route 53 zone required), and free wildcard hostnames like `13-39-187-128.sslip.io` resolve to the node's Elastic IP (verified). The change documents that `DOMAIN` may be a free wildcard hostname and applies it to the demo environment.
- **Switch the live `demo` environment off the self-signed certificate** — URLs change from `https://13.39.187.128` (CMS) / `https://13.39.187.128:8443` (Users) to `https://cms.13-39-187-128.sslip.io` / `https://users.13-39-187-128.sslip.io`, and Scalar-generated curl works without `-k`.
- **Align the CI deploy target** (`AWS_DOMAIN` variable) so deploy-time live verification uses the new hostnames without `-k`.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `aws-deployment`: the TLS requirement gains an explicit free-DNS + Let's Encrypt option for the domainless variant (self-signed remains the fallback), so live-deployment clients do not need to bypass certificate verification.

## Impact

- Live demo environment (`demo`): Caddyfile on the node switched from `tls internal` to Let's Encrypt (manual node edit + `systemctl restart caddy` per the documented Caddy-config-change procedure, or instance re-creation with updated `user_data`); public URLs change as above.
- `.github/workflows/ci.yml` / GitHub variable `AWS_DOMAIN` — deploy-time verification targets the new hostnames.
- `scripts/bootstrap-aws.sh` — `DOMAIN` variable comment documents the free-wildcard option (no Route 53 zone needed).
- `docs/deployment-aws.md` — domainless TLS section documents the option, the URL change, and that self-signed `-k` remains the fallback for the bare-IP variant.
- `openspec/specs/aws-deployment/spec.md` — delta spec for the modified capability.
