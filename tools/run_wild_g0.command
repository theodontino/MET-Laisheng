#!/bin/bash
set -euo pipefail

# Works both beside the delivered app and from this repository's tools folder.
SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
MET_G0_DIR="$SCRIPT_DIR"
if [[ ! -d "$MET_G0_DIR/MET Wild G0.app" ]]; then
  MET_G0_DIR=$(cd "$SCRIPT_DIR/../build/wild-g0" && pwd)
fi
MET_G0_SAVE="$MET_G0_DIR/G0-playtest"
MET_G0_FIXTURE="$MET_G0_DIR/initial/met_city_v1.json"
MET_G0_EXEC="$MET_G0_DIR/MET Wild G0.app/Contents/MacOS/MET 来生 Unity Beta"

if [[ ! -x "$MET_G0_EXEC" || ! -f "$MET_G0_FIXTURE" ]]; then
  printf '%s\n' '请先完整解压 G0 试用包，保留应用、initial 文件夹与此启动器。'
  exit 1
fi
mkdir -p "$MET_G0_SAVE"
if [[ ! -f "$MET_G0_SAVE/met_city_v1.json" ]]; then
  cp "$MET_G0_FIXTURE" "$MET_G0_SAVE/met_city_v1.json"
fi
if [[ ! -f "$MET_G0_SAVE/display_settings.json" ]]; then
  cat > "$MET_G0_SAVE/display_settings.json" <<'JSON'
{"toolkit_upgrade":true,"ui_scale":1.0,"text_scale":1.0,"frame_limit":60}
JSON
fi
printf '%s\n' 'G0 独立试玩：已启用 Toolkit 升级窗口。' '地图选农居 → 升级。试玩进度保存在包内 G0-playtest 文件夹。'
MET_UNITY_SAVE_ROOT="$MET_G0_SAVE" "$MET_G0_EXEC" \
  -screen-fullscreen 1 -screen-width 2560 -screen-height 1440 \
  -logFile "$MET_G0_SAVE/player.log"
