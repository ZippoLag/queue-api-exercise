variable "env_name" {
  description = "Environment/stack name for resource names."
  type        = string
}

variable "github_org" {
  description = "GitHub organization/username; empty skips the CI deploy OIDC role."
  type        = string
  default     = ""
}

variable "github_repo" {
  description = "Optional repository to scope the OIDC role to; empty allows the whole organization."
  type        = string
  default     = ""
}

variable "github_org_id" {
  description = "Numeric ID of the GitHub organization/username. Since the 2025 OIDC subject-claim change, GitHub includes the owner ID in the sub claim (repo:owner@<owner-id>/repo@<repo-id>:...); the trust policy must match it. Find it via the repo API (owner.id)."
  type        = number
  default     = 0
}

variable "github_repo_id" {
  description = "Numeric ID of the GitHub repository, part of the 2025 OIDC subject-claim format. Find it via the repo API (id)."
  type        = number
  default     = 0
}

variable "bucket_suffix" {
  description = "Optional suffix for the artifact bucket name (bucket names are globally unique)."
  type        = string
  default     = ""
}

variable "github_thumbprint" {
  description = "TLS thumbprint of token.actions.githubusercontent.com, used when creating the GitHub OIDC provider."
  type        = string
  default     = "6938fd4d98bab03faadb97b34396831e3780aea1"
}
