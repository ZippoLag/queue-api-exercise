## 1. Prose and configuration alignment (design D5)

- [ ] 1.1 `infra/aws/variables.tf`: update the `domain` variable description — a free wildcard hostname (sslip.io dash-form of the Elastic IP) works without `route53_zone_id`; drop "requires route53_zone_id"
- [ ] 1.2 `infra/aws/terraform.tfvars.example`: present the free-wildcard option (`domain = "13-39-187-128.sslip.io"`, no zone) alongside the real-domain option
- [ ] 1.3 `scripts/bootstrap-aws.sh`: update the `DOMAIN` top-of-file comment to document the free-wildcard option and the dash-form derivation rule from the Elastic IP
- [ ] 1.4 `scripts/deploy-aws.sh`: update the header comment for `DOMAIN` to mention the free-wildcard option

## 2. Documentation

- [ ] 2.1 `docs/deployment-aws.md` domainless TLS section: document the free-DNS + Let's Encrypt option, the URL change to `https://cms.13-39-187-128.sslip.io` / `https://users.13-39-187-128.sslip.io`, and that self-signed `-k` remains the fallback for the bare-IP variant (spec: free wildcard DNS hostname receives trusted certificates)
- [ ] 2.2 `docs/deployment-aws.md` cost table: clarify the Route 53 hosted-zone row — $0.50 only when a real domain is configured; a free wildcard hostname adds no cost
- [ ] 2.3 `docs/deployment-aws.md` verification section: the `curl -k` examples for the domainless variant note the no-`-k` alternative when a free wildcard hostname is configured

## 3. Demo environment switch (design D3, D4)

- [ ] 3.1 Set `DOMAIN="13-39-187-128.sslip.io"` at the top of `scripts/bootstrap-aws.sh` for the demo environment (keep `ROUTE53_ZONE_ID=""`)
- [ ] 3.2 Set the GitHub `AWS_DOMAIN` variable to `13-39-187-128.sslip.io` so CI deploy-time verification targets the new hostnames without `-k`
- [ ] 3.3 Apply the Let's Encrypt Caddyfile to the live demo node (manual `/etc/caddy/Caddyfile` edit + `systemctl restart caddy`, or instance re-creation with updated user-data) per the documented Caddy-config-change procedure
- [ ] 3.4 Confirm port 8443 is no longer exposed after the switch (network module closes it when `domain != ""` — design D6) and that the Users API is reachable at `https://users.<host>` on 443

## 4. Verification

- [ ] 4.1 `curl https://cms.13-39-187-128.sslip.io/health` and `curl https://users.13-39-187-128.sslip.io/health` succeed without `-k`, and HTTP redirects to HTTPS
- [ ] 4.2 Run `bash scripts/deploy-aws.sh` (or trigger the CI deploy) and confirm live verification passes against the new hostnames without `-k`
- [ ] 4.3 `openspec validate --all` passes; no references to the old `https://13.39.187.128:8443` form remain in docs/scripts except as the documented self-signed fallback
