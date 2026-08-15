# Standard-tier SSM Parameter Store is free; SecureString encrypts at rest with the
# account default KMS key. The instance role (iam module) is the only reader.
resource "aws_ssm_parameter" "cms_password" {
  name        = "/queue-api/${var.env_name}/cms-password"
  type        = "SecureString"
  value       = var.cms_password
  description = "Password of the reserved cms-webhook user (generated at environment creation)."
}

resource "aws_ssm_parameter" "admin_password" {
  name        = "/queue-api/${var.env_name}/admin-password"
  type        = "SecureString"
  value       = var.admin_password
  description = "Password of the reserved administrator user (generated at environment creation)."
}

resource "aws_ssm_parameter" "regular_password" {
  name        = "/queue-api/${var.env_name}/regular-password"
  type        = "SecureString"
  value       = var.regular_password
  description = "Password of the reserved regular-user user (generated at environment creation)."
}

resource "aws_ssm_parameter" "auth_db" {
  name        = "/queue-api/${var.env_name}/auth-db"
  type        = "SecureString"
  value       = var.auth_db_connection
  description = "Connection string of the shared credential store."
}

resource "aws_ssm_parameter" "cms_db" {
  name        = "/queue-api/${var.env_name}/cms-db"
  type        = "SecureString"
  value       = var.cms_db_connection
  description = "Connection string of the shared CMS event store."
}
