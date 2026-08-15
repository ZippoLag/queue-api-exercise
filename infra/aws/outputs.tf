output "region" {
  description = "AWS region the environment was deployed into."
  value       = var.region
}

output "environment" {
  description = "Environment/stack name."
  value       = var.env_name
}

output "instance_id" {
  description = "EC2 instance id (target for scripts/deploy-aws.sh)."
  value       = module.compute.instance_id
}

output "public_ip" {
  description = "Elastic IP of the node (no public SSH — deploys go through SSM Run Command)."
  value       = module.network.eip_public_ip
}

output "cms_url" {
  description = "Base URL of the CmsWebhook API."
  value       = var.domain != "" ? "https://cms.${var.domain}" : "https://${module.network.eip_public_ip}"
}

output "users_url" {
  description = "Base URL of the Users API."
  value       = var.domain != "" ? "https://users.${var.domain}" : "https://${module.network.eip_public_ip}:8443"
}

output "artifact_bucket" {
  description = "S3 bucket that CI publishes artifacts to and scripts/bootstrap-aws.sh reads from."
  value       = module.iam.artifact_bucket
}

output "password_parameters" {
  description = "SSM parameter names holding the generated credentials (SecureString; read with --with-decryption)."
  value = {
    cms_webhook = "/queue-api/${var.env_name}/cms-password"
    admin       = "/queue-api/${var.env_name}/admin-password"
    regular     = "/queue-api/${var.env_name}/regular-password"
  }
}
