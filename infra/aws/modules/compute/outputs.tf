output "instance_id" {
  description = "EC2 instance id (target of SSM Run Command deploys)."
  value       = aws_instance.this.id
}

output "store_volume_id" {
  description = "EBS volume holding the SQLite stores."
  value       = aws_ebs_volume.store.id
}
