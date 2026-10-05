#!/usr/bin/env bash
set -euo pipefail
: "${GODOT:?Set GODOT to the pinned Godot 4.7.2 executable}"
: "${DISPLAY:?Run this native suite on a 1920x1080 desktop display}"
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MET_UI_SUITE="${MET_UI_SUITE:-reset}"
if [[ "$MET_UI_SUITE" == desk ]]; then
  UI_SCRIPT=res://tests/desk_visuals.gd
  UI_PREFIX=desk
  UI_REPORT=desk-visuals.json
elif [[ "$MET_UI_SUITE" == impacts ]]; then
  UI_SCRIPT=res://tests/ui_impacts.gd
  UI_PREFIX=ui-impacts
  UI_REPORT=ui-impacts.json
else
  UI_SCRIPT=res://tests/ui_reset.gd
  UI_PREFIX=ui-reset
  UI_REPORT=ui-reset.json
fi
export MET_EVIDENCE_DIR="${MET_EVIDENCE_DIR:-/tmp/met-ui-reset-evidence}"
mkdir -p "$MET_EVIDENCE_DIR"
export MET_SOURCE_FINGERPRINT
MET_SOURCE_FINGERPRINT="$(python3 - "$PROJECT_ROOT" "$MET_EVIDENCE_DIR" "$UI_SCRIPT" "$UI_PREFIX" <<'PY'
import hashlib, json, pathlib, subprocess, sys, time
root, evidence = map(pathlib.Path, sys.argv[1:3])
script, prefix = sys.argv[3:5]
files = sorted(p for name in ('core', 'content', 'ui', 'assets') for p in (root / name).rglob('*') if p.is_file() and p.suffix not in ('.uid', '.import'))
files += [root / name for name in ('project.godot', 'main.tscn', 'tests/ui_reset.gd', 'tests/ui_impacts.gd', 'tests/beta_ui.gd', 'tests/desk_visuals.gd', 'tools/test_ui_reset.sh')]
members = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
fingerprint = hashlib.sha256(json.dumps(members, sort_keys=True).encode()).hexdigest()
try:
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True, stderr=subprocess.DEVNULL).strip()
    git_status = subprocess.check_output(['git', 'status', '--short'], cwd=root, text=True, stderr=subprocess.DEVNULL)
except subprocess.CalledProcessError:
    commit, git_status = 'unavailable', 'Source archive without Git metadata; file hashes remain authoritative.'
report = {'started_utc_epoch': time.time(), 'fingerprint': fingerprint, 'files': members,
          'commit': commit, 'git_status': git_status,
          'native_command': ['--path', str(root), '--rendering-method', 'gl_compatibility', '--audio-driver', 'Dummy', '--script', script, '--', '--fresh'],
          'evidence_type': 'native 1920x1080 pointer fixtures; not a human playtest'}
(evidence / (prefix + '-source-before.json')).write_text(json.dumps(report, indent=2), encoding='utf8')
print(fingerprint)
PY
)"
if [[ "${MET_UI_IMPORT:-1}" == 1 ]]; then
  "$GODOT" --headless --path "$PROJECT_ROOT" --editor --import --quit >"$MET_EVIDENCE_DIR/$UI_PREFIX-import.log" 2>&1
  if rg -n 'SCRIPT ERROR|Parse Error|Compile Error|^ERROR:' "$MET_EVIDENCE_DIR/$UI_PREFIX-import.log"; then exit 1; fi
fi
timeout 360 "$GODOT" --path "$PROJECT_ROOT" --rendering-method gl_compatibility --audio-driver Dummy --script "$UI_SCRIPT" -- --fresh >"$MET_EVIDENCE_DIR/$UI_PREFIX.log" 2>&1
if rg -n 'SCRIPT ERROR|Parse Error|Compile Error|^ERROR:|UI RESET FAIL|UI IMPACTS FAIL' "$MET_EVIDENCE_DIR/$UI_PREFIX.log"; then exit 1; fi
python3 - "$PROJECT_ROOT" "$MET_EVIDENCE_DIR" "$UI_PREFIX" "$UI_REPORT" <<'PY'
import hashlib, json, pathlib, sys, time
root, evidence = map(pathlib.Path, sys.argv[1:3])
prefix, report_name = sys.argv[3:5]
before = json.loads((evidence / (prefix + '-source-before.json')).read_text())
changed = [name for name, digest in before['files'].items() if not (root / name).is_file() or hashlib.sha256((root / name).read_bytes()).hexdigest() != digest]
current_files = {str(p.relative_to(root)) for name in ('core', 'content', 'ui', 'assets') for p in (root / name).rglob('*') if p.is_file() and p.suffix not in ('.uid', '.import')}
current_files.update(('project.godot', 'main.tscn', 'tests/ui_reset.gd', 'tests/ui_impacts.gd', 'tests/beta_ui.gd', 'tests/desk_visuals.gd', 'tools/test_ui_reset.sh'))
added = sorted(current_files - set(before['files']))
changed.extend(added)
after_members = {name: hashlib.sha256((root / name).read_bytes()).hexdigest() for name in sorted(current_files)}
(evidence / (prefix + '-source-after.json')).write_text(json.dumps({'files': after_members}, indent=2), encoding='utf8')
report = json.loads((evidence / report_name).read_text())
result = {'finished_utc_epoch': time.time(), 'source_unchanged': not changed, 'changed_files': changed,
          'fingerprint': before['fingerprint'], 'passed': report['passed'], 'failed': report['failed'],
          'captures': len(report['captures']), 'native_input_actions': len(report['input_actions'])}
(evidence / (prefix + '-capture-manifest.json')).write_text(json.dumps(report['captures'], indent=2), encoding='utf8')
(evidence / (prefix + '-verification.json')).write_text(json.dumps(result, indent=2), encoding='utf8')
print(json.dumps(result, ensure_ascii=False))
assert not changed, 'Sources changed during this evidence run'
assert report['failed'] == 0 and report['passed'] > 0, 'Native UI suite did not pass'
assert report['source_fingerprint'] == before['fingerprint'], 'Wrong evidence provenance'
PY
