output "vpc_id" {
  value = aws_vpc.this.id
}

output "subnet_id" {
  value = aws_subnet.public.id
}

output "security_group_id" {
  value = aws_security_group.this.id
}

output "eip_allocation_id" {
  value = aws_eip.this.id
}

output "eip_public_ip" {
  value = aws_eip.this.public_ip
}
