#!/usr/bin/env bash
#
# Deploys the two APIs to the AWS node: publishes (Release), uploads the artifacts to
# the S3 bucket, ships them to the node via SSM Run Command (no SSH port), seeds the
# credential store idempotently, restarts the services in order (cms-api first), and
# verifies the live deployment (health probes + the UI shell + the smoke flow).
#
# Usage:
#   scripts/deploy-aws.sh [--skip-publish] [--rollback]
#
# Environment (all optional except S3_BUCKET/INSTANCE_ID):
#   REGION        AWS region (default eu-west-3)
#   ENV_NAME      environment/stack name (default demo)
#   S3_BUCKET     artifact bucket (required; see infra/aws outputs)
#   INSTANCE_ID   EC2 instance id (required; see infra/aws outputs)
#   DOMAIN        public domain; empty → self-signed URLs on the Elastic IP
#   PUBLISH_DIR   where to publish; default a throwaway temp dir
#
# Modes:
#   (default)       publish → upload → deploy → verify
#   --skip-publish  deploy the artifacts already in S3 (used by scripts/bootstrap-aws.sh)
#   --rollback      restore the previous artifacts on the node and restart
#
# The identity running this script must be able to publish to the bucket, send SSM Run
# Command, describe instances, and read the /queue-api/<env>/* SSM parameters (the CI
# OIDC role and the CloudShell console user both have these). The API units are also
# `systemctl enable`d so they start again after an instance stop/start or reboot.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

REGION="${REGION:-eu-west-3}"
ENV_NAME="${ENV_NAME:-demo}"
S3_BUCKET="${S3_BUCKET:-}"
INSTANCE_ID="${INSTANCE_ID:-}"
DOMAIN="${DOMAIN:-}"
PUBLISH_DIR="${PUBLISH_DIR:-$(mktemp -d /tmp/queue-api-publish.XXXXXX)}"

MODE="publish"
for arg in "$@"; do
  case "$arg" in
    --skip-publish) MODE="skip-publish" ;;
    --rollback)     MODE="rollback" ;;
    *) echo "[Error] Unknown argument: $arg" >&2; exit 1 ;;
  esac
done

[ -n "$S3_BUCKET" ]   || { echo "[Error] S3_BUCKET is required." >&2; exit 1; }
[ -n "$INSTANCE_ID" ] || { echo "[Error] INSTANCE_ID is required." >&2; exit 1; }

S3_PREFIX="s3://$S3_BUCKET/latest"

log()   { echo "[Information] $*"; }
fail()  { echo "[Error] $*" >&2; exit 1; }

# --- publish + upload -----------------------------------------------------------
# The node runs the .NET runtime installed by user-data (framework-dependent), but the
# apphost launcher binaries must match the node's CPU architecture — publishing without
# a RID produces x64 apphosts that an ARM64 (Graviton) node cannot execute.
publish_rid() {
  local arch
  arch="$(aws ec2 describe-instances --instance-ids "$INSTANCE_ID" --region "$REGION" \
    --query 'Reservations[0].Instances[0].Architecture' --output text)" || fail "Could not resolve the instance architecture."
  case "$arch" in
    arm64)   echo "linux-arm64" ;;
    x86_64)  echo "linux-x64" ;;
    *)       fail "Unsupported instance architecture: $arch" ;;
  esac
}

publish_and_upload() {
  local rid
  rid="$(publish_rid)"
  log "Publishing the CMS Webhook API, Users API and AuthDbInit (Release, $rid)"
  dotnet publish "$REPO_ROOT/src/CmsWebhook/CmsWebhook.Api/CmsWebhook.Api.csproj" -c Release -r "$rid" -o "$PUBLISH_DIR/cms" --nologo -v:q \
    || fail "dotnet publish of CmsWebhook.Api failed."
  dotnet publish "$REPO_ROOT/src/Users/Users.Api/Users.Api.csproj" -c Release -r "$rid" -o "$PUBLISH_DIR/users" --nologo -v:q \
    || fail "dotnet publish of Users.Api failed."
  dotnet publish "$REPO_ROOT/tools/AuthDbInit/AuthDbInit.csproj" -c Release -r "$rid" -o "$PUBLISH_DIR/auth-db-init" --nologo -v:q \
    || fail "dotnet publish of AuthDbInit failed."

  log "Uploading artifacts to $S3_PREFIX"
  aws s3 sync "$PUBLISH_DIR" "$S3_PREFIX/" --delete --region "$REGION" \
    || fail "aws s3 sync to $S3_PREFIX failed."
}

# --- SSM Run Command -------------------------------------------------------------
# The remote script runs on the node with the instance role (SSM managed-instance core,
# s3:GetObject, ssm:GetParameters), so no secrets travel through the command text.
run_remote() {
  local remote_bash="$1"
  local comment="$2"
  local b64
  b64="$(printf '%s' "$remote_bash" | base64 -w0)"

  log "Sending SSM Run Command ($comment) to $INSTANCE_ID"
  local cmd_id
  cmd_id="$(aws ssm send-command \
    --instance-ids "$INSTANCE_ID" \
    --document-name "AWS-RunShellScript" \
    --comment "$comment" \
    --parameters "{\"commands\":[\"echo $b64 | base64 -d | bash\"],\"executionTimeout\":[\"900\"]}" \
    --region "$REGION" \
    --query "Command.CommandId" --output text)" \
    || fail "ssm send-command failed."

  local status deadline=$((SECONDS + 600))
  while [ "$SECONDS" -lt "$deadline" ]; do
    # The invocation may not exist for a moment after send-command; keep polling.
    if ! status="$(aws ssm get-command-invocation --command-id "$cmd_id" --instance-id "$INSTANCE_ID" \
      --region "$REGION" --query "Status" --output text 2>/dev/null)"; then
      sleep 5
      continue
    fi
    case "$status" in
      Success) log "Run Command succeeded."; return 0 ;;
      Failed|TimedOut|Cancelled|Undeliverable|Terminated)
        echo "--- remote stderr ---" >&2
        aws ssm get-command-invocation --command-id "$cmd_id" --instance-id "$INSTANCE_ID" \
          --region "$REGION" --query "StandardErrorContent" --output text >&2 || true
        fail "Run Command $comment failed with status: $status" ;;
    esac
    sleep 10
  done
  fail "Timed out waiting for Run Command $comment."
}

deploy_remote_script() {
  cat <<REMOTE
set -euo pipefail
REGION="$REGION"
ENV="$ENV_NAME"
BUCKET="$S3_BUCKET"
STAGE=/opt/queue-api/.stage
rm -rf "\$STAGE" && mkdir -p "\$STAGE"
aws s3 sync "s3://\$BUCKET/latest/" "\$STAGE/" --delete --region "\$REGION"
for app in cms users auth-db-init; do
  if [ -d "/opt/queue-api/\$app" ]; then
    rm -rf "/opt/queue-api/\$app.previous"
    mv "/opt/queue-api/\$app" "/opt/queue-api/\$app.previous"
  fi
  mv "\$STAGE/\$app" "/opt/queue-api/\$app"
done
chmod +x /opt/queue-api/cms/CmsWebhook.Api /opt/queue-api/users/Users.Api /opt/queue-api/auth-db-init/AuthDbInit
read_ssm() { aws ssm get-parameter --region "\$REGION" --name "\$1" --with-decryption --query Parameter.Value --output text; }
CMS_PW="\$(read_ssm "/queue-api/\$ENV/cms-password")"
ADMIN_PW="\$(read_ssm "/queue-api/\$ENV/admin-password")"
REGULAR_PW="\$(read_ssm "/queue-api/\$ENV/regular-password")"
# Idempotent seed (same contract as scripts/init-db.sh): a no-op over an existing store.
mkdir -p /var/lib/queue-api
/opt/queue-api/auth-db-init/AuthDbInit /var/lib/queue-api/queue-auth.db "\$CMS_PW" "\$ADMIN_PW" "\$REGULAR_PW"
systemctl daemon-reload
systemctl restart cms-api
systemctl restart users-api
# Enable for boot: user-data only enables Caddy, so without this the APIs would not
# come back after a stop/start or reboot (only a deploy/rollback restarts them).
systemctl enable cms-api users-api
sleep 3
systemctl is-active cms-api users-api
REMOTE
}

rollback_remote_script() {
  cat <<REMOTE
set -euo pipefail
for app in cms users auth-db-init; do
  if [ -d "/opt/queue-api/\$app.previous" ]; then
    rm -rf "/opt/queue-api/\$app"
    mv "/opt/queue-api/\$app.previous" "/opt/queue-api/\$app"
  fi
done
chmod +x /opt/queue-api/cms/CmsWebhook.Api /opt/queue-api/users/Users.Api /opt/queue-api/auth-db-init/AuthDbInit 2>/dev/null || true
systemctl restart cms-api
systemctl restart users-api
systemctl enable cms-api users-api
sleep 3
systemctl is-active cms-api users-api
REMOTE
}

# --- live verification ------------------------------------------------------------
api_urls() {
  if [ -n "$DOMAIN" ]; then
    CMS_URL="https://cms.$DOMAIN"
    USERS_URL="https://users.$DOMAIN"
    CURL_EXTRA=()
  else
    local ip
    ip="$(aws ec2 describe-instances --instance-ids "$INSTANCE_ID" \
      --region "$REGION" --query "Reservations[0].Instances[0].PublicIpAddress" --output text)" \
      || fail "Could not resolve the instance public IP."
    CMS_URL="https://$ip"
    USERS_URL="https://$ip:8443"
    CURL_EXTRA=(-k) # self-signed internal certificate in the domainless variant
  fi
}

wait_for_health() {
  local url="$1" name="$2" deadline=$((SECONDS + 120))
  log "Probing $name /health"
  while [ "$SECONDS" -lt "$deadline" ]; do
    if curl -fsS "${CURL_EXTRA[@]}" "$url/health" >/dev/null 2>&1; then
      log "$name /health OK"
      return 0
    fi
    sleep 5
  done
  fail "Timed out waiting for $name /health on $url."
}

verify_ui_shell() {
  log "Checking the Users API serves the UI shell at its origin root"
  curl -fsS "${CURL_EXTRA[@]}" "$USERS_URL/" | grep -q "_framework/blazor.webassembly.js" \
    || fail "The users host root did not serve the Blazor UI shell."
}

expect_status() {
  local method="$1" url="$2" expected="$3" user="$4" password="$5" body="${6:-}"
  local actual
  if [ -n "$body" ]; then
    actual="$(curl -sS -o /tmp/deploy-aws-last.json -w "%{http_code}" "${CURL_EXTRA[@]}" \
      -u "$user:$password" -X "$method" -H "Content-Type: application/json" -d "$body" "$url")"
  else
    actual="$(curl -sS -o /dev/null -w "%{http_code}" "${CURL_EXTRA[@]}" -u "$user:$password" -X "$method" "$url")"
  fi
  [ "$actual" = "$expected" ] || fail "Expected HTTP $expected for $method $url, got $actual."
}

wait_for_entity() {
  local entity_id="$1" expect_present="$2" regular_user="$3" regular_pw="$4" deadline=$((SECONDS + 60))
  while [ "$SECONDS" -lt "$deadline" ]; do
    local body
    body="$(curl -sS "${CURL_EXTRA[@]}" -u "$regular_user:$regular_pw" "$USERS_URL/entities")" \
      || fail "Regular-user listing request failed."
    if echo "$body" | grep -q "\"id\":\"$entity_id\""; then
      [ "$expect_present" = "1" ] && return 0
    elif [ "$expect_present" = "0" ]; then
      return 0
    fi
    sleep 2
  done
  fail "Timed out waiting for entity '$entity_id' presence=$expect_present in the regular listing."
}

verify_live() {
  api_urls
  wait_for_health "$CMS_URL" "CMS Webhook API"
  wait_for_health "$USERS_URL" "Users API"
  verify_ui_shell

  local cms_pw admin_pw regular_pw
  read_ssm() { aws ssm get-parameter --region "$REGION" --name "$1" --with-decryption --query Parameter.Value --output text; }
  cms_pw="$(read_ssm "/queue-api/$ENV_NAME/cms-password")"
  admin_pw="$(read_ssm "/queue-api/$ENV_NAME/admin-password")"
  regular_pw="$(read_ssm "/queue-api/$ENV_NAME/regular-password")"

  log "Running the smoke flow against the live deployment"
  expect_status POST "$CMS_URL/cms/events" 201 "cms-webhook" "$cms_pw" \
    '{"type":"publish","id":"deploy-verify-1","payload":{"title":"verify"},"version":1,"timestamp":"2026-01-01T00:00:00Z"}'
  wait_for_entity "deploy-verify-1" 1 "regular-user" "$regular_pw"
  expect_status GET "$USERS_URL/entities" 403 "cms-webhook" "$cms_pw"
  expect_status POST "$USERS_URL/entities/deploy-verify-1/disable" 204 "administrator" "$admin_pw"
  wait_for_entity "deploy-verify-1" 0 "regular-user" "$regular_pw"
  expect_status POST "$USERS_URL/entities/deploy-verify-1/enable" 204 "administrator" "$admin_pw"
  wait_for_entity "deploy-verify-1" 1 "regular-user" "$regular_pw"

  log "Live verification passed: both /health endpoints healthy, the UI shell is served, and the smoke flow succeeded."
}

# --- main -------------------------------------------------------------------------
case "$MODE" in
  publish)
    publish_and_upload
    run_remote "$(deploy_remote_script)" "queue-api deploy"
    verify_live
    ;;
  skip-publish)
    run_remote "$(deploy_remote_script)" "queue-api deploy (existing artifacts)"
    verify_live
    ;;
  rollback)
    log "Rolling back to the previous artifacts"
    run_remote "$(rollback_remote_script)" "queue-api rollback"
    verify_live
    ;;
esac
