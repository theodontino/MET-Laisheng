#!/usr/bin/env bash
# Serial checks: never overlap simulation, native render or Blender measurements.
set -euo pipefail
: "${GODOT:?Set GODOT to the Godot 4.7.2 Standard executable}"
MET_PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export MET_EVIDENCE_DIR="${MET_EVIDENCE_DIR:-/tmp/met-living-world-evidence}"
mkdir -p "$MET_EVIDENCE_DIR"
python3 "$MET_PROJECT_ROOT/tools/validate_assets.py" > "$MET_EVIDENCE_DIR/assets.log"
python3 -m unittest discover -s "$MET_PROJECT_ROOT/tests" -p test_world3d_assets.py > "$MET_EVIDENCE_DIR/assets-tests.log" 2>&1
"$GODOT" --headless --path "$MET_PROJECT_ROOT" --script res://tests/living_world.gd > "$MET_EVIDENCE_DIR/living-world-headless.log" 2>&1
if rg -n 'SCRIPT ERROR|ERROR:|Parse Error|Compile Error|^FAIL' "$MET_EVIDENCE_DIR/living-world-headless.log"; then exit 1; fi
if [[ "${MET_LIVING_NATIVE:-0}" == 1 ]]; then
  : "${DISPLAY:?A native 1920x1080 desktop display is required}"
  "$GODOT" --path "$MET_PROJECT_ROOT" --audio-driver Dummy --rendering-method forward_plus --rendering-driver vulkan --script res://tests/living_world_native.gd > "$MET_EVIDENCE_DIR/living-world-native.log" 2>&1
  if rg -n 'SCRIPT ERROR|ERROR:|Parse Error|Compile Error|^FAIL' "$MET_EVIDENCE_DIR/living-world-native.log"; then exit 1; fi
fi
if [[ "${MET_LIVING_PERFORMANCE:-0}" == 1 ]]; then
  "$GODOT" --path "$MET_PROJECT_ROOT" --audio-driver Dummy --rendering-method forward_plus --rendering-driver vulkan --script res://tests/living_world_performance.gd > "$MET_EVIDENCE_DIR/living-world-performance.log" 2>&1
  if rg -n 'SCRIPT ERROR|ERROR:|Parse Error|Compile Error|^FAIL' "$MET_EVIDENCE_DIR/living-world-performance.log"; then exit 1; fi
fi
