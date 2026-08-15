terraform {
  # Local state by default; see backend.tf for the remote-state switch.
  backend "local" {}
}

provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Project     = "queue-api-exercise"
      Environment = var.env_name
      ManagedBy   = "terraform"
    }
  }
}

# Single-AZ VPC, one public subnet, an SG that exposes only the proxy ports, and the
# Elastic IP the APIs are reached through.
module "network" {
  source   = "./modules/network"
  env_name = var.env_name
  domain   = var.domain
}

# Caddy TLS termination: renders the Caddyfile (Let's Encrypt when a domain is set,
# self-signed `tls internal` when it is not) and, when a hosted zone is provided,
# the cms./users. Route 53 A records.
module "tls" {
  source          = "./modules/tls"
  env_name        = var.env_name
  domain          = var.domain
  route53_zone_id = var.route53_zone_id
  public_ip       = module.network.eip_public_ip
}

# The single node: t4g.small EC2 instance, 8GB gp3 EBS store volume, and user-data
# that installs the .NET 9 runtime + Caddy, renders the systemd EnvironmentFiles from
# SSM, and mounts the store volume.
module "compute" {
  source                = "./modules/compute"
  region                = var.region
  env_name              = var.env_name
  instance_type         = var.instance_type
  instance_arch         = var.instance_arch
  ebs_size_gb           = var.ebs_size_gb
  subnet_id             = module.network.subnet_id
  security_group_ids    = [module.network.security_group_id]
  eip_allocation_id     = module.network.eip_allocation_id
  domain                = var.domain
  caddyfile             = module.tls.caddyfile
  instance_profile_name = module.iam.instance_profile_name
}

# SSM Parameter Store SecureString parameters: the three generated passwords and the
# two connection strings, injected as environment variables at boot (see compute
# user-data) and used to seed the credential store at the first deploy.
module "secrets" {
  source             = "./modules/secrets"
  env_name           = var.env_name
  cms_password       = var.cms_password
  admin_password     = var.admin_password
  regular_password   = var.regular_password
  auth_db_connection = var.auth_db_connection
  cms_db_connection  = var.cms_db_connection
}

# IAM: the instance role (SSM managed-instance core, ssm:GetParameters, S3 read on
# the artifact bucket), the GitHub OIDC deploy role, and the versioned S3 artifact
# bucket that both the CI deploy job and the console bootstrap script use.
module "iam" {
  source      = "./modules/iam"
  env_name    = var.env_name
  github_org  = var.github_org
  github_repo = var.github_repo
}
