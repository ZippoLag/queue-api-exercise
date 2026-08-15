# State backend.
#
# Local state is the default so that a single paste of scripts/bootstrap-aws.sh in
# AWS CloudShell "just works" without pre-creating any resources.
#
# To share state safely across a team, switch to the S3 backend below and run the
# bootstrap script with REMOTE_STATE=1 (it creates the state bucket and the DynamoDB
# lock table inline, then re-inits with the matching -backend-config values):
#
#   backend "s3" {
#     bucket         = "queue-api-terraform-state-<account-id>"  # created by REMOTE_STATE=1
#     key            = "queue-api-exercise/<env_name>/terraform.tfstate"
#     region         = "eu-west-3"
#     dynamodb_table = "queue-api-terraform-lock"                # created by REMOTE_STATE=1
#     encrypt        = true
#   }
