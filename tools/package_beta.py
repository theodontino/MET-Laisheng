#!/usr/bin/env python3
"""Package this Beta's actual runtime/source, with deterministic ZIP members.

Evidence and sample saves are separate explicit artifacts; never collect user://,
.git, imported caches, secret bindings, or historical evidence directories.
Run an independent extracted --editor --import and native/headless launch before
calling an archive verified. This script checks content/integrity, not gameplay.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ARCHIVE_ROOT = 'MET_Laisheng_Beta'
ROOT_FILES = ('.gitignore', 'AGENTS.md', 'README.md', 'project.godot', 'main.tscn', 'export_presets.cfg')
SOURCE_DIRS = ('core', 'ui', 'content', 'assets', 'tests', 'tools', 'docs', 'art_source')
EXCLUDE = {'.git', '.godot', '.agents', '.codex', '__pycache__', 'node_modules', 'build', 'archive', 'vendor_local', 'user_data', 'user_saves', 'player_saves', 'save_data', '__MACOSX'}

def selected() -> list[Path]:
    paths = [Path(name) for name in ROOT_FILES if (ROOT / name).is_file()]
    for directory in SOURCE_DIRS:
        for parent, dirs, files in os.walk(ROOT / directory, followlinks=False):
            dirs[:] = sorted(d for d in dirs if d not in EXCLUDE and not d.startswith('.') and not d.startswith('evidence') and not (Path(parent) / d).is_symlink())
            for name in sorted(files):
                p = Path(parent) / name
                if p.is_symlink() or name.lower() in {'.ds_store', 'thumbs.db'} or name.startswith('._'):
                    continue
                if p.suffix.lower() in {'.sav', '.save', '.savegame', '.bak'}:
                    continue
                if name.startswith('.') and name != '.gdignore':
                    continue
                paths.append(p.relative_to(ROOT))
    return sorted(set(paths), key=lambda p: p.as_posix())

def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--output', type=Path, default=ROOT / 'build' / 'MET_Laisheng_Beta_Source.zip')
    args = ap.parse_args()
    members = selected()
    required = {Path('project.godot'), Path('main.tscn'), Path('core/game.gd'), Path('ui/app.gd'), Path('content/beta/parameters.json')}
    missing = required - set(members)
    if missing:
        raise SystemExit(f'Missing actual runtime members: {sorted(map(str, missing))}')
    try:
        commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        dirty = bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True))
    except subprocess.CalledProcessError:
        commit, dirty = 'unavailable', True
    manifest = {'archive': ARCHIVE_ROOT, 'source_commit': commit, 'includes_uncommitted_changes': dirty,
                'verification': 'Member hashes and ZIP CRC; launch evidence is separate.',
                'files': {p.as_posix(): hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in members}}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temp = args.output.with_suffix('.tmp')
    with zipfile.ZipFile(temp, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for p in members:
            info = zipfile.ZipInfo(f'{ARCHIVE_ROOT}/{p.as_posix()}', date_time=(2026, 10, 3, 0, 0, 0))
            info.external_attr = (0o100644 & 0xffff) << 16
            if p.suffix in {'.sh', '.py'}:
                info.external_attr = (0o100755 & 0xffff) << 16
            archive.writestr(info, (ROOT / p).read_bytes(), compress_type=zipfile.ZIP_DEFLATED)
        info = zipfile.ZipInfo(f'{ARCHIVE_ROOT}/SOURCE_MANIFEST.json', date_time=(2026, 10, 3, 0, 0, 0))
        info.external_attr = (0o100644 & 0xffff) << 16
        archive.writestr(info, json.dumps(manifest, ensure_ascii=False, indent=2), compress_type=zipfile.ZIP_DEFLATED)
    with zipfile.ZipFile(temp) as archive:
        bad = archive.testzip()
        if bad:
            raise SystemExit(f'CRC failure: {bad}')
        for path, expected in manifest['files'].items():
            if hashlib.sha256(archive.read(f'{ARCHIVE_ROOT}/{path}')).hexdigest() != expected:
                raise SystemExit(f'Hash mismatch: {path}')
    temp.replace(args.output)
    checksum = hashlib.sha256(args.output.read_bytes()).hexdigest()
    args.output.with_suffix('.zip.sha256').write_text(f'{checksum}  {args.output.name}\n')
    print(json.dumps({'output': str(args.output), 'files': len(members), 'bytes': args.output.stat().st_size, 'sha256': checksum}))

if __name__ == '__main__':
    main()
