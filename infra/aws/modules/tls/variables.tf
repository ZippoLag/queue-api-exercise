variable "env_name" {
  description = "Environment/stack name for tags."
  type        = string
}

variable "domain" {
  description = "Public domain; empty renders the self-signed internal Caddyfile."
  type        = string
  default     = ""
}

variable "route53_zone_id" {
  description = "Route 53 hosted zone id; records are only created when both this and `domain` are set."
  type        = string
  default     = ""
}

variable "public_ip" {
  description = "Elastic IP of the node (used by the domainless Caddyfile)."
  type        = string
}
