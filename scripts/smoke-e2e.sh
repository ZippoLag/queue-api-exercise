#!/usr/bin/env bash
#
# Real-process end-to-end smoke test. Unlike the in-process WebApplicationFactory tests, this publishes
# both APIs, provisions a REAL credential store through scripts/init-db.sh, starts both APIs as real
# processes against real SQLite files, and drives the full vertical over real HTTP:
#
#   ingest (cms-webhook -> CMS Webhook API) -> outbox processing -> list (regular-user -> Users API)
#   -> disable/enable (administrator -> Users API) -> cms-webhook rejected on the Users API
#   -> the UI shell served at the Users API origin root
#   -> the rejection contract: 401 anonymous, 400 invalid timestamp / non-object payload /
#     whitespace-only id, 204 padded-id trim, 404 unknown id (each rejected id proven never listed)
#
# Usage: scripts/smoke-e2e.sh
#   CMS_PORT / USERS_PORT   ports to bind (defaults 5264/5265)
#
# The stores live in a throwaway temp directory, deleted on exit. Everything runs with
# ASPNETCORE_ENVIRONMENT=Production and the stores supplied through environment variables, so the smoke
# mirrors the deployment path documented in the README.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

CMS_PORT="${CMS_PORT:-5264}"
USERS_PORT="${USERS_PORT:-5265}"
CMS_BASE_URL="http://127.0.0.1:$CMS_PORT"
USERS_BASE_URL="http://127.0.0.1:$USERS_PORT"

CMS_USER="cms-webhook"
ADMIN_USER="administrator"
REGULAR_USER="regular-user"
# Local-development defaults, kept in sync with scripts/init-db.sh and the README.
CMS_PASSWORD="0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61"
ADMIN_PASSWORD="a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"
REGULAR_PASSWORD="6d5c4b3a-2f1e-4d0c-9b8a-7f6e5d4c3b2a"

WORK_DIR="$(mktemp -d)"
AUTH_DB_PATH="$WORK_DIR/queue-auth.db"
CMS_DB_PATH="$WORK_DIR/queue-cms.db"
PUBLISH_DIR="$WORK_DIR/publish"
CMS_PID=""
USERS_PID=""

cleanup() {
  if [ -n "$CMS_PID" ]; then kill "$CMS_PID" 2>/dev/null || true; fi
  if [ -n "$USERS_PID" ]; then kill "$USERS_PID" 2>/dev/null || true; fi
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

fail() {
  echo "[Error] $*" >&2
  exit 1
}

# Publishes an API and returns its executable path.
publish_api() {
  local project="$1" output="$2"
  dotnet publish "$REPO_ROOT/$project" -c Release -o "$output" --nologo -v:q \
    || fail "dotnet publish of $project failed."
}

# Starts a published API, waits for its /health probe, and records the pid.
# The executable runs with the publish directory as its working directory, mirroring the systemd unit's
# WorkingDirectory=/opt/queue-api/<app>: ASP.NET Core resolves the content root (and therefore wwwroot,
# where the published Blazor client shell lives) from the current directory.
start_api() {
  local executable="$1" port="$2"
  local app_dir
  app_dir="$(cd "$(dirname "$executable")" && pwd)"
  (cd "$app_dir" && \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS="http://127.0.0.1:$port" \
    ConnectionStrings__AuthDb="Data Source=$AUTH_DB_PATH" \
    ConnectionStrings__CmsDb="Data Source=$CMS_DB_PATH;Default Timeout=30" \
      "$executable" >"$WORK_DIR/$(basename "$executable").log" 2>&1) &
  local pid=$!

  local deadline=$((SECONDS + 30))
  while [ "$SECONDS" -lt "$deadline" ]; do
    if curl -fsS "http://127.0.0.1:$port/health" >/dev/null 2>&1; then
      echo "$pid"
      return 0
    fi
    if ! kill -0 "$pid" 2>/dev/null; then
      fail "API $executable exited before becoming healthy; log:" \
        && tail -5 "$WORK_DIR/$(basename "$executable").log" >&2
    fi
    sleep 1
  done
  kill "$pid" 2>/dev/null || true
  fail "Timed out waiting for $executable to become healthy on port $port."
}

# Asserts the HTTP status of a request. An empty user sends NO Authorization header at all, so the
# anonymous 401 assertion proves the true no-credentials case (design D5 of extend-e2e-smoke-contract).
expect_status() {
  local method="$1" url="$2" expected="$3" user="$4" password="$5" body="${6:-}"
  local actual
  local auth_args=()
  if [ -n "$user" ]; then auth_args=(-u "$user:$password"); fi
  if [ -n "$body" ]; then
    actual=$(curl -sS -o "$WORK_DIR/last-body.json" -w "%{http_code}" \
      "${auth_args[@]}" -X "$method" -H "Content-Type: application/json" -d "$body" "$url") \
      || fail "Request failed: $method $url"
  else
    actual=$(curl -sS -o /dev/null -w "%{http_code}" "${auth_args[@]}" -X "$method" "$url") \
      || fail "Request failed: $method $url"
  fi
  [ "$actual" = "$expected" ] || fail "Expected HTTP $expected for $method $url, got $actual."
}

# Asserts a rejected entity id never appears in the regular-user listing. Single-shot by design: a
# rejected request never enters the pipeline, so absence is immediate and polling would only add
# flakiness (design D2 of extend-e2e-smoke-contract).
assert_absent() {
  local entity_id="$1"
  local body
  body=$(curl -sS -u "$REGULAR_USER:$REGULAR_PASSWORD" "$USERS_BASE_URL/entities") \
    || fail "Regular-user listing request failed."
  if echo "$body" | grep -q "\"id\":\"$entity_id\""; then
    fail "Rejected entity '$entity_id' unexpectedly appears in the listing."
  fi
}

# Polls the regular-user listing until the entity is present (1) or absent (0).
wait_for_entity() {
  local entity_id="$1" expect_present="$2"
  local deadline=$((SECONDS + 20))
  while [ "$SECONDS" -lt "$deadline" ]; do
    local body
    body=$(curl -sS -u "$REGULAR_USER:$REGULAR_PASSWORD" "$USERS_BASE_URL/entities") \
      || fail "Regular-user listing request failed."
    if echo "$body" | grep -q "\"id\":\"$entity_id\""; then
      [ "$expect_present" = "1" ] && return 0
    elif [ "$expect_present" = "0" ]; then
      return 0
    fi
    sleep 1
  done
  fail "Timed out waiting for entity '$entity_id' presence=$expect_present in the regular listing."
}

# Polls the administrator listing until the entity reports the expected latest version AND is still
# flagged disabled, proving a CMS event was processed into the shared store without resetting the flag.
wait_for_admin_version_and_disabled() {
  local entity_id="$1" expected_version="$2"
  local deadline=$((SECONDS + 20))
  while [ "$SECONDS" -lt "$deadline" ]; do
    local body
    body=$(curl -sS -u "$ADMIN_USER:$ADMIN_PASSWORD" "$USERS_BASE_URL/entities") \
      || fail "Administrator listing request failed."
    if echo "$body" | grep -q "\"id\":\"$entity_id\"" \
      && echo "$body" | grep -q "\"latestVersion\":$expected_version" \
      && echo "$body" | grep -q "\"isVisibleByAdmin\":false"; then
      return 0
    fi
    sleep 1
  done
  fail "Timed out waiting for '$entity_id' at version $expected_version, still disabled, in the admin listing."
}

echo "[Information] Seeding the real credential store via scripts/init-db.sh"
DB_PATH="$AUTH_DB_PATH" "$SCRIPT_DIR/init-db.sh" "$CMS_PASSWORD" "$ADMIN_PASSWORD" "$REGULAR_PASSWORD"

echo "[Information] Publishing the APIs"
publish_api "src/CmsWebhook/CmsWebhook.Api/CmsWebhook.Api.csproj" "$PUBLISH_DIR/cms"
publish_api "src/Users/Users.Api/Users.Api.csproj" "$PUBLISH_DIR/users"

echo "[Information] Starting the CMS Webhook API (port $CMS_PORT) and the Users API (port $USERS_PORT)"
# The CMS Webhook API must start first: its startup provisions the shared CMS schema (cms_event_log +
# cms_entities) on the file the Users API reads.
CMS_PID=$(start_api "$PUBLISH_DIR/cms/CmsWebhook.Api" "$CMS_PORT")
USERS_PID=$(start_api "$PUBLISH_DIR/users/Users.Api" "$USERS_PORT")

echo "[Information] Anonymous health probes"
curl -fsS "$CMS_BASE_URL/health" >/dev/null || fail "CMS /health did not return 200."
curl -fsS "$USERS_BASE_URL/health" >/dev/null || fail "Users /health did not return 200."

echo "[Information] The Users API serves the browser UI shell at its origin root"
curl -fsS "$USERS_BASE_URL/" | grep -q "_framework/blazor.webassembly.js" \
  || fail "The Users API origin root did not serve the Blazor UI shell."

echo "[Information] Ingesting a publish event through the CMS Webhook API"
expect_status POST "$CMS_BASE_URL/cms/events" 201 "$CMS_USER" "$CMS_PASSWORD" \
  '{"type":"publish","id":"entity-1","payload":{"title":"hello"},"version":1,"timestamp":"2024-01-01T00:00:00Z"}'

echo "[Information] Waiting for the entity to appear in the regular-user listing"
wait_for_entity "entity-1" 1

echo "[Information] cms-webhook is rejected on the Users API"
expect_status GET "$USERS_BASE_URL/entities" 403 "$CMS_USER" "$CMS_PASSWORD"

echo "[Information] Administrator disables and enables the entity"
expect_status POST "$USERS_BASE_URL/entities/entity-1/disable" 204 "$ADMIN_USER" "$ADMIN_PASSWORD"
wait_for_entity "entity-1" 0

# A subsequent CMS update rewrites the stored entity; the disable must survive it (users-api design
# decision 3: the upsert carries the visibility override forward).
expect_status POST "$CMS_BASE_URL/cms/events" 201 "$CMS_USER" "$CMS_PASSWORD" \
  '{"type":"update","id":"entity-1","payload":{"title":"updated"},"version":2,"timestamp":"2024-01-01T00:00:01Z"}'
wait_for_admin_version_and_disabled "entity-1" 2
wait_for_entity "entity-1" 0

expect_status POST "$USERS_BASE_URL/entities/entity-1/enable" 204 "$ADMIN_USER" "$ADMIN_PASSWORD"
wait_for_entity "entity-1" 1

echo "[Information] Anonymous OpenAPI contract describes the entity endpoints"
curl -fsS "$USERS_BASE_URL/openapi/v1.json" | grep -q '"/entities/{id}/disable"' \
  || fail "The Users API OpenAPI contract does not describe /entities/{id}/disable."

echo "[Information] Rejection contract: anonymous requests return 401 without any Authorization header"
expect_status GET "$USERS_BASE_URL/entities" 401 "" ""
expect_status POST "$CMS_BASE_URL/cms/events" 401 "" "" \
  '{"type":"publish","id":"entity-x","payload":{},"version":1,"timestamp":"2024-01-01T00:00:00Z"}'

echo "[Information] Rejection contract: non-RFC 3339 timestamps and non-object payloads return 400 and record nothing"
expect_status POST "$CMS_BASE_URL/cms/events" 400 "$CMS_USER" "$CMS_PASSWORD" \
  '{"type":"publish","id":"smoke-reject-ts-1","payload":{},"version":1,"timestamp":"2024-01-01"}'
assert_absent "smoke-reject-ts-1"
expect_status POST "$CMS_BASE_URL/cms/events" 400 "$CMS_USER" "$CMS_PASSWORD" \
  '{"type":"publish","id":"smoke-reject-payload-1","payload":[],"version":1,"timestamp":"2024-01-01T00:00:00Z"}'
assert_absent "smoke-reject-payload-1"

echo "[Information] Rejection contract: whitespace-only id returns 400, padded id is trimmed, unknown id returns 404"
expect_status POST "$USERS_BASE_URL/entities/%20%20/disable" 400 "$ADMIN_USER" "$ADMIN_PASSWORD"

# The padded-id check ingests its own entity (design D2) so the acceptance flow's entity-1 state is untouched.
expect_status POST "$CMS_BASE_URL/cms/events" 201 "$CMS_USER" "$CMS_PASSWORD" \
  '{"type":"publish","id":"smoke-padded-1","payload":{"title":"padded"},"version":1,"timestamp":"2024-01-01T00:00:00Z"}'
wait_for_entity "smoke-padded-1" 1
expect_status POST "$USERS_BASE_URL/entities/%20smoke-padded-1%20/disable" 204 "$ADMIN_USER" "$ADMIN_PASSWORD"
wait_for_entity "smoke-padded-1" 0

expect_status POST "$USERS_BASE_URL/entities/no-such-entity/disable" 404 "$ADMIN_USER" "$ADMIN_PASSWORD"

echo "[Information] Smoke test passed: both APIs interoperated over the real seeded stores and the rejection contract holds."
