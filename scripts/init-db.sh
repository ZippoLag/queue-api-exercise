#!/usr/bin/env bash
#
# Initializes the shared SQLite credential store used by the APIs.
#
# Usage: scripts/init-db.sh [username] [password]
#   username  defaults to "cms-webhook"
#   password  defaults to a local-development default (see README) which MUST NOT be
#             used outside local development.
#
# Idempotent: re-running over an already-initialized store is a no-op.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Default matches the API's own resolution: relative data sources resolve against the content root,
# which for the web project is its project directory (src/CmsWebhook/CmsWebhook.Api), where the
# credential store now lives (see docs/configuration.md).
DB_PATH="${DB_PATH:-$REPO_ROOT/src/CmsWebhook/CmsWebhook.Api/db/queue-auth.db}"
USERNAME="${1:-cms-webhook}"
PASSWORD="${2:-0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61}"

if [ "$#" -lt 2 ]; then
  echo "[Warning] No password supplied; using the local-development default password." >&2
  echo "[Warning] Do NOT use this default outside local development." >&2
fi

mkdir -p "$(dirname "$DB_PATH")"

dotnet run --project "$REPO_ROOT/tools/AuthDbInit" -- "$DB_PATH" "$USERNAME" "$PASSWORD"

echo "[Information] Credential store ready at '$DB_PATH'."
