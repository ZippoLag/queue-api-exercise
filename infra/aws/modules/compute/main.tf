data "aws_ami" "al2023" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["al2023-ami-2023.*-${var.instance_arch}"]
  }

  filter {
    name   = "architecture"
    values = [var.instance_arch]
  }
}

data "aws_subnet" "selected" {
  id = var.subnet_id
}

# The single node hosting both APIs. disable_api_termination protects the EBS store
# from accidental console terminations; stopping the instance (not terminating) is the
# supported cost-saving action.
resource "aws_instance" "this" {
  ami                     = data.aws_ami.al2023.id
  instance_type           = var.instance_type
  subnet_id               = var.subnet_id
  vpc_security_group_ids  = var.security_group_ids
  iam_instance_profile    = var.instance_profile_name
  disable_api_termination = true
  user_data = templatefile("${path.module}/templates/user-data.sh.tftpl", {
    region        = var.region
    env_name      = var.env_name
    arch_download = var.instance_arch == "arm64" ? "arm64" : "amd64"
    volume_id     = aws_ebs_volume.store.id
    caddyfile     = var.caddyfile
    cms_unit      = templatefile("${path.module}/templates/cms-api.service.tftpl", {})
    users_unit    = templatefile("${path.module}/templates/users-api.service.tftpl", {})
  })

  root_block_device {
    volume_type = "gp3"
    volume_size = 8
  }

  tags = { Name = "queue-api-${var.env_name}-node" }
}

# The persistent store: both SQLite files live here and survive redeploys and instance
# stop/start. Terminating the instance is the only way to lose the data.
resource "aws_ebs_volume" "store" {
  availability_zone = data.aws_subnet.selected.availability_zone
  size              = var.ebs_size_gb
  type              = "gp3"

  tags = { Name = "queue-api-${var.env_name}-store" }
}

resource "aws_volume_attachment" "store" {
  device_name = "/dev/sdf"
  volume_id   = aws_ebs_volume.store.id
  instance_id = aws_instance.this.id
}

resource "aws_eip_association" "this" {
  instance_id   = aws_instance.this.id
  allocation_id = var.eip_allocation_id
}
