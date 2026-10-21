#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
UNITY_EDITOR=${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity}
if [[ -z "${UNITY_BURST_RUNTIME_PATH:-}" && -x "$ROOT/.toolcache/unity-burst-6000.6.5f1/Client/hostmac/llvm-lipo" ]]; then
  export UNITY_BURST_RUNTIME_PATH="$ROOT/.toolcache/unity-burst-6000.6.5f1/Client"
fi
MET_UNITY_RUN_DIR=${MET_UNITY_RUN_DIR:-"$ROOT/build/unity-city-evidence"}
export MET_UNITY_EVIDENCE="$MET_UNITY_RUN_DIR"
export MET_UNITY_SAVE_ROOT="$MET_UNITY_RUN_DIR/isolated-save"
export MET_UNITY_MAC_OUTPUT=${MET_UNITY_MAC_OUTPUT:-"$ROOT/build/MET Laisheng Unity.app"}
mkdir -p "$MET_UNITY_RUN_DIR"
unity_step() {
  local label=$1
  shift
  "$UNITY_EDITOR" -projectPath "$ROOT/unity/METLivingWorld" -buildTarget OSXUniversal -asset-garbage-collectors 1 \
    -logFile "$MET_UNITY_RUN_DIR/$label.log" "$@"
}
case "${1:-core}" in
  core)
    export GODOT=${GODOT:-"$ROOT/.toolcache/godot-4.7.2/Godot.app/Contents/MacOS/Godot"}
    export MET_CORE_PARITY_OUTPUT="$MET_UNITY_RUN_DIR/godot-parity.json"
    "$GODOT" --headless --path "$ROOT" --log-file "$MET_UNITY_RUN_DIR/godot.log" --script tools/export_unity_core_parity.gd
    export DOTNET_ROOT="$ROOT/.toolcache/dotnet"
    export DOTNET_CLI_HOME="$ROOT/.toolcache/dotnet-home"
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export NUGET_PACKAGES="$ROOT/.toolcache/nuget"
    "$DOTNET_ROOT/dotnet" run --project tools/unity_core/Core.Checks.csproj --no-restore -- \
      "$ROOT/content" "$MET_UNITY_RUN_DIR/core-records" "$MET_UNITY_RUN_DIR/godot-parity.json" \
      | tee "$MET_UNITY_RUN_DIR/core-checks.log"
    ;;
  city-core)
    export DOTNET_ROOT="$ROOT/.toolcache/dotnet"
    export DOTNET_CLI_HOME="$ROOT/.toolcache/dotnet-home"
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export NUGET_PACKAGES="$ROOT/.toolcache/nuget"
    for suite in CityGovernance CityNetwork; do
      "$DOTNET_ROOT/dotnet" run --project "tools/unity_core/$suite.Checks.csproj" --no-restore -- \
        "$ROOT/content" "$MET_UNITY_RUN_DIR" | tee "$MET_UNITY_RUN_DIR/$suite.log"
    done
    "$DOTNET_ROOT/dotnet" run --project tools/unity_core/CityArc.Checks.csproj --no-restore -- \
      "$ROOT/content" "$MET_UNITY_RUN_DIR/city-core-journey.json" | tee "$MET_UNITY_RUN_DIR/CityArc.log"
    ;;
  editor)
    python3 tools/sync_unity_content.py
    unity_step prepare -batchmode -nographics -executeMethod MET.City.Editor.CityProject.PrepareURP
    unity_step generate -batchmode -nographics -executeMethod MET.City.Editor.CityProject.GenerateBatch
    ;;
  mac)
    unity_step build -batchmode -nographics -executeMethod MET.City.Editor.CityProject.BuildMac
    python3 tools/sign_unity_mac.py "$MET_UNITY_MAC_OUTPUT"
    ;;
  native)
    executable=$(python3 - "$MET_UNITY_MAC_OUTPUT/Contents/Info.plist" <<'PY'
import plistlib,sys
with open(sys.argv[1],'rb') as f: print(plistlib.load(f)['CFBundleExecutable'])
PY
)
    "$MET_UNITY_MAC_OUTPUT/Contents/MacOS/$executable" --validate-city --capture-city \
      -screen-fullscreen 1 -screen-width 1920 -screen-height 1080 \
      -logFile "$MET_UNITY_RUN_DIR/player.log"
    ;;
  *) printf '%s\n' 'Usage: tools/test_unity_city.sh [core|city-core|editor|mac|native]' >&2; exit 2 ;;
esac
