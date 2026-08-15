output "parameter_names" {
  description = "SSM parameter names, for documentation and scripts."
  value = {
    cms_webhook = "/queue-api/${var.env_name}/cms-password"
    admin       = "/queue-api/${var.env_name}/admin-password"
    regular     = "/queue-api/${var.env_name}/regular-password"
    auth_db     = "/queue-api/${var.env_name}/auth-db"
    cms_db      = "/queue-api/${var.env_name}/cms-db"
  }
}
