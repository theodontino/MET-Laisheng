#!/usr/bin/env python3
"""Validate the distributable Living World registry without Godot or Blender.

Paths, provenance and hashes are checked against this checkout. GLB checks
reject unlit materials, external payloads and embedded cameras/lights;
runtime lighting belongs to the Environment Controller. This is a registry
gate, not a visual-quality or legal-license interpretation test.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import math
import re
import struct
import subprocess
from pathlib import Path, PurePosixPath
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = "met.presentation3d.v1"
SHA256 = re.compile(r"[0-9a-f]{64}\Z")
ASSET_ID = re.compile(r"[a-z][a-z0-9]*(?:[._-][a-z0-9]+)+\Z")


def finite_number(value: Any) -> bool:
    try:
        return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
    except OverflowError:
        return False


class Validator:
    def __init__(self, root: Path) -> None:
        self.root = root.resolve()
        self.errors: list[str] = []
        self.notes: list[str] = []
        self.assets: list[dict[str, Any]] = []

    def require(self, condition: bool, label: str) -> bool:
        if not condition:
            self.errors.append(label)
        return condition

    def path(self, value: Any, label: str, prefix: str | None = None) -> Path | None:
        if not self.require(isinstance(value, str) and bool(value.strip()), f"{label}: missing path"):
            return None
        raw = value.removeprefix("res://")
        posix = PurePosixPath(raw)
        if not self.require(not posix.is_absolute() and ".." not in posix.parts and "\\" not in raw,
                            f"{label}: absolute/traversal/nonportable path"):
            return None
        if not self.require("vendor_local" not in posix.parts, f"{label}: vendor raw assets cannot enter this registry"):
            return None
        if prefix is not None and not self.require(raw.startswith(prefix + "/"), f"{label}: must be inside {prefix}/"):
            return None
        candidate = self.root / raw
        # Forbid symlinks even if they currently point inside the root: a source
        # package must retain exactly the validated, traceable file mapping.
        chain = [candidate, *candidate.parents]
        if not self.require(not any(p.is_symlink() for p in chain if p != self.root.parent), f"{label}: symlink path"):
            return None
        try:
            candidate.resolve().relative_to(self.root)
        except ValueError:
            self.errors.append(f"{label}: resolves outside checkout")
            return None
        if not self.require(candidate.is_file(), f"{label}: file does not exist: {raw}"):
            return None
        return candidate

    def hashed_path(self, value: Any, expected: Any, label: str, prefix: str | None = None) -> Path | None:
        path = self.path(value, label, prefix)
        valid_hash = isinstance(expected, str) and bool(SHA256.fullmatch(expected))
        self.require(valid_hash, f"{label}: invalid SHA-256")
        if path is not None and valid_hash:
            self.require(hashlib.sha256(path.read_bytes()).hexdigest() == expected, f"{label}: SHA-256 mismatch")
        return path

    def vector(self, value: Any, length: int, label: str, positive: bool = False) -> list[float] | None:
        valid = isinstance(value, list) and len(value) == length
        if valid:
            valid = all(finite_number(n) and (not positive or n > 0) for n in value)
        if not self.require(valid, f"{label}: expected {length} finite {'positive ' if positive else ''}numbers"):
            return None
        return [float(n) for n in value]

    def link(self, value: Any, label: str) -> None:
        if not self.require(isinstance(value, str) and bool(value.strip()), f"{label}: missing provenance link"):
            return
        if value.startswith("res://"):
            self.path(value.split("#", 1)[0], label)
        else:
            self.require(value.startswith(("https://", "http://")), f"{label}: expected repository or HTTP(S) URL")

    def glb(self, path: Path, label: str) -> dict[str, int]:
        stats = {"meshes": 0, "materials": 0, "nodes": 0, "triangles": 0}
        try:
            data = path.read_bytes()
            if len(data) < 20:
                raise ValueError("truncated GLB")
            magic, version, length = struct.unpack_from("<4sII", data)
            if magic != b"glTF" or version != 2 or length != len(data):
                raise ValueError("invalid GLB magic/version/declared length")
            chunks: list[tuple[bytes, bytes]] = []
            offset = 12
            while offset < len(data):
                if offset + 8 > len(data):
                    raise ValueError("truncated chunk header")
                size, kind = struct.unpack_from("<I4s", data, offset)
                offset += 8
                if size % 4 != 0 or offset + size > len(data):
                    raise ValueError("invalid chunk alignment/size")
                chunks.append((kind, data[offset:offset + size]))
                offset += size
            if not chunks or chunks[0][0] != b"JSON" or len(chunks) > 2 or (len(chunks) == 2 and chunks[1][0] != b"BIN\0"):
                raise ValueError("expected JSON then optional embedded BIN chunk")
            gltf = json.loads(chunks[0][1].decode("utf-8"))
            if not isinstance(gltf, dict) or gltf.get("asset", {}).get("version") != "2.0":
                raise ValueError("invalid glTF 2.0 JSON")
            self.require(bool(gltf.get("meshes")) and bool(gltf.get("scenes")), f"{label}: no mesh/scene")
            self.require(not gltf.get("cameras"), f"{label}: runtime GLB contains cameras")
            self.require("KHR_lights_punctual" not in gltf.get("extensions", {}), f"{label}: runtime GLB contains lights")
            self.require(not any("KHR_lights_punctual" in n.get("extensions", {}) for n in gltf.get("nodes", [])),
                         f"{label}: runtime GLB node contains lights")
            self.require(not any("camera" in n for n in gltf.get("nodes", [])), f"{label}: runtime GLB node contains camera")
            self.require(not any("uri" in b for b in gltf.get("buffers", [])), f"{label}: external buffer URI")
            self.require(not any("uri" in img for img in gltf.get("images", [])), f"{label}: external image URI")
            self.require("KHR_materials_unlit" not in gltf.get("extensionsUsed", [])
                         and "KHR_materials_unlit" not in gltf.get("extensionsRequired", []), f"{label}: unlit extension declared")
            for index, material in enumerate(gltf.get("materials", [])):
                self.require("KHR_materials_unlit" not in material.get("extensions", {}), f"{label}: material {index} is unlit")
                pbr = material.get("pbrMetallicRoughness")
                self.require(isinstance(pbr, dict), f"{label}: material {index} lacks PBR profile")
                if isinstance(pbr, dict):
                    for field in ("metallicFactor", "roughnessFactor"):
                        number = pbr.get(field, 1.0)
                        self.require(finite_number(number) and 0 <= number <= 1,
                                     f"{label}: material {index} invalid {field}")
                    if "baseColorFactor" in pbr:
                        color = self.vector(pbr["baseColorFactor"], 4, f"{label}: material {index} baseColorFactor")
                        self.require(color is not None and all(0 <= n <= 1 for n in color), f"{label}: baseColorFactor out of range")
            stats.update(meshes=len(gltf.get("meshes", [])), materials=len(gltf.get("materials", [])), nodes=len(gltf.get("nodes", [])))
            accessors = gltf.get("accessors", [])
            for mesh in gltf.get("meshes", []):
                for primitive in mesh.get("primitives", []):
                    self.require(primitive.get("mode", 4) == 4, f"{label}: only triangle primitives accepted")
                    accessor_index = primitive.get("indices", primitive.get("attributes", {}).get("POSITION"))
                    if isinstance(accessor_index, int) and 0 <= accessor_index < len(accessors):
                        count = accessors[accessor_index].get("count", 0)
                        if isinstance(count, int):
                            stats["triangles"] += count // 3
            return stats
        except (OSError, ValueError, TypeError, KeyError, AttributeError, struct.error) as error:
            self.errors.append(f"{label}: {error}")
            return stats

    def asset(self, entry: Any, index: int) -> None:
        if not self.require(isinstance(entry, dict), f"asset {index}: expected object"):
            return
        asset_id = entry.get("id")
        label = str(asset_id or f"asset {index}")
        self.require(isinstance(asset_id, str) and bool(ASSET_ID.fullmatch(asset_id)), f"{label}: invalid logical ID")
        kind = entry.get("kind")
        self.require(kind in {"building", "tree", "rock", "foliage"}, f"{label}: unsupported kind")
        for field in ("source_pack", "author", "license", "redistribution_policy", "material_profile"):
            self.require(isinstance(entry.get(field), str) and bool(entry[field].strip()), f"{label}: missing {field}")
        self.link(entry.get("source_url"), f"{label} source_url")
        self.link(entry.get("license_url"), f"{label} license_url")
        try:
            dt.date.fromisoformat(entry.get("download_date", ""))
        except (TypeError, ValueError):
            self.errors.append(f"{label}: invalid download_date (YYYY-MM-DD required)")
        self.require(isinstance(entry.get("attribution_required"), bool), f"{label}: attribution_required must be boolean")
        tags = entry.get("tags")
        self.require(isinstance(tags, list) and bool(tags) and all(isinstance(t, str) and bool(t.strip()) for t in tags), f"{label}: nonempty tags required")
        scale = self.vector(entry.get("scale"), 3, f"{label} scale", positive=True)
        if scale is not None:
            self.require(max(scale) - min(scale) <= 1e-8 * max(scale), f"{label}: runtime scale must be uniform")
        self.vector(entry.get("pivot"), 3, f"{label} pivot")
        bounds = entry.get("bounds", {})
        self.require(isinstance(bounds, dict), f"{label}: bounds must be object")
        low = self.vector(bounds.get("min") if isinstance(bounds, dict) else None, 3, f"{label} bounds.min")
        high = self.vector(bounds.get("max") if isinstance(bounds, dict) else None, 3, f"{label} bounds.max")
        if low is not None and high is not None:
            self.require(all(a < b for a, b in zip(low, high)), f"{label}: bounds must have positive extent on each axis")
        footprint = entry.get("footprint", {})
        self.require(isinstance(footprint, dict), f"{label}: footprint must be object")
        fp = self.vector(footprint.get("xz_bounds") if isinstance(footprint, dict) else None, 4, f"{label} footprint.xz_bounds")
        if fp is not None:
            self.require(fp[0] < fp[2] and fp[1] < fp[3], f"{label}: footprint must have positive extent")
            if low is not None and high is not None:
                self.require(low[0] - 1e-5 <= fp[0] < fp[2] <= high[0] + 1e-5
                             and low[2] - 1e-5 <= fp[1] < fp[3] <= high[2] + 1e-5,
                             f"{label}: footprint exceeds mesh bounds")
        original = entry.get("license") == "LicenseRef-MET-Project-Original"
        if original:
            self.require(entry.get("redistribution_policy") == "project-source-and-runtime", f"{label}: original redistribution policy mismatch")
            self.require(entry.get("attribution_required") is False, f"{label}: project-original cannot invent third-party attribution")
        else:
            # V1 admits only a separately approved, explicitly registered CC0
            # test import. A 'commercial use' string alone is never sufficient.
            self.require(entry.get("license") == "CC0-1.0", f"{label}: unapproved/non-CC0 third-party license")
            self.require(entry.get("redistribution_policy") == "cc0-source-and-runtime", f"{label}: CC0 source redistribution not explicit")
            self.require(entry.get("approved_for_import") is True, f"{label}: third-party import approval missing")
        source = self.hashed_path(entry.get("source_path"), entry.get("source_sha256"), f"{label} source",
                                  "art_source/original" if original else "art_source/third_party_cc0")
        if source is not None and original:
            self.require(source.suffix == ".blend", f"{label}: generated original source must be .blend")
        runtime = self.hashed_path(entry.get("runtime_path"), entry.get("runtime_sha256"), f"{label} runtime", "assets/world3d")
        if runtime is not None:
            self.require(runtime.suffix == ".glb", f"{label}: runtime must be embedded GLB")
        generator = entry.get("generator")
        self.require(isinstance(generator, dict), f"{label}: generator provenance missing")
        if isinstance(generator, dict):
            self.hashed_path(generator.get("script"), generator.get("sha256"), f"{label} generator")
            dependencies = generator.get("dependencies")
            self.require(isinstance(dependencies, list) and bool(dependencies), f"{label}: generator dependencies missing")
            if isinstance(dependencies, list):
                for dependency in dependencies:
                    if self.require(isinstance(dependency, dict), f"{label}: invalid dependency"):
                        self.hashed_path(dependency.get("path"), dependency.get("sha256"), f"{label} dependency")
            for field in ("blender_version", "command"):
                self.require(isinstance(generator.get(field), str) and bool(generator[field].strip()), f"{label}: generator {field} missing")
        if kind == "building":
            legacy = entry.get("legacy_binding")
            self.require(isinstance(legacy, dict), f"{label}: legacy logical binding missing")
            if isinstance(legacy, dict):
                catalog = self.path(legacy.get("catalog_path"), f"{label} legacy catalog")
                legacy_id = legacy.get("logical_asset_id")
                self.require(isinstance(legacy_id, str) and bool(legacy_id), f"{label}: missing legacy logical ID")
                self.require(isinstance(legacy.get("definition_id"), str) and bool(legacy.get("definition_id")), f"{label}: missing gameplay definition binding")
                if catalog is not None:
                    try:
                        legacy_entry = json.loads(catalog.read_text())["assets"][legacy_id]
                        for field in ("footprint_world_bounds", "world_bounds", "connector_anchors"):
                            self.require(legacy.get(field) == legacy_entry.get(field) and field in legacy_entry,
                                         f"{label}: legacy {field} no longer matches preserved catalog")
                    except (OSError, ValueError, KeyError, TypeError):
                        self.errors.append(f"{label}: legacy ID/catalog mapping invalid")
            anchors = entry.get("connector_anchors")
            self.require(isinstance(anchors, dict) and bool(anchors), f"{label}: runtime connector anchors missing")
            if isinstance(anchors, dict):
                for name, vector in anchors.items():
                    self.vector(vector, 3, f"{label} connector {name}")
        self.assets.append({"id": asset_id, "kind": kind, "runtime_path": entry.get("runtime_path"),
                            "source_path": entry.get("source_path"), "glb": self.glb(runtime, label) if runtime else {}})

    def distributable_sources(self) -> None:
        try:
            git_root = Path(subprocess.check_output(["git", "rev-parse", "--show-toplevel"], cwd=self.root,
                                                   text=True, stderr=subprocess.DEVNULL).strip()).resolve()
            if git_root != self.root:
                self.notes.append("No repository metadata at registry root; tracked-vendor audit skipped for extracted source.")
                return
            tracked = subprocess.check_output(["git", "ls-files", "-z"], cwd=self.root).decode().split("\0")
            forbidden = [p for p in tracked if "vendor_local" in PurePosixPath(p).parts]
            self.require(not forbidden, f"Tracked vendor raw files forbidden: {forbidden}")
        except (FileNotFoundError, subprocess.CalledProcessError):
            self.notes.append("Git unavailable; registry path/provenance checks ran, tracked-vendor audit unavailable.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=ROOT / "content/presentation3d/asset_manifest.json")
    parser.add_argument("--expected-count", type=int, help="Optional delivery-specific asset count gate.")
    parser.add_argument("--json", type=Path, help="Write the complete validation report here.")
    args = parser.parse_args()
    validator = Validator(ROOT)
    try:
        manifest_path = args.manifest.resolve()
        manifest = json.loads(manifest_path.read_text())
        if not isinstance(manifest, dict):
            raise ValueError("manifest must be an object")
        validator.require(manifest.get("schema") == SCHEMA, f"Expected schema {SCHEMA}")
        entries = manifest.get("assets")
        if validator.require(isinstance(entries, list) and bool(entries), "assets must be a nonempty array"):
            if args.expected_count is not None:
                validator.require(len(entries) == args.expected_count, f"Expected {args.expected_count} assets, got {len(entries)}")
            ids = [entry.get("id") for entry in entries if isinstance(entry, dict)]
            validator.require(all(isinstance(asset_id, str) for asset_id in ids) and len(ids) == len(set(str(x) for x in ids)), "Asset IDs must be unique strings")
            for index, entry in enumerate(entries):
                validator.asset(entry, index)
        validator.distributable_sources()
    except (OSError, ValueError, TypeError) as error:
        validator.errors.append(f"Manifest: {error}")
    report = {"schema": "met.presentation3d.validation.v1", "manifest": str(args.manifest),
              "passed": not validator.errors, "asset_count": len(validator.assets),
              "errors": validator.errors, "notes": validator.notes, "assets": validator.assets,
              "scope": "Registry integrity, source traceability, source redistribution gate and embedded lit GLB structure; not visual quality or a legal opinion."}
    output = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(output)
    print(output, end="")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
