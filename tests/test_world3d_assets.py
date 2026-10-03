#!/usr/bin/env python3
"""Small hostile-fixture audit for the Living World registry gate (stdlib only)."""
from __future__ import annotations

import contextlib
import copy
import hashlib
import importlib.util
import io
import json
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("met_asset_validator", Path(__file__).resolve().parents[1] / "tools/validate_assets.py")
validator_module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(validator_module)


def glb_document() -> dict:
    return {"asset": {"version": "2.0"}, "scene": 0, "scenes": [{"nodes": [0]}],
            "nodes": [{"mesh": 0}], "meshes": [{"primitives": [{"attributes": {"POSITION": 0}, "indices": 1, "material": 0}]}],
            "materials": [{"pbrMetallicRoughness": {"baseColorFactor": [0.4, 0.5, 0.3, 1], "metallicFactor": 0, "roughnessFactor": 0.8}}],
            "buffers": [{"byteLength": 44}], "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 36}, {"buffer": 0, "byteOffset": 36, "byteLength": 6}],
            "accessors": [{"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [-0.4, 0, -0.3], "max": [0.4, 0.8, 0.4]},
                          {"bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR"}]}


def write_glb(path: Path, document: dict) -> None:
    payload = json.dumps(document, separators=(",", ":")).encode()
    payload += b" " * (-len(payload) % 4)
    binary = struct.pack("<9f3H", -0.4, 0, -0.3, 0.4, 0, -0.3, 0, 0.8, 0.4, 0, 1, 2)
    binary += b"\0" * (-len(binary) % 4)
    path.write_bytes(struct.pack("<4sII", b"glTF", 2, 28 + len(payload) + len(binary))
                     + struct.pack("<I4s", len(payload), b"JSON") + payload
                     + struct.pack("<I4s", len(binary), b"BIN\0") + binary)


class RegistryAudit(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="met-asset-validator-")
        self.root = Path(self.temp.name)
        files = {"art_source/original/world3d/test.blend": b"BLENDER-v300test-fixture",
                 "docs/living_world/ASSET_PROVENANCE.md": b"Original test fixture only.",
                 "tools/make_world3d_assets.py": b"# test generator\n",
                 "tools/make_art040.py": b"# legacy test generator\n"}
        self.legacy = {"world_bounds": [-1, -1, 0, 1, 1, 1], "footprint_world_bounds": [-1, -1, 1, 1],
                       "connector_anchors": {"front": [0, -1]}}
        files["content/presentation040/art_catalog.json"] = json.dumps({"assets": {"building_test": self.legacy}}).encode()
        for name, payload in files.items():
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload)
        self.runtime = self.root / "assets/world3d/buildings/test.glb"
        self.runtime.parent.mkdir(parents=True)
        self.document = glb_document()
        write_glb(self.runtime, self.document)
        self.entry = {"id": "building.test.t1", "kind": "building", "runtime_path": "assets/world3d/buildings/test.glb",
                      "source_path": "art_source/original/world3d/test.blend", "source_pack": "MET original fixture",
                      "author": "MET", "source_url": "res://docs/living_world/ASSET_PROVENANCE.md",
                      "license": "LicenseRef-MET-Project-Original", "license_url": "res://docs/living_world/ASSET_PROVENANCE.md",
                      "download_date": "2026-10-03", "source_sha256": self.sha("art_source/original/world3d/test.blend"),
                      "runtime_sha256": self.sha("assets/world3d/buildings/test.glb"), "scale": [1, 1, 1], "pivot": [0, 0, 0],
                      "footprint": {"xz_bounds": [-0.4, -0.3, 0.4, 0.4]}, "bounds": {"min": [-0.4, 0, -0.3], "max": [0.4, 0.8, 0.4]},
                      "tags": ["fixture"], "material_profile": "lit-flat-pbr", "redistribution_policy": "project-source-and-runtime",
                      "attribution_required": False, "connector_anchors": {"front": [0, 0, -0.3]},
                      "generator": {"script": "tools/make_world3d_assets.py", "sha256": self.sha("tools/make_world3d_assets.py"),
                                    "dependencies": [{"path": "tools/make_art040.py", "sha256": self.sha("tools/make_art040.py")}],
                                    "blender_version": "fixture", "command": "fixture only"},
                      "legacy_binding": {"logical_asset_id": "building_test", "definition_id": "test",
                                         "catalog_path": "content/presentation040/art_catalog.json", **self.legacy}}

    def tearDown(self) -> None:
        self.temp.cleanup()

    def sha(self, name: str) -> str:
        return hashlib.sha256((self.root / name).read_bytes()).hexdigest()

    def audit(self, entry: dict | None = None) -> list[str]:
        validator = validator_module.Validator(self.root)
        validator.asset(self.entry if entry is None else entry, 0)
        return validator.errors

    def assert_glb_rejected(self, text: str) -> None:
        write_glb(self.runtime, self.document)
        self.entry["runtime_sha256"] = self.sha("assets/world3d/buildings/test.glb")
        self.assertTrue(any(text in error for error in self.audit()), self.audit())

    def test_valid_embedded_lit_original(self) -> None:
        validator = validator_module.Validator(self.root)
        validator.asset(self.entry, 0)
        self.assertEqual(validator.errors, [])
        self.assertEqual(validator.assets[0]["glb"], {"meshes": 1, "materials": 1, "nodes": 1, "triangles": 1})

    def test_hash_mismatch(self) -> None:
        self.entry["source_sha256"] = "0" * 64
        self.assertTrue(any("SHA-256 mismatch" in error for error in self.audit()))

    def test_traversal(self) -> None:
        self.entry["runtime_path"] = "assets/world3d/../../../outside.glb"
        self.assertTrue(any("traversal" in error for error in self.audit()))

    def test_symlink_payload(self) -> None:
        link = self.runtime.with_name("link.glb")
        link.symlink_to(self.runtime)
        self.entry["runtime_path"] = "assets/world3d/buildings/link.glb"
        self.assertTrue(any("symlink" in error for error in self.audit()))

    def test_vendor_reference(self) -> None:
        self.entry["source_path"] = "art_source/vendor_local/paid.blend"
        self.assertTrue(any("vendor raw" in error for error in self.audit()))

    def test_missing_provenance(self) -> None:
        self.entry["license_url"] = ""
        self.entry["source_pack"] = ""
        self.assertTrue(any("missing provenance" in error for error in self.audit()))
        self.assertTrue(any("missing source_pack" in error for error in self.audit()))

    def test_scale_and_nan_pivot(self) -> None:
        self.entry["scale"] = [1, 2, 1]
        self.entry["pivot"] = [0, float("nan"), 0]
        errors = self.audit()
        self.assertTrue(any("uniform" in error for error in errors))
        self.assertTrue(any("pivot" in error for error in errors))

    def test_footprint_exceeds_bounds(self) -> None:
        self.entry["footprint"]["xz_bounds"] = [-1, -1, 1, 1]
        self.assertTrue(any("footprint exceeds" in error for error in self.audit()))

    def test_legacy_metadata_drift(self) -> None:
        entry = copy.deepcopy(self.entry)
        entry["legacy_binding"]["world_bounds"][0] = -12
        self.assertTrue(any("no longer matches" in error for error in self.audit(entry)))

    def test_unapproved_commercial_license(self) -> None:
        self.entry["license"] = "Commercial use allowed"
        self.assertTrue(any("unapproved/non-CC0" in error for error in self.audit()))

    def test_unlit_material(self) -> None:
        self.document["materials"][0]["extensions"] = {"KHR_materials_unlit": {}}
        self.assert_glb_rejected("is unlit")

    def test_external_image(self) -> None:
        self.document["images"] = [{"uri": "private/albedo.png"}]
        self.assert_glb_rejected("external image URI")

    def test_external_buffer(self) -> None:
        self.document["buffers"][0]["uri"] = "private/payload.bin"
        self.assert_glb_rejected("external buffer URI")

    def test_embedded_light(self) -> None:
        self.document["extensions"] = {"KHR_lights_punctual": {"lights": [{"type": "directional"}]}}
        self.assert_glb_rejected("contains lights")

    def test_embedded_camera(self) -> None:
        self.document["cameras"] = [{"type": "perspective", "perspective": {"yfov": 0.5, "znear": 0.1}}]
        self.document["nodes"][0]["camera"] = 0
        self.assert_glb_rejected("contains camera")

    def test_truncated_glb(self) -> None:
        self.runtime.write_bytes(self.runtime.read_bytes()[:-4])
        self.entry["runtime_sha256"] = self.sha("assets/world3d/buildings/test.glb")
        self.assertTrue(any("declared length" in error for error in self.audit()))

    def test_duplicate_id_cli_exit(self) -> None:
        manifest = self.root / "manifest.json"
        manifest.write_text(json.dumps({"schema": validator_module.SCHEMA, "assets": [self.entry, self.entry]}))
        with patch.object(validator_module, "ROOT", self.root), patch.object(sys, "argv", ["validate_assets.py", "--manifest", str(manifest)]), contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(validator_module.main(), 1)
        self.assertIn("Asset IDs must be unique", output.getvalue())

    def test_tracked_vendor_file(self) -> None:
        vendor = self.root / "art_source/vendor_local/forbidden.blend"
        vendor.parent.mkdir(parents=True)
        vendor.write_bytes(b"private test payload")
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        subprocess.run(["git", "add", "art_source/vendor_local/forbidden.blend"], cwd=self.root, check=True)
        validator = validator_module.Validator(self.root)
        validator.distributable_sources()
        self.assertTrue(any("Tracked vendor raw files forbidden" in error for error in validator.errors))


if __name__ == "__main__":
    unittest.main()
