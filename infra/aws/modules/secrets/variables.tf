variable "env_name" {
  description = "Environment/stack name (parameter path segment)."
  type        = string
}

variable "cms_password" {
  description = "Password of the reserved cms-webhook user (generated at environment creation)."
  type        = string
  sensitive   = true
}

variable "admin_password" {
  description = "Password of the reserved administrator user (generated at environment creation)."
  type        = string
  sensitive   = true
}

variable "regular_password" {
  description = "Password of the reserved regular-user user (generated at environment creation)."
  type        = string
  sensitive   = true
}

variable "auth_db_connection" {
  description = "Connection string for the shared credential store."
  type        = string
  sensitive   = true
}

variable "cms_db_connection" {
  description = "Connection string for the shared CMS event store."
  type        = string
  sensitive   = true
}
