# Route 53 A records for the two hostnames — only when a domain AND an existing hosted
# zone are provided. The zone itself is expected to exist (AWS charges ~$0.50/mo for it).
resource "aws_route53_record" "cms" {
  count   = var.domain != "" && var.route53_zone_id != "" ? 1 : 0
  zone_id = var.route53_zone_id
  name    = "cms.${var.domain}"
  type    = "A"
  ttl     = 300
  records = [var.public_ip]
}

resource "aws_route53_record" "users" {
  count   = var.domain != "" && var.route53_zone_id != "" ? 1 : 0
  zone_id = var.route53_zone_id
  name    = "users.${var.domain}"
  type    = "A"
  ttl     = 300
  records = [var.public_ip]
}
