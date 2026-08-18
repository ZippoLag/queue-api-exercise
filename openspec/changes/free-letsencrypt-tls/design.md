# Design: Free Let's Encrypt TLS via a Free Wildcard DNS Hostname

## Context

See proposal.md — Why. The domainless AWS deployment (`infra/aws`) currently terminates TLS with a self-signed internal Caddy certificate, so every curl and browser visit needs `-k` or a CA import. The Caddyfile template (`infra/aws/modules/tls/templates/Caddyfile.tftpl`) already renders the automatic-Let's-Encrypt variant whenever `domain != ""` — the Route 53 A records (`infra/aws/modules/tls/main.tf`) are created only when *both* `domain != ""` and `route53_zone_id != ""`, so a public domain does not require an owned hosted zone. The security group (`infra/aws/modules/network/main.tf`) keys on `domain`: `[80, 443]` when set, `[80, 443, 8443]` when not. The Caddyfile is written to the node by instance user-data at boot (`infra/aws/modules/compute/templates/user-data.sh.tftpl`) and the service runs with `admin off`, so config changes are applied with `systemctl restart caddy` (never `caddy reload`).

`scripts/deploy-aws.sh` already selects URLs by `DOMAIN` (`api_urls()`: hostnames without `-k`, bare-IP with `CURL_EXTRA=(-k)`), and `.github/workflows/ci.yml` already passes `DOMAIN: ${{ vars.AWS_DOMAIN || '' }}` into the deploy. The only stale facts are prose: `infra/aws/variables.tf` claims `domain` "requires route53_zone_id", `terraform.tfvars.example` presents a domain as mutually exclusive with zero-DNS-cost, and `docs/deployment-aws.md`'s cost table charges $0.50 for a hosted zone "only when `DOMAIN` is set".

## Goals / Non-Goals

**Goals:**
- Document and enable the free-DNS + Let's Encrypt option so the domainless deployment can serve trusted certificates at zero cost and without a Route 53 zone.
- Switch the live `demo` environment off the self-signed certificate: URLs become `https://cms.13-39-187-128.sslip.io` / `https://users.13-39-187-128.sslip.io`, and deploy-time live verification targets the new hostnames without `-k`.
- Align every script/CI/config surface (`bootstrap-aws.sh` `DOMAIN`, GitHub `AWS_DOMAIN` variable) on the same hostname, and correct the stale "requires route53_zone_id" prose.

**Non-Goals:**
- No Terraform resource changes, no Caddyfile template changes, no domain purchase, no Route 53 zone.
- The bare-IP self-signed variant stays fully supported and documented (it remains the fallback and the option when DNS is not wanted).
- No API, application, or artifact changes — the two APIs and their endpoints are untouched.

## Decisions

### D1 — The free wildcard hostname IS the `domain` value; no IaC change

The Caddyfile template already renders `https://cms.${domain}` / `https://users.${domain}` with automatic Let's Encrypt (HTTP-01 on :80) whenever `domain != ""`, independent of `route53_zone_id`. Setting `DOMAIN="13-39-187-128.sslip.io"` (with `ROUTE53_ZONE_ID` left empty) therefore produces the trusted-certificate Caddyfile with no Terraform edit: no records are created, and sslip.io's own wildcard DNS resolves `cms.13-39-187-128.sslip.io` and `users.13-39-187-128.sslip.io` to the node's Elastic IP.

- **Why this over alternatives:** adding a new Terraform variable (e.g. `free_domain`) or a template branch would duplicate an existing, already-correct mechanism; the template's `domain != ""` branch is precisely the LE path. The demo node's Elastic IP is fixed, so the derived hostname is stable.

### D2 — sslip.io as the free-DNS provider (dash-form of the Elastic IP)

The hostname is the Elastic IP with dots→dashes (`13.39.187.128` → `13-39-187-128.sslip.io`); subdomains `cms.` and `users.` are derived automatically. This matches the existing `bootstrap-aws.sh` report which already prints `https://cms.$DOMAIN` / `https://users.$DOMAIN` when `DOMAIN` is set.

- **Why sslip.io over nip.io:** functionally equivalent wildcard resolution; sslip.io is chosen as the single documented provider to keep one fact, one home. The naming rule (dash-form of the Elastic IP) is the invariant that survives an IP change.

### D3 — The live demo switch is a manual Caddyfile edit + restart (with instance re-creation as the reproducible alternative)

The proposal's Impact section names two application paths; the design recommends the manual one for the existing node:

1. **Manual edit (fast path, no downtime):** edit `/etc/caddy/Caddyfile` on the node to the Let's Encrypt variant (or drop in the `caddyfile` output of a `terraform plan`-rendered template), then `systemctl restart caddy` — the documented Caddy-config-change procedure (`admin off` makes reload impossible). No state is touched; the EBS stores are unaffected.
2. **Instance re-creation (reproducible path):** run `terraform apply` with `domain` set in tfvars — user-data changes force instance replacement, which rebuilds the node from the rendered user-data.

- **Why manual first:** the demo node is live; re-creation is heavy for a config-only change and would re-run the whole user-data bootstrap. The manual path is the one the deployment docs already prescribe for Caddy config changes.

### D4 — `AWS_DOMAIN` (GitHub variable) and `DOMAIN` (bootstrap) must both carry the hostname

CI already forwards `vars.AWS_DOMAIN` as `DOMAIN` into `scripts/deploy-aws.sh`, and the deploy script's `api_urls()` uses `DOMAIN` to build hostnames *without* `-k`. Setting the GitHub variable to `13-39-187-128.sslip.io` makes deploy-time live verification hit the new trusted hostnames automatically. `scripts/bootstrap-aws.sh`'s top-of-file `DOMAIN` gets the same value (and its comment gains the free-wildcard option), keeping console bootstraps consistent with CI.

- **Why not derive the hostname in-script from the EIP:** the deploy script would need the EIP and a provider convention anyway, and an explicit variable keeps the "what is this environment's public domain" fact in one visible place. The comment documents the derivation rule so an operator can compute it.

### D5 — Fix the stale `domain` prose so the option is discoverable

- `infra/aws/variables.tf` `domain` description: drop "requires route53_zone_id", state that a free wildcard hostname (sslip.io dash-form of the EIP) works without a hosted zone.
- `infra/aws/terraform.tfvars.example`: present the free-wildcard option alongside the real-domain option.
- `docs/deployment-aws.md`: domainless TLS section documents the option, the URL change, and that self-signed `-k` remains the fallback for the bare-IP variant; the cost table's "Route 53 hosted zone" row is clarified as "only when a real domain is configured" (a free wildcard hostname costs nothing).
- `scripts/bootstrap-aws.sh` `DOMAIN` comment and `scripts/deploy-aws.sh` header comment: note the free-wildcard option.

### D6 — Port 8443 closes automatically once `DOMAIN` is set — no manual SG change

The network module opens `[80, 443]` when `domain != ""`, so switching the demo environment removes the 8443 exposure as a side effect. This is correct: the Users API moves from `https://<ip>:8443` to `https://users.<host>` on 443. The design note is recorded so an operator observing the port close does not "fix" it.

## Risks / Trade-offs

- [Let's Encrypt HTTP-01 issuance fails or rate-limits] → Mitigation: the SG already opens :80, the hostname resolves publicly (verified during exploration), and Caddy retries issuance automatically; only two hostnames are issued. The self-signed fallback stays documented.
- [DNS/hostname changes if the Elastic IP ever changes] → Mitigation: the derivation rule (dash-form of the EIP) is documented, so a new IP yields a new hostname to update in `DOMAIN`/`AWS_DOMAIN`; the EIP is stable by design.
- [Instance re-creation path is heavy] → Mitigation: D3 recommends the manual edit + restart path for the live node; re-creation remains the reproducible fallback.
- [Someone re-opens port 8443 after the switch] → Mitigation: D6 records that the close is intentional; the SG template stays untouched.
- [Live `demo` switch is an ops action on a real environment] → Mitigation: the switch is a documented, reversible Caddyfile edit (rollback = restore the previous Caddyfile + restart); the stores are untouched.

## Migration Plan

1. Docs + prose first (D5): `docs/deployment-aws.md`, `infra/aws/variables.tf`, `infra/aws/terraform.tfvars.example`, `scripts/bootstrap-aws.sh` comment, `scripts/deploy-aws.sh` header comment.
2. Align values (D4): set `DOMAIN="13-39-187-128.sslip.io"` at the top of `scripts/bootstrap-aws.sh` for the demo environment; set the GitHub `AWS_DOMAIN` variable to the same value.
3. Switch the live node (D3): edit `/etc/caddy/Caddyfile` to the Let's Encrypt variant (as rendered by the template with `domain` set) and `systemctl restart caddy`.
4. Verify: `curl https://cms.13-39-187-128.sslip.io/health` and `curl https://users.13-39-187-128.sslip.io/health` succeed **without** `-k`; run `bash scripts/deploy-aws.sh` (or the CI deploy) and confirm live verification passes against the new hostnames.
5. Rollback: restore the previous self-signed Caddyfile on the node, `systemctl restart caddy`, and unset `AWS_DOMAIN`/`DOMAIN` — clients return to `-k`.

## Open Questions

None material: the provider (sslip.io), the hostname form (dash-form of the Elastic IP), and the application path (manual edit + restart) are all decided; the alternative path (instance re-creation) is recorded in D3. No decision would change the spec or the task breakdown.
