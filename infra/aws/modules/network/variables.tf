variable "env_name" {
  description = "Environment/stack name for tags."
  type        = string
}

variable "domain" {
  description = "Public domain; empty keeps the security group to the domainless ports (80/443/8443)."
  type        = string
  default     = ""
}

variable "vpc_cidr" {
  description = "CIDR block of the VPC."
  type        = string
  default     = "10.0.0.0/16"
}

variable "subnet_cidr" {
  description = "CIDR block of the single public subnet."
  type        = string
  default     = "10.0.1.0/24"
}
