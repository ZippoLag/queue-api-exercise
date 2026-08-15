output "instance_role_name" {
  value = aws_iam_role.instance.name
}

output "instance_profile_name" {
  value = aws_iam_instance_profile.instance.name
}

output "github_deploy_role_arn" {
  value = var.github_org != "" ? aws_iam_role.github_deploy[0].arn : null
}

output "artifact_bucket" {
  value = aws_s3_bucket.artifacts.bucket
}

output "artifact_bucket_arn" {
  value = aws_s3_bucket.artifacts.arn
}
