#!/usr/bin/env bash
set -euo pipefail
: "${GODOT:?Set GODOT to the Godot 4.7.2 executable}"
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export MET_EVIDENCE_DIR="${MET_EVIDENCE_DIR:-/tmp/met-beta-evidence}"
mkdir -p "$MET_EVIDENCE_DIR"
for suite in beta_time beta_bonus beta_themes beta_lifecycle beta_planning beta_feedback beta_math_cache beta_acceptance beta_playthrough; do
  test -f "$PROJECT_ROOT/tests/$suite.gd" || { echo "Required Beta suite missing: $suite" >&2; exit 1; }
  logfile="$MET_EVIDENCE_DIR/$suite.log"
  "$GODOT" --headless --path "$PROJECT_ROOT" --script "res://tests/$suite.gd" >"$logfile" 2>&1
  if rg -n 'SCRIPT ERROR|Parse Error|Compile Error|^FAIL| FAIL ' "$logfile"; then
    exit 1
  fi
  tail -n 2 "$logfile"
done
if [[ "${MET_BETA_PERFORMANCE:-0}" == 1 ]]; then
  "$GODOT" --headless --path "$PROJECT_ROOT" --script res://tests/beta_performance.gd >"$MET_EVIDENCE_DIR/beta_performance.log" 2>&1
  if rg -n 'SCRIPT ERROR|Parse Error|Compile Error|^FAIL| FAIL ' "$MET_EVIDENCE_DIR/beta_performance.log"; then exit 1; fi
  tail -n 2 "$MET_EVIDENCE_DIR/beta_performance.log"
fi
