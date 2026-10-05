#!/usr/bin/env python3
"""Package only the Unity visual port and declared provenance, never editor/account caches."""
from __future__ import annotations
import argparse
import hashlib
import json
import subprocess
import zipfile
from pathlib import Path

from validate_unity_port import validate

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'unity/METLivingWorld'
ARCHIVE_ROOT = 'METLivingWorld_Unity'
ALLOWED_SUFFIXES = {'.cs', '.meta', '.json', '.glb', '.asset', '.txt', '.md', '.unity'}


def selected() -> dict[str, Path]:
    mapping: dict[str, Path] = {}
    for folder in ('Assets', 'Packages', 'ProjectSettings'):
        for path in sorted((PROJECT / folder).rglob('*')):
            if path.is_symlink():
                raise ValueError('Symlink is not distributable: ' + str(path))
            if not path.is_file():
                continue
            if path.suffix.lower() not in ALLOWED_SUFFIXES or path.name.startswith('.'):
                raise ValueError('Unexpected Unity source member: ' + str(path))
            mapping[path.relative_to(PROJECT).as_posix()] = path
    mapping['README.md'] = PROJECT / 'README.md'
    mapping['Tools/validate_unity_port.py'] = ROOT / 'tools/validate_unity_port.py'
    for path in sorted((ROOT / 'docs/unity_port').rglob('*')):
        if path.is_file() and not path.is_symlink():
            mapping['PortDocumentation/' + path.relative_to(ROOT / 'docs/unity_port').as_posix()] = path
    # Original asset/source rights are preserved. No third-party vendor raw packs.
    for folder in ('art_source/original/world3d', 'assets/world3d'):
        for path in sorted((ROOT / folder).rglob('*')):
            if path.is_file() and not path.is_symlink():
                if path.suffix.lower() not in {'.glb', '.blend', '.json', '.md', '.gdignore'}:
                    continue
                mapping['MET_PROVENANCE/' + path.relative_to(ROOT).as_posix()] = path
    for name in ('content/presentation3d/asset_manifest.json', 'docs/living_world/ASSET_PROVENANCE.md',
                 'assets/licenses/THIRD-PARTY-NOTICES.txt'):
        mapping['MET_PROVENANCE/' + name] = ROOT / name
    return mapping


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'build/MET_Unity_Hero_Valley_Source.zip')
    args = parser.parse_args()
    validation = validate(PROJECT)
    members = selected()
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    dirty = bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True).strip())
    manifest = {'source_commit': commit, 'includes_uncommitted_changes': dirty,
                'artifact_kind': 'Unity source project, not a macOS app',
                'verification': validation,
                'files': {name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in members.items()}}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix('.zip.tmp')
    with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name, path in sorted(members.items()):
            info = zipfile.ZipInfo(f'{ARCHIVE_ROOT}/{name}', (2026, 10, 5, 0, 0, 0))
            info.external_attr = 0o100644 << 16
            archive.writestr(info, path.read_bytes(), compress_type=zipfile.ZIP_DEFLATED)
        info = zipfile.ZipInfo(f'{ARCHIVE_ROOT}/SOURCE_MANIFEST.json', (2026, 10, 5, 0, 0, 0))
        info.external_attr = 0o100644 << 16
        archive.writestr(info, json.dumps(manifest, ensure_ascii=False, indent=2) + '\n',
                         compress_type=zipfile.ZIP_DEFLATED)
    with zipfile.ZipFile(temporary) as archive:
        if archive.testzip() is not None:
            raise ValueError('ZIP CRC failure')
        for name, sha in manifest['files'].items():
            if hashlib.sha256(archive.read(f'{ARCHIVE_ROOT}/{name}')).hexdigest() != sha:
                raise ValueError('ZIP source mismatch: ' + name)
    temporary.replace(args.output)
    sha = hashlib.sha256(args.output.read_bytes()).hexdigest()
    args.output.with_suffix('.zip.sha256').write_text(f'{sha}  {args.output.name}\n')
    print(json.dumps({'artifact_kind': manifest['artifact_kind'], 'source_commit': commit,
                      'includes_uncommitted_changes': dirty, 'files': len(members),
                      'bytes': args.output.stat().st_size, 'sha256': sha, 'output': str(args.output)}, indent=2))


if __name__ == '__main__':
    main()
