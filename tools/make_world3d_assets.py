#!/usr/bin/env python3
"""Export the original Hero Valley meshes, without rendering/replacing legacy PNGs.

Blender --background --python-exit-code 1 --python tools/make_art040.py -- --runtime3d
Blender --background --python-exit-code 1 --python tools/make_world3d_assets.py

The four building constructors are the existing MET 0.4 geometry. A fifth asset
is one native fern tree assembled with the same original botanical vocabulary.
Runtime geometry is merged per asset (one mesh, multiple PBR surfaces); source
.blend files preserve the editable components. No images, lights, cameras,
gameplay state, random generator, or downloaded assets enter these GLBs.
"""
from __future__ import annotations

import hashlib
import importlib.util
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "art_source/original/world3d"
RUNTIME_DIR = ROOT / "assets/world3d"
MANIFEST_PATH = ROOT / "content/presentation3d/asset_manifest.json"
LEGACY_CATALOG = ROOT / "content/presentation040/art_catalog.json"
SOURCE_SCALE = 0.055
TREE_MATERIAL_PROFILE = "met.valley_foliage_pbr.v1"
TREE_RUNTIME_PALETTE = {
    "green": "3F654B", "green_dark": "2E4F3B", "leaf": "3F6848",
    "leaf_light": "708D56", "leaf_dark": "2E5137",
}
HERO_BUILDINGS = ("homestead", "neighborhood", "workshop", "residence")
BUILDING_IDS = {
    "homestead": "building.homestead.t1",
    "neighborhood": "building.neighborhood.t1",
    "workshop": "building.workshop.t1",
    "residence": "building.residence.t2",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_legacy():
    import bpy
    spec = importlib.util.spec_from_file_location("met_art040_geometry", ROOT / "tools/make_art040.py")
    legacy = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(legacy)
    legacy.bpy = bpy
    return legacy


def _runtime_point(point) -> list[float]:
    """Blender Z-up to Godot/glTF Y-up; scale is baked, root transform is identity."""
    return [round(point[0] * SOURCE_SCALE, 7), round(point[2] * SOURCE_SCALE, 7),
            round(-point[1] * SOURCE_SCALE, 7)]


def _merge_for_runtime(asset_id: str, material_profile: str):
    """Evaluate original bevels/normals, bake component transforms and join surfaces."""
    import bpy
    from mathutils import Matrix, Vector

    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    components = sorted((o for o in bpy.context.scene.objects if o.type == "MESH"), key=lambda o: o.name)
    vertices, faces, face_materials, face_smooth, loop_normals = [], [], [], [], []
    materials, material_indices = [], {}
    scale = Matrix.Scale(SOURCE_SCALE, 4)
    for obj in components:
        evaluated = obj.evaluated_get(depsgraph)
        component = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
        matrix = scale @ evaluated.matrix_world
        normal_matrix = matrix.to_3x3().inverted().transposed()
        offset = len(vertices)
        vertices.extend(matrix @ v.co for v in component.vertices)
        local_materials = []
        for material in component.materials:
            token = material.name
            if token not in material_indices:
                material_indices[token] = len(materials)
                materials.append(material)
            local_materials.append(material_indices[token])
        for polygon in component.polygons:
            faces.append(tuple(offset + i for i in polygon.vertices))
            face_materials.append(local_materials[polygon.material_index] if local_materials else 0)
            face_smooth.append(polygon.use_smooth)
            for loop_index in polygon.loop_indices:
                normal = normal_matrix @ component.corner_normals[loop_index].vector
                normal.normalize()
                loop_normals.append(normal)
        bpy.data.meshes.remove(component)
    if not vertices or not faces:
        raise RuntimeError(f"Empty original geometry: {asset_id}")
    data = bpy.data.meshes.new(asset_id + ".mesh")
    data.from_pydata(vertices, [], faces)
    for material in materials:
        data.materials.append(material)
    data.update()
    for polygon, material_index, smooth in zip(data.polygons, face_materials, face_smooth):
        polygon.material_index = material_index
        polygon.use_smooth = smooth
    data.normals_split_custom_set(loop_normals)
    runtime = bpy.data.objects.new(asset_id, data)
    bpy.context.collection.objects.link(runtime)
    bpy.ops.object.select_all(action="DESELECT")
    runtime.select_set(True)
    bpy.context.view_layer.objects.active = runtime
    runtime["logical_asset_id"] = asset_id
    runtime["material_profile"] = material_profile
    points = [_runtime_point(v / SOURCE_SCALE) for v in vertices]
    bounds = {"min": [min(v[i] for v in points) for i in range(3)],
              "max": [max(v[i] for v in points) for i in range(3)]}
    triangle_count = sum(max(0, len(face) - 2) for face in faces)
    return runtime, bounds, {"source_components": len(components), "vertices": len(vertices),
                             "triangles": triangle_count, "material_surfaces": len(materials)}


def _generator_metadata() -> dict:
    import bpy
    return {
        "script": "tools/make_world3d_assets.py", "sha256": sha256(Path(__file__)),
        "dependencies": [{"path": p, "sha256": sha256(ROOT / p)} for p in
                         ("tools/make_art040.py", "content/presentation040/art_catalog.json")],
        "blender_version": bpy.app.version_string,
        "command": "blender --background --python-exit-code 1 --python tools/make_art040.py -- --runtime3d",
    }


def _asset_record(asset_id: str, kind: str, source: Path, runtime: Path,
                  bounds: dict, statistics: dict, material_profile: str,
                  legacy_binding: dict | None = None) -> dict:
    minimum, maximum = bounds["min"], bounds["max"]
    record = {
        "id": asset_id, "kind": kind,
        "runtime_path": runtime.relative_to(ROOT).as_posix(),
        "source_path": source.relative_to(ROOT).as_posix(),
        "source_pack": "MET original parameterized Laisheng geometry",
        "author": "MET project; original procedural geometry, Codex-assisted implementation",
        "source_url": "https://github.com/theodontino/MET-Laisheng",
        "license": "LicenseRef-MET-Project-Original",
        "license_url": "res://docs/living_world/ASSET_PROVENANCE.md",
        "download_date": "2026-10-03",
        "download_date_note": "Original in-project export date; no external download",
        "source_sha256": sha256(source), "runtime_sha256": sha256(runtime),
        "scale": [1.0, 1.0, 1.0], "pivot": [0.0, 0.0, 0.0],
        "footprint": {"xz_bounds": [minimum[0], minimum[2], maximum[0], maximum[2]]},
        "bounds": bounds,
        "tags": ["hero-valley", "original", "runtime-3d", "realtime-lightable", kind],
        "material_profile": material_profile,
        "redistribution_policy": "project-source-and-runtime",
        "attribution_required": False,
        "source_to_runtime": {"uniform_scale": SOURCE_SCALE, "axis_mapping": "[x,z,-y]",
                              "pivot_origin": "legacy-origin-ground", "scale_baked": True},
        "generator": _generator_metadata(), "mesh_statistics": statistics,
    }
    if legacy_binding:
        record["legacy_binding"] = legacy_binding
        x0, y0, x1, y1 = legacy_binding["footprint_world_bounds"]
        record["footprint"] = {"xz_bounds": [round(x0 * SOURCE_SCALE, 7), round(-y1 * SOURCE_SCALE, 7),
                                                round(x1 * SOURCE_SCALE, 7), round(-y0 * SOURCE_SCALE, 7)]}
        record["connector_anchors"] = {
            name: _runtime_point([point[0], point[1], 0.0])
            for name, point in legacy_binding["connector_anchors"].items()
        }
    return record


def _simplify_tree_rachises():
    """Keep the existing fern silhouette, with five-sided unbevelled thin stems.

    Forty subpixel rachis cylinders account for most of the original botanical
    mesh's triangles. Their centers, lengths and directions remain identical;
    canopy ribbons and the main trunk are unchanged. This is the single V1 tree,
    not another species or a runtime LOD system.
    """
    import bpy
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or "flexible rachis" not in obj.name:
            continue
        radius = max(math.hypot(v.co.x, v.co.y) for v in obj.data.vertices)
        z0, z1 = min(v.co.z for v in obj.data.vertices), max(v.co.z for v in obj.data.vertices)
        vertices = [(radius * math.cos(math.tau * i / 5), radius * math.sin(math.tau * i / 5), z)
                    for z in (z0, z1) for i in range(5)]
        faces = [(i, (i + 1) % 5, (i + 1) % 5 + 5, i + 5) for i in range(5)]
        faces.extend([(4, 3, 2, 1, 0), (5, 6, 7, 8, 9)])
        data = bpy.data.meshes.new(obj.name + ".v1")
        data.from_pydata(vertices, [], faces)
        for material in obj.data.materials:
            data.materials.append(material)
        data.update()
        obj.modifiers.clear()
        obj.data = data


def _export_one(legacy, key: str, catalog: dict) -> dict:
    import bpy
    legacy.clear_scene()
    is_tree = key == "valley_tree_01"
    asset_id = "nature.tree.valley_01" if is_tree else BUILDING_IDS[key]
    material_profile = TREE_MATERIAL_PROFILE if is_tree else "met.mineral_pbr.v1"
    if is_tree:
        # The runtime foliage palette is independent of the legacy PNG palette.
        # Restore even on construction failure; no color change leaks to other
        # building/PNG exports using the existing geometry module.
        original_palette = legacy.PALETTE.copy()
        try:
            legacy.PALETTE.update(TREE_RUNTIME_PALETTE)
            legacy.alien_tree("Valley native fern", 0.0, 0.0, height=12.0, crown=4.5, upright=False)
        finally:
            legacy.PALETTE.clear()
            legacy.PALETTE.update(original_palette)
        for material in legacy._mats.values():
            material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.90
        _simplify_tree_rachises()
    else:
        legacy.build_building(key)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene["logical_asset_id"] = asset_id
    scene["material_profile"] = material_profile
    scene["runtime_export_axis_mapping"] = "[x,z,-y]"
    scene["runtime_export_uniform_scale"] = SOURCE_SCALE
    scene["provenance"] = "Original MET procedural components; no downloaded assets"
    source = SOURCE_DIR / (key + ".blend")
    source.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    runtime_mesh, bounds, statistics = _merge_for_runtime(asset_id, material_profile)
    subdir = "nature" if is_tree else "buildings"
    runtime = RUNTIME_DIR / subdir / (key + ".glb")
    runtime.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(runtime), export_format="GLB", use_selection=True,
        export_yup=True, export_apply=True, export_materials="EXPORT",
        export_animations=False, export_cameras=False, export_lights=False,
        export_extras=True, export_texcoords=False, export_normals=True,
        export_tangents=False,
    )
    legacy_binding = None
    if not is_tree:
        old_id = "building_" + key
        old = catalog["assets"][old_id]
        legacy_binding = {
            "logical_asset_id": old_id, "definition_id": key,
            "catalog_path": LEGACY_CATALOG.relative_to(ROOT).as_posix(),
            "footprint_world_bounds": old["footprint_world_bounds"],
            "world_bounds": old["world_bounds"], "connector_anchors": old["connector_anchors"],
        }
    record = _asset_record(asset_id, "foliage" if is_tree else "building", source, runtime,
                           bounds, statistics, material_profile, legacy_binding)
    print("RUNTIME_3D", asset_id, json.dumps(statistics), "bytes", runtime.stat().st_size, flush=True)
    return record


def export_hero_assets() -> dict:
    legacy = load_legacy()
    catalog = json.loads(LEGACY_CATALOG.read_text(encoding="utf-8"))
    records = [_export_one(legacy, key, catalog) for key in (*HERO_BUILDINGS, "valley_tree_01")]
    manifest = {
        "schema": "met.presentation3d.v1", "version": 1,
        "units": "Hex radius 1; glTF/Godot right-handed Y-up; source scale baked into vertices",
        "camera_reference": {"direction_to_camera": [9.0, 13.2, 12.0],
                             "elevation_degrees": 41.35, "azimuth_degrees_blender_xy": -53.1301},
        "material_profiles": {"met.mineral_pbr.v1": {
            "description": "Original mineral colors; Principled metallic-roughness; realtime scene lighting",
            "baked_directional_light": False, "baked_ao": False, "textures": False,
        }, TREE_MATERIAL_PROFILE: {
            "description": "Runtime-only native fern greens, calibrated for fixed daylight and filmic tonemapping",
            "palette_note": "Tree-only saturated/deeper greens; legacy PNG and building palettes unchanged",
            "palette_srgb_hex": TREE_RUNTIME_PALETTE, "roughness": 0.90,
            "baked": False, "baked_directional_light": False, "baked_ao": False, "textures": False,
        }},
        "bindings": {key: BUILDING_IDS[key] for key in HERO_BUILDINGS},
        "assets": records,
    }
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("RUNTIME_3D_MANIFEST", MANIFEST_PATH, "assets", len(records), flush=True)
    return manifest


if __name__ == "__main__":
    export_hero_assets()
