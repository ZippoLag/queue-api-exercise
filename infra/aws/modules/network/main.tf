data "aws_availability_zones" "available" {
  state = "available"
}

# Single-AZ VPC: one public subnet in the first AZ. The shared SQLite store pins the
# footprint to one node, so a multi-AZ network adds cost without resilience.
resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = { Name = "queue-api-${var.env_name}-vpc" }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "queue-api-${var.env_name}-igw" }
}

resource "aws_subnet" "public" {
  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.subnet_cidr
  availability_zone       = data.aws_availability_zones.available.names[0]
  map_public_ip_on_launch = true

  tags = { Name = "queue-api-${var.env_name}-public" }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  tags = { Name = "queue-api-${var.env_name}-public" }
}

resource "aws_route" "internet" {
  route_table_id         = aws_route_table.public.id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.this.id
}

resource "aws_route_table_association" "public" {
  subnet_id      = aws_subnet.public.id
  route_table_id = aws_route_table.public.id
}

# Only the TLS proxy ports are reachable from the internet. No SSH: deploys travel via
# SSM Run Command, and the instance role (see the iam module) scopes what the agent can
# do. Port 8443 serves the Users API in the domainless (self-signed) variant.
resource "aws_security_group" "this" {
  name        = "queue-api-${var.env_name}-public"
  description = "TLS proxy ports only (80/443, plus 8443 when domainless); no SSH."
  vpc_id      = aws_vpc.this.id

  dynamic "ingress" {
    for_each = var.domain != "" ? [80, 443] : [80, 443, 8443]
    content {
      from_port   = ingress.value
      to_port     = ingress.value
      protocol    = "tcp"
      cidr_blocks = ["0.0.0.0/0"]
    }
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "queue-api-${var.env_name}-public" }
}

# The Elastic IP is free while attached to a running instance and gives the APIs a
# stable address regardless of instance lifecycle.
resource "aws_eip" "this" {
  domain = "vpc"

  tags = { Name = "queue-api-${var.env_name}-eip" }
}
