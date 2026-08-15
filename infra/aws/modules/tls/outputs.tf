output "caddyfile" {
  description = "Rendered Caddyfile, written to /etc/caddy/Caddyfile by user-data."
  value = templatefile("${path.module}/templates/Caddyfile.tftpl", {
    domain    = var.domain
    public_ip = var.public_ip
  })
}
