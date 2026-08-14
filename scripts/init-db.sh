#!/usr/bin/env bash
#
# Initializes the shared SQLite credential store used by the APIs, seeding the three
# reserved users: cms-webhook, administrator and regular-user.
#
# Usage: scripts/init-db.sh [cms-password] [admin-password] [regular-password]
#   cms-password      password of the reserved cms-webhook user (used by the CMS Webhook API)
#   admin-password    password of the reserved administrator user (used by the Users API)
#   regular-password  password of the reserved regular-user user (used by the Users API)
#
# Every password defaults to a local-development default (see README) which MUST NOT be
# used outside local development. Passwords are positional arguments only; no credentials
# are ever read from environment variables.
#
# Idempotent: re-running over an already-initialized store is a no-op.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Default matches the API's own resolution: relative data sources resolve against the content
# root, which for the web project is its project directory (src/CmsWebhook/CmsWebhook.Api),
# where the credential store now lives (see docs/configuration.md).
DB_PATH="${DB_PATH:-$REPO_ROOT/src/CmsWebhook/CmsWebhook.Api/db/queue-auth.db}"
CMS_PASSWORD="${1:-0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61}"
ADMIN_PASSWORD="${2:-a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d}"
REGULAR_PASSWORD="${3:-6d5c4b3a-2f1e-4d0c-9b8a-7f6e5d4c3b2a}"

if [ "$#" -lt 3 ]; then
  echo "[Warning] At least one password was not supplied; using the local-development default passwords." >&2
  echo "[Warning] Do NOT use these defaults outside local development." >&2
fi

mkdir -p "$(dirname "$DB_PATH")"

dotnet run --project "$REPO_ROOT/tools/AuthDbInit" -- \
  "$DB_PATH" "$CMS_PASSWORD" "$ADMIN_PASSWORD" "$REGULAR_PASSWORD"

echo "[Information] Credential store ready at '$DB_PATH'."
