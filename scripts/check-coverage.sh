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
# Each test project emits its own cobertura report (dotnet test
# --collect:"XPlat Code Coverage" produces one file per project), so the
# script sums lines-valid / lines-covered across every report found instead
# of trusting a single file.
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
  ''|*[!0-9.]*) echo "[Error] Invalid coverage threshold '$MIN' in '$THRESHOLD_FILE' (expected a number, e.g. 75.5)." >&2; exit 1 ;;
esac

mapfile -t REPORTS < <(find "$RESULTS_ROOT" -path "*/TestResults/*/coverage.cobertura.xml" -type f 2>/dev/null)

if [ "${#REPORTS[@]}" -eq 0 ]; then
  echo "[Error] No cobertura coverage reports found under '$RESULTS_ROOT'. Run 'dotnet test --collect:\"XPlat Code Coverage\"' first." >&2
  exit 1
fi

TOTAL_VALID=0
TOTAL_COVERED=0
for report in "${REPORTS[@]}"; do
  # Sum the root element's per-project totals: lines-covered="N" lines-valid="M"
  read -r covered valid < <(grep -o 'lines-covered="[0-9]*" lines-valid="[0-9]*"' "$report" | head -n1 | grep -o '[0-9]*' | paste -sd ' ' -)
  TOTAL_COVERED=$((TOTAL_COVERED + ${covered:-0}))
  TOTAL_VALID=$((TOTAL_VALID + ${valid:-0}))
done

[ "$TOTAL_VALID" -gt 0 ] || {
  echo "[Error] No valid lines found across ${#REPORTS[@]} coverage report(s)." >&2
  exit 1
}

# awk does the float math; bash integers can't represent 75.5
RATE="$(awk -v c="$TOTAL_COVERED" -v v="$TOTAL_VALID" 'BEGIN { printf "%.2f", 100.0 * c / v }')"
PASS="$(awk -v r="$RATE" -v m="$MIN" 'BEGIN { print (r >= m) ? 1 : 0 }')"

echo "[Information] Aggregate line coverage: $TOTAL_COVERED/$TOTAL_VALID lines ($RATE%), threshold: $MIN% (${#REPORTS[@]} report(s))"

if [ "$PASS" -eq 1 ]; then
  echo "[Information] Coverage gate passed."
else
  echo "[Error] Coverage gate failed: $RATE% is below the minimum of $MIN%." >&2
  echo "[Error] Raise coverage, or deliberately raise the threshold in '$THRESHOLD_FILE' only when appropriate." >&2
  exit 1
fi
