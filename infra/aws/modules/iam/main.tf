locals {
  artifact_bucket = "queue-api-artifacts-${var.env_name}${var.bucket_suffix}"
}

# --- Versioned S3 artifact bucket (CI publishes here; the node and bootstrap read) ---
resource "aws_s3_bucket" "artifacts" {
  bucket        = local.artifact_bucket
  force_destroy = true
}

resource "aws_s3_bucket_versioning" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_public_access_block" "artifacts" {
  bucket                  = aws_s3_bucket.artifacts.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# --- Instance role: SSM core (Run Command agent), parameter reads, artifact reads ----
data "aws_iam_policy_document" "instance_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "instance" {
  name               = "queue-api-node-${var.env_name}"
  assume_role_policy = data.aws_iam_policy_document.instance_assume.json
}

resource "aws_iam_role_policy_attachment" "instance_ssm_core" {
  role       = aws_iam_role.instance.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

data "aws_iam_policy_document" "instance" {
  statement {
    sid    = "ReadQueueApiSecrets"
    effect = "Allow"
    actions = [
      "ssm:GetParameters",
      "ssm:GetParameter",
    ]
    resources = ["arn:aws:ssm:*:*:parameter/queue-api/${var.env_name}/*"]
  }

  statement {
    sid       = "ReadArtifacts"
    effect    = "Allow"
    actions   = ["s3:GetObject", "s3:ListBucket"]
    resources = [aws_s3_bucket.artifacts.arn, "${aws_s3_bucket.artifacts.arn}/*"]
  }
}

resource "aws_iam_role_policy" "instance" {
  name   = "queue-api-node-${var.env_name}"
  role   = aws_iam_role.instance.name
  policy = data.aws_iam_policy_document.instance.json
}

resource "aws_iam_instance_profile" "instance" {
  name = "queue-api-node-${var.env_name}"
  role = aws_iam_role.instance.name
}

# --- GitHub OIDC deploy role (skipped when github_org is empty) ---------------------
resource "aws_iam_openid_connect_provider" "github" {
  count           = var.github_org != "" ? 1 : 0
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = [var.github_thumbprint]
}

data "aws_iam_policy_document" "github_assume" {
  count = var.github_org != "" ? 1 : 0

  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github[0].arn]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = [var.github_repo != "" ? "repo:${var.github_org}/${var.github_repo}:*" : "repo:${var.github_org}:*"]
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  count                = var.github_org != "" ? 1 : 0
  name                 = "queue-api-deploy-${var.env_name}"
  assume_role_policy   = data.aws_iam_policy_document.github_assume[0].json
  max_session_duration = 3600
}

data "aws_iam_policy_document" "github_deploy" {
  count = var.github_org != "" ? 1 : 0

  statement {
    sid       = "PublishArtifacts"
    effect    = "Allow"
    actions   = ["s3:PutObject", "s3:DeleteObject", "s3:ListBucket"]
    resources = [aws_s3_bucket.artifacts.arn, "${aws_s3_bucket.artifacts.arn}/*"]
  }

  # SendCommand is scoped to the deploy role; the wildcard resources cover the instance
  # (created by the compute module) and the AWS-RunShellScript document. The role itself
  # is only assumable by the repo's GitHub Actions workflows, which is the real boundary.
  statement {
    sid       = "SendAndInspectRunCommand"
    effect    = "Allow"
    actions   = ["ssm:SendCommand", "ssm:GetCommandInvocation", "ssm:ListCommandInvocations"]
    resources = ["*"]
  }

  statement {
    sid    = "ReadQueueApiSecretsForVerification"
    effect = "Allow"
    actions = [
      "ssm:GetParameters",
      "ssm:GetParameter",
    ]
    resources = ["arn:aws:ssm:*:*:parameter/queue-api/${var.env_name}/*"]
  }

  statement {
    sid       = "DescribeInstances"
    effect    = "Allow"
    actions   = ["ec2:DescribeInstances"]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "github_deploy" {
  count  = var.github_org != "" ? 1 : 0
  name   = "queue-api-deploy-${var.env_name}"
  role   = aws_iam_role.github_deploy[0].name
  policy = data.aws_iam_policy_document.github_deploy[0].json
}
