#!/usr/bin/env bash
set -euo pipefail
: "${GODOT:?Set GODOT to Godot 4.7.2 editor or this build executable}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export MET_EVIDENCE_DIR="${MET_EVIDENCE_DIR:-$ROOT/evidence030/local}"
mkdir -p "$MET_EVIDENCE_DIR"
cp -n "$ROOT/evidence030/final/legacy020-fixture.json" "$MET_EVIDENCE_DIR/" 2>/dev/null || true
cp -n "$ROOT/evidence030/final/legacy021-fixture.json" "$MET_EVIDENCE_DIR/" 2>/dev/null || true
for flag in --test --test030 --playthrough; do
  logfile="$MET_EVIDENCE_DIR/${flag#--}.log"
  "$GODOT" --headless --path "$ROOT" -- "$flag" >"$logfile" 2>&1
  if grep -E 'SCRIPT ERROR|Parse Error|Compile Error|^FAIL|VIEW FAIL' "$logfile"; then exit 1; fi
  tail -n 3 "$logfile"
done
