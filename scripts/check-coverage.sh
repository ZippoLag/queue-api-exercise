#!/usr/bin/env bash
#
# Aggregates line coverage from every coverlet cobertura report under the
# given results root and fails if the aggregate line rate is below the
# threshold in .config/coverage-min.txt — the "coverage ratchet": the gate
# never regresses, and the threshold only rises when raised deliberately.
#
# Usage: scripts/check-coverage.sh [results-root]
#   results-root  where to look for TestResults/**/coverage.cobertura.xml;
#                 defaults to the repository root (covers the per-project
#                 TestResults/ directories dotnet test emits by default).
#
# METRIC: unique source lines (union across reports). Each test project
# emits one cobertura report, and coverlet instruments EVERY referenced
# assembly per project — so a shared library line (e.g. the QueueApi.Auth
# handler) appears in every report that references it. Summing per-report
# counts would double-count shared code: the AuthDbInit tool's report would
# mark 112 lines of an ASP.NET middleware as "uncovered" even though the
# auth and API test projects cover them. Instead we count each source line
# ONCE: a line is covered if ANY report shows hits > 0 for it, and valid if
# ANY report lists it. This matches how SonarQube/Coveralls report
# solution-level coverage — "every line covered by at least one test".
#
# PATH NORMALIZATION: the six reports emit different prefixes for the same
# source file, so (filename, line) pairs are normalized to a canonical
# repo-relative path before deduplication:
#
#   CmsWebhook/CmsWebhook.Api/...          -> src/CmsWebhook/CmsWebhook.Api/...
#   CmsWebhook/CmsWebhook.Application/...  -> src/CmsWebhook/CmsWebhook.Application/...
#   CmsWebhook/CmsWebhook.Domain/...       -> src/CmsWebhook/CmsWebhook.Domain/...
#   CmsWebhook/CmsWebhook.Infrastructure/... -> src/CmsWebhook/CmsWebhook.Infrastructure/...
#   CmsWebhook.Application/...             -> src/CmsWebhook/CmsWebhook.Application/...
#   CmsWebhook.Domain/...                  -> src/CmsWebhook/CmsWebhook.Domain/...
#   CmsWebhook.Infrastructure/...          -> src/CmsWebhook/CmsWebhook.Infrastructure/...
#   Users/Users.Api/...                    -> src/Users/Users.Api/...
#   Users/Users.Application/...            -> src/Users/Users.Application/...
#   Users/Users.Infrastructure/...         -> src/Users/Users.Infrastructure/...
#   Shared/QueueApi.Auth/...               -> src/Shared/QueueApi.Auth/...
#   src/Shared/QueueApi.Auth/...           (already canonical)
#   tools/AuthDbInit/...                   (already canonical)
#   src/...                                (already canonical)
#
# Bare filenames (emitted by the Domain.Tests and QueueApi.Auth.Tests
# reports, which list files relative to the project directory) are mapped by
# name to their canonical location. Each bare name below is unique to one
# project, so the mapping is unambiguous:
#
#   AuthDbContext.cs / BasicAuthenticationHandler.cs /
#   BasicAuthenticationOptions.cs / BasicAuthenticationServiceCollectionExtensions.cs /
#   DbUserCredentialsProvider.cs / Pbkdf2PasswordHasher.cs / UserCredential.cs
#       -> src/Shared/QueueApi.Auth/
#   CmsEntity.cs / CmsEvent.cs / CmsRequest.cs / CmsRequestValidator.cs
#       -> src/CmsWebhook/CmsWebhook.Domain/
#   EntityListItem.cs / IEntityCommandRepository.cs / IEntityQueryRepository.cs /
#   ListEntitiesQueryHandler.cs / SetEntityVisibilityCommandHandler.cs
#       -> src/Users/Users.Application/
#   EfEntityCommandRepository.cs / EfEntityQueryRepository.cs / UsersDbContext.cs /
#   UsersServiceCollectionExtensions.cs
#       -> src/Users/Users.Infrastructure/
#   EntityEndpoints.cs / HealthEndpoints.cs / Program.cs
#       -> src/Users/Users.Api/
#
# If a path still does not start with src/ or tools/ after normalization the
# script fails loudly, listing the offending path, so an unseen prefix is
# surfaced instead of silently mis-aggregated.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

RESULTS_ROOT="${1:-$REPO_ROOT}"
THRESHOLD_FILE="$REPO_ROOT/.config/coverage-min.txt"

[ -f "$THRESHOLD_FILE" ] || {
  echo "[Error] Coverage threshold file '$THRESHOLD_FILE' not found." >&2
  exit 1
}

MIN="$(tr -d '[:space:]' < "$THRESHOLD_FILE")"
case "$MIN" in
  ''|*[!0-9.]*) echo "[Error] Invalid coverage threshold '$MIN' in '$THRESHOLD_FILE' (expected a number, e.g. 95.1)." >&2; exit 1 ;;
esac

mapfile -t REPORTS < <(find "$RESULTS_ROOT" -path "*/TestResults/*/coverage.cobertura.xml" -type f 2>/dev/null)

if [ "${#REPORTS[@]}" -eq 0 ]; then
  echo "[Error] No cobertura coverage reports found under '$RESULTS_ROOT'. Run 'dotnet test --collect:\"XPlat Code Coverage\"' first." >&2
  exit 1
fi

# Emit every (normalized-path:line) pair from a report; the second column is
# 1 when the line has hits > 0 (covered), 0 otherwise. Duplicate class
# entries for the same file within one report are deduplicated downstream.
extract_lines() {
  awk -v file="$1" '
    /<class / {
      fn = $0; sub(/.*filename="/, "", fn); sub(/".*/, "", fn)
    }
    /<line number=/ {
      ln = $0; sub(/.*number="/, "", ln); sub(/".*/, "", ln)
      h  = $0; sub(/.*hits="/,  "", h);  sub(/".*/,  "", h)
      print normalize(fn) ":" ln "\t" ((h + 0 > 0) ? 1 : 0)
    }
    function normalize(f,   base) {
      if (f ~ /^CmsWebhook\/CmsWebhook\.(Api|Application|Domain|Infrastructure)\//) return "src/" f
      if (f ~ /^CmsWebhook\.(Application|Domain|Infrastructure)\//) return "src/CmsWebhook/" f
      if (f ~ /^Shared\/QueueApi\.Auth\//) return "src/" f
      if (f ~ /^Users\/Users\.(Api|Application|Infrastructure)\//) return "src/" f
      if (f ~ /^(src\/|tools\/)/) return f
      base = f; sub(/.*\//, "", base)
      if (base == "AuthDbContext.cs"            ||
          base == "BasicAuthenticationHandler.cs" ||
          base == "BasicAuthenticationOptions.cs" ||
          base == "BasicAuthenticationServiceCollectionExtensions.cs" ||
          base == "DbUserCredentialsProvider.cs" ||
          base == "Pbkdf2PasswordHasher.cs"     ||
          base == "UserCredential.cs") return "src/Shared/QueueApi.Auth/" f
      if (base == "CmsEntity.cs" || base == "CmsEvent.cs" ||
          base == "CmsRequest.cs" || base == "CmsRequestValidator.cs") return "src/CmsWebhook/CmsWebhook.Domain/" f
      if (base == "EntityListItem.cs" || base == "IEntityCommandRepository.cs" ||
          base == "IEntityQueryRepository.cs" || base == "ListEntitiesQueryHandler.cs" ||
          base == "SetEntityVisibilityCommandHandler.cs") return "src/Users/Users.Application/" f
      if (base == "EfEntityCommandRepository.cs" || base == "EfEntityQueryRepository.cs" ||
          base == "UsersDbContext.cs" || base == "UsersServiceCollectionExtensions.cs") return "src/Users/Users.Infrastructure/" f
      if (base == "EntityEndpoints.cs" || base == "HealthEndpoints.cs" || base == "Program.cs") return "src/Users/Users.Api/" f
      return "UNKNOWN:" f
    }
  ' "$1"
}

# Union of covered and valid lines across all reports (dedup by key, keeping
# covered=true if ANY report covered the line).
awk -F'\t' '
  {
    if ($2 + 0 > 0) covered[$1] = 1
    valid[$1] = 1
  }
  END {
    for (k in covered) print k
  }
' < <(for report in "${REPORTS[@]}"; do extract_lines "$report"; done) | sort -u > /tmp/check-coverage-covered.$$

awk -F'\t' '{ print $1 }' < <(for report in "${REPORTS[@]}"; do extract_lines "$report"; done) | sort -u > /tmp/check-coverage-valid.$$

# Fail loudly on paths that escaped normalization.
UNKNOWN=$(grep -c '^UNKNOWN:' /tmp/check-coverage-valid.$$ || true)
if [ "$UNKNOWN" -gt 0 ]; then
  echo "[Error] $UNKNOWN coverage path(s) could not be normalized; add them to the mapping in '$SCRIPT_DIR/check-coverage.sh':" >&2
  grep '^UNKNOWN:' /tmp/check-coverage-valid.$$ | head -10 | sed 's/^/  /' >&2
  rm -f /tmp/check-coverage-covered.$$ /tmp/check-coverage-valid.$$
  exit 1
fi

TOTAL_VALID=$(wc -l < /tmp/check-coverage-valid.$$)
TOTAL_COVERED=$(wc -l < /tmp/check-coverage-covered.$$)
rm -f /tmp/check-coverage-covered.$$ /tmp/check-coverage-valid.$$

[ "$TOTAL_VALID" -gt 0 ] || {
  echo "[Error] No valid lines found across ${#REPORTS[@]} coverage report(s)." >&2
  exit 1
}

# awk does the float math; bash integers can't represent 95.1
RATE="$(awk -v c="$TOTAL_COVERED" -v v="$TOTAL_VALID" 'BEGIN { printf "%.2f", 100.0 * c / v }')"
PASS="$(awk -v r="$RATE" -v m="$MIN" 'BEGIN { print (r >= m) ? 1 : 0 }')"

echo "[Information] Unique-line coverage: $TOTAL_COVERED/$TOTAL_VALID lines ($RATE%), threshold: $MIN% (${#REPORTS[@]} report(s))"

if [ "$PASS" -eq 1 ]; then
  echo "[Information] Coverage gate passed."
else
  echo "[Error] Coverage gate failed: $RATE% is below the minimum of $MIN%." >&2
  echo "[Error] Raise coverage, or deliberately raise the threshold in '$THRESHOLD_FILE' only when appropriate." >&2
  exit 1
fi
