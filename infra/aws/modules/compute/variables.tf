variable "region" {
  description = "AWS region (used by user-data's SSM/S3 calls)."
  type        = string
}

variable "env_name" {
  description = "Environment/stack name for tags and SSM parameter paths."
  type        = string
}

variable "instance_type" {
  description = "EC2 instance type (default t4g.small; downgrade to t4g.micro post-trial)."
  type        = string
  default     = "t4g.small"
}

variable "instance_arch" {
  description = "CPU architecture of instance_type: arm64 or x86_64."
  type        = string
  default     = "arm64"
}

variable "ebs_size_gb" {
  description = "Size in GiB of the EBS store volume mounted at /var/lib/queue-api."
  type        = number
  default     = 8
}

variable "subnet_id" {
  description = "Public subnet to launch into."
  type        = string
}

variable "security_group_ids" {
  description = "Security group ids attached to the instance (proxy ports only; no SSH)."
  type        = list(string)
}

variable "eip_allocation_id" {
  description = "Elastic IP allocation to associate with the instance."
  type        = string
}

variable "domain" {
  description = "Public domain (only used by the Caddyfile, which arrives rendered)."
  type        = string
  default     = ""
}

variable "caddyfile" {
  description = "Rendered Caddyfile content (from the tls module)."
  type        = string
}

variable "instance_profile_name" {
  description = "IAM instance profile name (from the iam module)."
  type        = string
}
