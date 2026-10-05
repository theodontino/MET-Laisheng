#!/usr/bin/env bash
# Serial entry points. A missing licensed Editor is a failure, never a skipped pass.
set -euo pipefail
MET_REPO_ROOT=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$MET_REPO_ROOT"
MET_UNITY_PROJECT="$MET_REPO_ROOT/unity/METLivingWorld"
MET_UNITY_MODE=${1:-portable}
MET_UNITY_EVIDENCE_ROOT=${MET_EVIDENCE_DIR:-/tmp/met-unity-evidence}
mkdir -p "$MET_UNITY_EVIDENCE_ROOT"
MET_UNITY_RUN_DIR=$(mktemp -d "$MET_UNITY_EVIDENCE_ROOT/run.XXXXXX")
MET_UNITY_RUN_DIR=$(cd "$MET_UNITY_RUN_DIR" && pwd)

unity_step() {
  local step_name=$1
  shift
  "$UNITY_EDITOR" -projectPath "$MET_UNITY_PROJECT" -logFile "$MET_UNITY_RUN_DIR/$step_name.log" "$@" --evidence-dir "$MET_UNITY_RUN_DIR"
  if rg -n 'error CS[0-9]+|Aborting batchmode|Failed to activate|No valid Unity Editor license|License.*[Ee]rror' "$MET_UNITY_RUN_DIR/$step_name.log"; then
    return 1
  fi
}

case "$MET_UNITY_MODE" in
  portable)
    python3 tools/validate_unity_port.py --json "$MET_UNITY_RUN_DIR/portable-validation.json"
    ;;
  export)
    : "${GODOT:?Set GODOT to the checksum-verified Godot 4.7.2 executable}"
    export MET_UNITY_EXPORT_TEST_DIR="$MET_UNITY_RUN_DIR/export-test"
    "$GODOT" --headless --path . --audio-driver Dummy --script res://tests/unity_hero_export.gd > "$MET_UNITY_RUN_DIR/export-test.log" 2>&1
    if rg -n 'SCRIPT ERROR|ERROR:|Parse Error|Compile Error' "$MET_UNITY_RUN_DIR/export-test.log"; then exit 1; fi
    python3 tools/validate_unity_port.py --json "$MET_UNITY_RUN_DIR/portable-validation.json"
    ;;
  editor|play-validation|native|mac)
    : "${UNITY_EDITOR:?An installed and lawfully activated Unity 6000.0.58f2 Editor is required}"
    if [ ! -x "$UNITY_EDITOR" ]; then
      printf '%s\n' 'UNITY_EDITOR is not an executable Editor; source checks are not Unity execution.' >&2
      exit 2
    fi
    # Select the target on process startup; batchmode cannot switch targets later.
    MET_UNITY_TARGET_ARGS=()
    if [ "$MET_UNITY_MODE" = mac ]; then MET_UNITY_TARGET_ARGS=(-buildTarget OSXUniversal); fi
    # Restart between configuration and import. glTFast caches its pipeline per domain.
    unity_step prepare "${MET_UNITY_TARGET_ARGS[@]}" -batchmode -nographics -executeMethod MET.LivingWorld.Editor.HeroProject.PrepareURP
    case "$MET_UNITY_MODE" in
      editor)
        unity_step import-validation -batchmode -nographics -executeMethod MET.LivingWorld.Editor.HeroProject.ValidateBatch
        ;;
      play-validation)
        unity_step play-validation -batchmode -nographics -executeMethod MET.LivingWorld.Editor.HeroProject.RunHeroValidation --validate-hero --evidence-dir "$MET_UNITY_RUN_DIR"
        ;;
      native)
        unity_step native -executeMethod MET.LivingWorld.Editor.HeroProject.RunHeroValidation --capture-hero --measure-hero --evidence-dir "$MET_UNITY_RUN_DIR"
        ;;
      mac)
        unity_step mac-build "${MET_UNITY_TARGET_ARGS[@]}" -batchmode -nographics -executeMethod MET.LivingWorld.Editor.HeroProject.BuildHeroMac
        ;;
    esac
    ;;
  *)
    printf '%s\n' 'Usage: tools/test_unity_port.sh [portable|export|editor|play-validation|native|mac]' >&2
    exit 2
    ;;
esac
