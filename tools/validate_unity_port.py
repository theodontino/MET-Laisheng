#!/usr/bin/env python3
"""Validate the portable fixture bytes; this does not run Unity or certify a Player."""
from __future__ import annotations
import argparse
import hashlib
import json
import math
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMPONENT = {5121: ('B', 1), 5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}
WIDTH = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def glb_geometry(path: Path) -> dict:
    raw = path.read_bytes()
    require(len(raw) >= 28, f'{path.name}: truncated GLB')
    magic, version, length = struct.unpack_from('<4sII', raw)
    require(magic == b'glTF' and version == 2 and length == len(raw), f'{path.name}: GLB header')
    chunks = []
    offset = 12
    while offset < len(raw):
        size, kind = struct.unpack_from('<II', raw, offset)
        require(size % 4 == 0 and offset + 8 + size <= len(raw), f'{path.name}: chunk bounds')
        chunks.append((kind, raw[offset + 8:offset + 8 + size]))
        offset += 8 + size
    require(len(chunks) == 2 and chunks[0][0] == 0x4e4f534a and chunks[1][0] == 0x004e4942,
            f'{path.name}: expected self-contained JSON/BIN')
    data = json.loads(chunks[0][1])
    binary = chunks[1][1]
    require(len(data['buffers']) == 1 and 'uri' not in data['buffers'][0], f'{path.name}: external buffer')
    require(not data.get('images'), f'{path.name}: unexpected texture dependency')

    def read_accessor(index: int) -> list:
        accessor = data['accessors'][index]
        require('sparse' not in accessor, f'{path.name}: unexpected sparse accessor')
        view = data['bufferViews'][accessor['bufferView']]
        require(view.get('buffer', 0) == 0, f'{path.name}: external view')
        code, size = COMPONENT[accessor['componentType']]
        width = WIDTH[accessor['type']]
        packed = size * width
        stride = view.get('byteStride', packed)
        begin = view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
        end = begin + (accessor['count'] - 1) * stride + packed
        require(accessor['count'] > 0 and stride >= packed and end <= len(binary) and
                end <= view.get('byteOffset', 0) + view['byteLength'], f'{path.name}: accessor bounds')
        values = [struct.unpack_from('<' + code * width, binary, begin + n * stride)
                  for n in range(accessor['count'])]
        require(all(math.isfinite(v) for row in values for v in row), f'{path.name}: non-finite geometry')
        return values

    triangles = surfaces = color_surfaces = 0
    for mesh in data['meshes']:
        for primitive in mesh['primitives']:
            require(primitive.get('mode', 4) == 4, f'{path.name}: non-triangle primitive')
            positions = read_accessor(primitive['attributes']['POSITION'])
            normals = read_accessor(primitive['attributes']['NORMAL'])
            indices = read_accessor(primitive['indices'])
            require(len(normals) == len(positions) and len(indices) % 3 == 0, f'{path.name}: geometry cardinality')
            require(all(len(row) == 1 and row[0] < len(positions) for row in indices), f'{path.name}: index range')
            require(all(.8 < sum(v * v for v in row) < 1.2 for row in normals), f'{path.name}: normals')
            material = data['materials'][primitive['material']]
            require('pbrMetallicRoughness' in material, f'{path.name}: PBR material missing')
            require('KHR_materials_unlit' not in material.get('extensions', {}), f'{path.name}: baked/unlit material')
            if 'COLOR_0' in primitive['attributes']:
                require(len(read_accessor(primitive['attributes']['COLOR_0'])) == len(positions), f'{path.name}: vertex colors')
                color_surfaces += 1
            triangles += len(indices) // 3
            surfaces += 1
    return {'triangles': triangles, 'meshes': len(data['meshes']), 'surfaces': surfaces,
            'color_surfaces': color_surfaces, 'nodes': len(data['nodes'])}


def validate(project: Path, source_root: Path | None = ROOT) -> dict:
    hero = project / 'Assets/Resources/Hero'
    sidecar_path = hero / 'hero_valley.json'
    data = json.loads(sidecar_path.read_text())
    require(data['schema'] == 1 and data['source_phase'] == 'committed_tick' and data['forecast'] is False,
            'Only the committed Hero fixture is supported')
    require((len(data['tiles']), len(data['buildings']), len(data['foliage'])) == (37, 4, 29), 'Fixture cardinality')
    require(len({t['key'] for t in data['tiles']}) == 37, 'Duplicate tile')
    require(len({t['id'] for t in data['foliage']}) == 29, 'Duplicate foliage instance')
    require(data['coordinate_system']['handedness'] == 'right' and data['coordinate_system']['up'] == '+Y',
            'JSON must retain the original glTF coordinate convention')
    require(data['camera']['projection'] == 'orthographic' and data['camera']['vertical_span'] == 12,
            'Golden camera changed')
    require(data['albedo_conversion']['lighting_baked'] is False, 'Directional lighting must not be baked')
    proof = data['isolation_proof']
    require(proof['before'] == proof['after'] and proof['snapshot_before_sha256'] == proof['snapshot_after_sha256'],
            'Source isolation proof mismatch')
    require(proof['live_state_unchanged'] and not proof['gameplay_rng_used_by_presentation'] and
            not proof['player_save_read'] and not proof['player_save_written'], 'Presentation isolation')
    geometry = {}
    for key in ('world', 'foliage'):
        output = data['outputs'][key]
        require(output['path'] == key + '.glb', 'Unexpected output path')
        path = hero / output['path']
        require(digest(path) == output['sha256'], f'{key}: source/export hash mismatch')
        geometry[key] = glb_geometry(path)
    require(geometry['world']['triangles'] == data['model_stats']['triangles_world'] == 110408, 'World geometry count')
    require(geometry['world']['meshes'] == 21 and geometry['world']['color_surfaces'] >= 2, 'World color/topology')
    require(geometry['foliage']['triangles'] == data['model_stats']['triangles_per_tree'] == 1892 and
            geometry['foliage']['meshes'] == 6, 'Shared foliage geometry')
    require(len(data['grounding_samples']) == 80, 'Grounding evidence missing')
    for tree in data['foliage']:
        require(all(math.isfinite(n) and n > 0 for n in tree['scale']), 'Invalid instance scale')
        require(abs(sum(n*n for n in tree['rotation']) - 1) < .0001, 'Invalid instance quaternion')
    checked_sources = 0
    if source_root is not None:
        for name, sha in data['source_files_sha256'].items():
            path = source_root / name
            require(path.resolve().is_relative_to(source_root.resolve()) and path.is_file(), 'Source provenance missing: ' + name)
            require(digest(path) == sha, 'Stale source provenance: ' + name)
            checked_sources += 1
    manifest = json.loads((project / 'Packages/manifest.json').read_text())['dependencies']
    require(manifest['com.unity.cloud.gltfast'] == '6.20.0' and
            manifest['com.unity.nuget.newtonsoft-json'] == '3.2.1', 'Unreviewed importer/JSON version')
    editor_version = (project / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(':', 1)[1].strip()
    pair = (editor_version, manifest['com.unity.render-pipelines.universal'])
    require(pair in [('6000.0.58f2', '17.0.3'), ('6000.6.5f1', '17.6.0')], 'Unreviewed Editor/URP combination')
    return {'status': 'passed', 'editor_urp': pair, 'scope': 'Historical portable fixture geometry, provenance and declared pins; this check does not execute Unity or validate the full game.',
            'geometry': geometry, 'source_files_checked': checked_sources, 'sidecar_sha256': digest(sidecar_path)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=ROOT / 'unity/METLivingWorld')
    parser.add_argument('--portable-only', action='store_true', help='Skip MET checkout source-hash comparison after extracting the standalone project.')
    parser.add_argument('--json', type=Path)
    args = parser.parse_args()
    try:
        result = validate(args.project, None if args.portable_only else ROOT)
    except (ValueError, KeyError, IndexError, struct.error, OSError) as error:
        raise SystemExit('Unity fixture validation failed: ' + str(error))
    output = json.dumps(result, indent=2) + '\n'
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(output)
    print(output, end='')


if __name__ == '__main__':
    main()
