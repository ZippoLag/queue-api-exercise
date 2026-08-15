#!/usr/bin/env bash
#
# One copy-paste bootstrap for the AWS deployment, designed to run in AWS CloudShell
# (it also works on any machine with bash + aws cli). Paste this file's contents into
# the AWS console and it: installs Terraform on demand, clones the repository, reuses or
# generates the three user passwords, creates the whole environment (terraform apply),
# deploys the latest CI-published artifacts (S3 -> SSM Run Command), and prints the URLs
# and how to read the generated credentials. Safe to re-run: passwords are reused, and
# terraform apply is idempotent.
#
# Usage:
#   bash scripts/bootstrap-aws.sh [--build] [--remote-state]
#
#   --build         also build the artifacts locally (installs the .NET SDK in CloudShell)
#                   and perform the first deploy, even if the artifact bucket is empty
#   --remote-state  create the S3 state bucket + DynamoDB lock table and use them
#
# ── 1. EDIT HERE ────────────────────────────────────────────────────────────────
REGION="eu-west-3"            # AWS region — change freely before running (Paris default)
ENV_NAME="queue-api-exercise"               # environment/stack name (repeat with a new name for more environments)
DOMAIN=""                     # public domain (e.g. "example.com") + ROUTE53_ZONE_ID for real certs; empty = self-signed on the Elastic IP
ROUTE53_ZONE_ID=""            # hosted zone id, only used when DOMAIN is set
INSTANCE_TYPE="t4g.small"     # free trial through 2026-12-31; downgrade to t4g.micro after
BUCKET_SUFFIX=""              # optional suffix for the artifact bucket (globally unique names)
GITHUB_ORG="ZippoLag"         # for the CI deploy OIDC role (leave empty to skip)
REPO_URL="https://github.com/ZippoLag/queue-api-exercise.git"
BRANCH="main"
WORK_DIR="$HOME/queue-api-aws" # where the repo is cloned and terraform runs
# ─────────────────────────────────────────────────────────────────────────────────

set -euo pipefail

log()  { echo "[Information] $*"; }
warn() { echo "[Warning] $*" >&2; }
fail() { echo "[Error] $*" >&2; exit 1; }

BUILD_FLAG=0
REMOTE_STATE=0
for arg in "$@"; do
  case "$arg" in
    --build)        BUILD_FLAG=1 ;;
    --remote-state) REMOTE_STATE=1 ;;
    *) echo "[Error] Unknown argument: $arg" >&2; exit 1 ;;
  esac
done

command -v aws >/dev/null 2>&1 || fail "The AWS CLI is required (preinstalled in CloudShell)."
command -v git >/dev/null 2>&1 || fail "git is required (preinstalled in CloudShell)."

# ── 2. Tooling ───────────────────────────────────────────────────────────────────
if ! command -v terraform >/dev/null 2>&1; then
  log "Terraform not found — installing to $HOME/.local/bin"
  TERRAFORM_VERSION="1.9.8"
  mkdir -p "$HOME/.local/bin"
  curl -fsSL -o /tmp/terraform.zip \
    "https://releases.hashicorp.com/terraform/${TERRAFORM_VERSION}/terraform_${TERRAFORM_VERSION}_linux_amd64.zip"
  (cd /tmp && unzip -oq terraform.zip -d "$HOME/.local/bin")
  export PATH="$HOME/.local/bin:$PATH"
  command -v terraform >/dev/null 2>&1 || fail "Terraform install failed."
fi
log "terraform: $(terraform version | head -1)"

# ── 3. Repository ─────────────────────────────────────────────────────────────────
mkdir -p "$WORK_DIR"
if [ ! -d "$WORK_DIR/repo/.git" ]; then
  log "Cloning $REPO_URL ($BRANCH) into $WORK_DIR/repo"
  git clone --quiet --branch "$BRANCH" "$REPO_URL" "$WORK_DIR/repo"
else
  log "Repository already present — pulling latest $BRANCH"
  git -C "$WORK_DIR/repo" fetch --quiet origin
  git -C "$WORK_DIR/repo" checkout --quiet "$BRANCH"
  git -C "$WORK_DIR/repo" pull --quiet --ff-only
fi
cd "$WORK_DIR/repo/infra/aws"

# ── 4. Secrets (reuse on re-run; generate only once) ─────────────────────────────
ssm_get_or_generate() {
  local name="$1"
  if value="$(aws ssm get-parameter --region "$REGION" --name "$name" \
    --with-decryption --query Parameter.Value --output text 2>/dev/null)"; then
    printf '%s' "$value"
  else
    openssl rand -hex 16
  fi
}
CMS_PW="$(ssm_get_or_generate "/queue-api/$ENV_NAME/cms-password")"
ADMIN_PW="$(ssm_get_or_generate "/queue-api/$ENV_NAME/admin-password")"
REGULAR_PW="$(ssm_get_or_generate "/queue-api/$ENV_NAME/regular-password")"

# ── 5. Environment (terraform init + apply) ──────────────────────────────────────
if [ "$REMOTE_STATE" = "1" ]; then
  STATE_BUCKET="queue-api-terraform-state-${ENV_NAME}${BUCKET_SUFFIX}"
  if ! aws s3api head-bucket --bucket "$STATE_BUCKET" --region "$REGION" >/dev/null 2>&1; then
    log "Creating state bucket $STATE_BUCKET and DynamoDB lock table"
    aws s3api create-bucket --bucket "$STATE_BUCKET" --region "$REGION" \
      --create-bucket-configuration "LocationConstraint=$REGION" >/dev/null
    aws s3api put-bucket-versioning --bucket "$STATE_BUCKET" \
      --versioning-configuration "Status=Enabled" >/dev/null
    aws dynamodb create-table --table-name "queue-api-terraform-lock-${ENV_NAME}" \
      --attribute-definitions "AttributeName=LockID,AttributeType=S" \
      --key-schema "AttributeName=LockID,KeyType=HASH" \
      --billing-mode PAY_PER_REQUEST --region "$REGION" >/dev/null
  fi
  terraform init -reconfigure -backend=true \
    -backend-config="bucket=$STATE_BUCKET" \
    -backend-config="key=queue-api-exercise/${ENV_NAME}/terraform.tfstate" \
    -backend-config="region=$REGION" \
    -backend-config="dynamodb_table=queue-api-terraform-lock-${ENV_NAME}" \
    -backend-config="encrypt=true"
else
  terraform init -reconfigure -input=false
fi

TFVARS="$(mktemp /tmp/queue-api-tfvars.XXXXXX)"
chmod 600 "$TFVARS"
cat > "$TFVARS" <<EOF
region              = "$REGION"
env_name            = "$ENV_NAME"
domain              = "$DOMAIN"
route53_zone_id     = "$ROUTE53_ZONE_ID"
instance_type       = "$INSTANCE_TYPE"
bucket_suffix       = "$BUCKET_SUFFIX"
github_org          = "$GITHUB_ORG"
cms_password        = "$CMS_PW"
admin_password      = "$ADMIN_PW"
regular_password    = "$REGULAR_PW"
EOF

log "Applying the environment ($ENV_NAME in $REGION) — idempotent, safe to re-run"
terraform apply -auto-approve -var-file="$TFVARS"
rm -f "$TFVARS"

INSTANCE_ID="$(terraform output -raw instance_id)"
BUCKET="$(terraform output -raw artifact_bucket)"
log "Environment ready: instance=$INSTANCE_ID bucket=$BUCKET"

# ── 6. Artifacts + first deploy ───────────────────────────────────────────────────
if [ "$BUILD_FLAG" = "0" ] && aws s3 ls "s3://$BUCKET/latest/" --region "$REGION" | grep -q .; then
  log "CI-published artifacts found in S3 — deploying them (no build needed)"
  REGION="$REGION" ENV_NAME="$ENV_NAME" S3_BUCKET="$BUCKET" INSTANCE_ID="$INSTANCE_ID" \
    DOMAIN="$DOMAIN" bash "$WORK_DIR/repo/scripts/deploy-aws.sh" --skip-publish
else
  if [ "$BUILD_FLAG" = "0" ]; then
    warn "No CI artifacts in S3 yet — building locally (installs the .NET SDK in CloudShell)."
  fi
  log "Installing the .NET 9 SDK (needed only for the local build)"
  # CloudShell's persistent home is a fixed 1 GiB per region and cannot be expanded; the SDK
  # (~700 MB extracted) plus the workspace clone and Terraform do not fit there, which fills
  # $HOME and aborts the build. Install to the ephemeral disk instead — the SDK is only needed
  # for this one-time local build and the ephemeral space is reclaimed when the session ends.
  DOTNET_DIR="${TMPDIR:-/tmp}/queue-api-dotnet"
  rm -rf "$DOTNET_DIR"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 9.0 --install-dir "$DOTNET_DIR"
  export PATH="$DOTNET_DIR:$PATH"
  export DOTNET_ROOT="$DOTNET_DIR"
  (cd "$WORK_DIR/repo" && REGION="$REGION" ENV_NAME="$ENV_NAME" S3_BUCKET="$BUCKET" \
    INSTANCE_ID="$INSTANCE_ID" DOMAIN="$DOMAIN" bash scripts/deploy-aws.sh)
fi

# ── 7. Report ─────────────────────────────────────────────────────────────────────
log "Deployment complete."
echo
echo "============================================================"
echo "  Queue API — AWS deployment report"
echo "============================================================"
echo "  Region:   $REGION"
echo "  Stack:    $ENV_NAME"
echo "  Instance: $INSTANCE_ID"
echo
echo "  URLs:"
if [ -n "$DOMAIN" ]; then
  echo "    CMS Webhook API : https://cms.$DOMAIN"
  echo "    Users API       : https://users.$DOMAIN"
else
  IP="$(terraform output -raw public_ip)"
  echo "    CMS Webhook API : https://$IP            (self-signed → curl -k)"
  echo "    Users API       : https://$IP:8443       (self-signed → curl -k)"
fi
echo
echo "  Generated credentials (read from SSM):"
echo "    aws ssm get-parameter --name \"/queue-api/$ENV_NAME/cms-password\"     --with-decryption --region $REGION --query Parameter.Value --output text"
echo "    aws ssm get-parameter --name \"/queue-api/$ENV_NAME/admin-password\"   --with-decryption --region $REGION --query Parameter.Value --output text"
echo "    aws ssm get-parameter --name \"/queue-api/$ENV_NAME/regular-password\" --with-decryption --region $REGION --query Parameter.Value --output text"
echo
echo "  Smoke-verify:"
echo "    curl -k -u cms-webhook:\$CMS_PW -X POST -H 'Content-Type: application/json' \\"
echo "      -d '{\"type\":\"publish\",\"id\":\"hello-1\",\"payload\":{\"title\":\"hi\"},\"version\":1,\"timestamp\":\"2026-01-01T00:00:00Z\"}' \\"
echo "      <cms-url>/cms/events"
echo "============================================================"
