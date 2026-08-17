#!/usr/bin/env bash
#
# Tear down an AWS environment previously created by scripts/bootstrap-aws.sh.
# Runs `terraform destroy` from the same working directory the bootstrap used, so it
# reads the same local state file and knows exactly which resources to remove.
#
# The node is created with termination protection (disable_api_termination = true, see
# infra/aws/modules/compute), so `terraform destroy` alone cannot terminate it — this
# script turns that protection off first.
#
# Usage:
#   bash scripts/teardown-aws.sh            # destroy the stack in the local state
#   bash scripts/teardown-aws.sh --all      # also delete the local clone + terraform
#
# Safe to re-run: an already-empty state is a no-op.

# ── 1. EDIT HERE ────────────────────────────────────────────────────────────────
REGION="eu-west-3"              # AWS region of the environment to destroy
ENV_NAME="queue-api-exercise"   # stack name to destroy (must match what bootstrap used)
GITHUB_ORG="ZippoLag"           # must match what bootstrap used (controls OIDC role presence)
GITHUB_REPO="queue-api-exercise" # repo the OIDC role was scoped to (cosmetic for destroy)
WORK_DIR="$HOME/queue-api-aws"  # where bootstrap-aws.sh cloned the repo (holds the state)
# ─────────────────────────────────────────────────────────────────────────────────

set -euo pipefail

log()  { echo "[Information] $*"; }
warn() { echo "[Warning] $*" >&2; }
fail() { echo "[Error] $*" >&2; exit 1; }

CLEANUP_ALL=0
for arg in "$@"; do
  case "$arg" in
    --all) CLEANUP_ALL=1 ;;
    *) fail "Unknown argument: $arg" ;;
  esac
done

command -v aws >/dev/null 2>&1 || fail "The AWS CLI is required (preinstalled in CloudShell)."

# Terraform and the AWS provider live on the ephemeral /tmp disk (see bootstrap-aws.sh);
# reinstall if a previous session reclaimed them, and keep the ~600 MB provider plugin out
# of the 1 GiB persistent $HOME.
TF_INSTALL_DIR="${TMPDIR:-/tmp}/queue-api-terraform"
export TF_DATA_DIR="${TMPDIR:-/tmp}/queue-api-tf-data"
mkdir -p "$TF_DATA_DIR"
if [ ! -x "$TF_INSTALL_DIR/terraform" ]; then
  log "Terraform not found — installing to $TF_INSTALL_DIR"
  TERRAFORM_VERSION="1.9.8"
  mkdir -p "$TF_INSTALL_DIR"
  curl -fsSL -o /tmp/terraform.zip \
    "https://releases.hashicorp.com/terraform/${TERRAFORM_VERSION}/terraform_${TERRAFORM_VERSION}_linux_amd64.zip"
  (cd /tmp && unzip -oq terraform.zip -d "$TF_INSTALL_DIR")
fi
export PATH="$TF_INSTALL_DIR:$PATH"
command -v terraform >/dev/null 2>&1 || fail "Terraform install failed."

TF_DIR="$WORK_DIR/repo/infra/aws"
[ -d "$TF_DIR" ] || fail "Terraform working dir not found at $TF_DIR — it must hold the local terraform.tfstate from bootstrap."
cd "$TF_DIR"

# ── 2. Init (no-op when already initialized; preserves the existing local backend) ──
terraform init -input=false

# ── 3. Drop termination protection so the instance can actually be terminated ─────
INSTANCE_ID="$(terraform output -raw instance_id 2>/dev/null || true)"
if [ -n "$INSTANCE_ID" ]; then
  log "Disabling termination protection on instance $INSTANCE_ID"
  aws ec2 modify-instance-attribute --instance-id "$INSTANCE_ID" \
    --no-disable-api-termination --region "$REGION" \
    || warn "Could not disable termination protection (continuing anyway)"
else
  log "No instance in state — nothing to unprotect"
fi

# ── 4. Destroy ──────────────────────────────────────────────────────────────────
# The password values are irrelevant to destroy (the state, not the values, decides
# what to delete); they are still required by the config, so read them from SSM and
# fall back to a placeholder if they were already removed.
ssm_read() {
  aws ssm get-parameter --name "$1" --with-decryption --region "$REGION" \
    --query Parameter.Value --output text 2>/dev/null || echo "placeholder"
}

TFVARS="$(mktemp /tmp/queue-api-teardown-tfvars.XXXXXX)"
chmod 600 "$TFVARS"
cat > "$TFVARS" <<EOF
region              = "$REGION"
env_name            = "$ENV_NAME"
github_org          = "$GITHUB_ORG"
github_repo         = "$GITHUB_REPO"
cms_password        = "$(ssm_read "/queue-api/$ENV_NAME/cms-password")"
admin_password      = "$(ssm_read "/queue-api/$ENV_NAME/admin-password")"
regular_password    = "$(ssm_read "/queue-api/$ENV_NAME/regular-password")"
EOF

log "Destroying the $ENV_NAME environment ($REGION)"
terraform destroy -auto-approve -input=false -var-file="$TFVARS"
rm -f "$TFVARS"

# ── 5. Optional: remove the local clone and Terraform install ────────────────────
if [ "$CLEANUP_ALL" = "1" ]; then
  log "Removing local clone ($WORK_DIR), Terraform install, and TF data"
  rm -rf "$WORK_DIR" "$TF_INSTALL_DIR" "$TF_DATA_DIR"
fi

log "Teardown complete. Re-run scripts/bootstrap-aws.sh to recreate the environment."
