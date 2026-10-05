using System;
using System.Collections.Generic;
using UnityEngine;

namespace MET.LivingWorld
{
    [Serializable] public sealed class HeroCheck
    {
        public string name, detail;
        public bool passed;
        public HeroCheck(string name, bool passed, string detail)
        { this.name = name; this.passed = passed; this.detail = detail; }
    }
    [Serializable] public sealed class HeroValidationReport
    {
        public string status, unity_version, rendering_pipeline, graphics_device, graphics_backend;
        public string source_phase, snapshot_signature, sidecar_sha256, geometry_sha256, error;
        public int width, height, checks_passed, checks_failed;
        public int tiles, buildings, trees, foliage_batches, world_meshes, world_triangles, triangles_per_tree;
        public int geometry_build_count, tree_geometry_build_count, normal_samples;
        public float maximum_grounding_error, maximum_projection_error;
        public string screenshot_sha256;
        public double initialization_wall_ms;
        public HeroCheck[] checks;
        public string measurement_scope = "Read-only Unity presentation; no Gameplay Simulation is included.";
    }

    public static class HeroValidation
    {
        public static HeroValidationReport Validate(HeroWorld world)
        {
            var checks = new List<HeroCheck>();
            var report = new HeroValidationReport {
                status = "failed", unity_version = Application.unityVersion,
                rendering_pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "none" :
                    UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName,
                graphics_device = SystemInfo.graphicsDeviceName, graphics_backend = SystemInfo.graphicsDeviceType.ToString(),
                width = Screen.width, height = Screen.height
            };
            try
            {
                HeroContract.Require(world != null && world.Contract != null && world.Geometry != null,
                    "Hero world did not initialize.");
                var data = world.Contract;
                data.Validate();
                report.source_phase = data.source_phase;
                report.snapshot_signature = data.snapshot_signature;
                report.sidecar_sha256 = world.SidecarSha256;
                report.tiles = data.tiles.Length;
                report.trees = world.Foliage.InstanceCount;
                report.foliage_batches = world.Foliage.BatchCount;
                report.triangles_per_tree = world.Foliage.PrototypeTriangles;
                report.geometry_build_count = world.GeometryBuildCount;
                report.tree_geometry_build_count = world.Foliage.GeometryBuildCount;
                report.initialization_wall_ms = world.InitializationWallMilliseconds;
                checks.Add(new HeroCheck("committed_snapshot_only", !data.forecast && data.source_phase == "committed_tick", data.snapshot_signature));
                checks.Add(new HeroCheck("source_json_immutable", world.SidecarUnchanged(), report.sidecar_sha256));
                var identities = new HashSet<string>();
                foreach (var tile in data.tiles) identities.Add(tile.key);
                checks.Add(new HeroCheck("37_unique_hexes", identities.Count == 37, identities.Count.ToString()));
                var buildings = new List<Transform>();
                foreach (var transform in world.Geometry.GetComponentsInChildren<Transform>(true))
                    if (transform.name.StartsWith("Building_", StringComparison.Ordinal)) buildings.Add(transform);
                report.buildings = buildings.Count;
                checks.Add(new HeroCheck("4_existing_buildings", buildings.Count == 4, buildings.Count.ToString()));
                checks.Add(new HeroCheck("29_instanced_trees", report.trees == 29 && report.foliage_batches == 6 &&
                    world.Foliage.transform.childCount == 0, report.trees + " instances / " + report.foliage_batches + " batches / no tree GameObjects"));
                checks.Add(new HeroCheck("tree_prototype_triangle_count", report.triangles_per_tree == data.model_stats.triangles_per_tree,
                    report.triangles_per_tree.ToString()));
                var digest = new System.Text.StringBuilder();
                var normalFailures = 0;
                var vertexColorSources = new HashSet<string>();
                var vertexColorFailures = 0;
                var materialFailures = 0;
                var vertexCount = 0;
                var actualBounds = new Bounds();
                var hasBounds = false;
                foreach (var filter in world.Geometry.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    HeroContract.Require(mesh != null && mesh.isReadable, "Imported geometry must be readable.");
                    var vertices = mesh.vertices;
                    var normals = mesh.normals;
                    HeroContract.Require(normals.Length == vertices.Length && vertices.Length > 0,
                        "Imported geometry requires complete normals.");
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || renderer.sharedMaterials.Length != mesh.subMeshCount) materialFailures++;
                    else foreach (var material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader" ||
                            material.GetTag("RenderPipeline", false, "") != "UniversalPipeline")
                            materialFailures++;
                    var albedoSource = NamedAncestor(filter.transform, "TerrainMesh", "RiverRibbon");
                    if (albedoSource != "")
                    {
                        vertexColorSources.Add(albedoSource);
                        if (mesh.colors.Length != vertices.Length) vertexColorFailures++;
                    }
                    vertexCount += vertices.Length;
                    report.world_meshes++;
                    var matrix = filter.transform.localToWorldMatrix;
                    digest.Append(filter.name).Append(':');
                    foreach (var vertex in vertices)
                    {
                        var point = matrix.MultiplyPoint3x4(vertex);
                        digest.Append(point.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                            .Append(point.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                            .Append(point.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
                    }
                    for (var submesh = 0; submesh < mesh.subMeshCount; ++submesh)
                    {
                        var indices = mesh.GetIndices(submesh);
                        report.world_triangles += indices.Length / 3;
                        foreach (var vertexIndex in indices)
                        {
                            var point = matrix.MultiplyPoint3x4(vertices[vertexIndex]);
                            if (!hasBounds) { actualBounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                            else actualBounds.Encapsulate(point);
                        }
                        var stride = Mathf.Max(3, (indices.Length / 192 / 3) * 3);
                        for (var index = 0; index + 2 < indices.Length; index += stride)
                        {
                            var a = indices[index]; var b = indices[index + 1]; var c = indices[index + 2];
                            var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                            if (face.sqrMagnitude < 1e-12f) continue;
                            var normal = normals[a] + normals[b] + normals[c];
                            if (normal.sqrMagnitude < .1f || Vector3.Dot(face.normalized, normal.normalized) < .2f)
                                normalFailures++;
                            report.normal_samples++;
                        }
                        foreach (var index in indices) digest.Append(index).Append(',');
                    }
                }
                report.geometry_sha256 = HeroContract.Sha256(digest.ToString());
                // glTFast may split a GLB mesh into primitive renderers: source is 21 mesh nodes / 85 primitives.
                checks.Add(new HeroCheck("world_mesh_inventory", report.world_meshes >= data.model_stats.mesh_instances_world && report.world_meshes <= 85,
                    report.world_meshes + " Unity meshes / " + vertexCount + " vertices; 21 logical source meshes, 85 primitive maximum"));
                checks.Add(new HeroCheck("world_triangle_inventory", report.world_triangles == data.model_stats.triangles_world,
                    report.world_triangles.ToString()));
                checks.Add(new HeroCheck("real_mesh_normals", report.normal_samples > 0 && normalFailures == 0,
                    report.normal_samples + " sampled triangles, " + normalFailures + " failures"));
                checks.Add(new HeroCheck("terrain_river_vertex_albedo", vertexColorSources.Count == 2 && vertexColorFailures == 0,
                    vertexColorSources.Count + " source meshes with COLOR_0 / " + vertexColorFailures + " incomplete arrays; no baked directional light"));
                checks.Add(new HeroCheck("imported_pbr_materials", materialFailures == 0,
                    materialFailures + " missing/error/non-UniversalPipeline material slots"));
                HeroContract.Require(data.bounds != null, "Exported indexed geometry bounds are missing.");
                var sourceMin = CoordinateConvention.Vector(data.bounds.min);
                var sourceMax = CoordinateConvention.Vector(data.bounds.max);
                var expectedMin = new Vector3(sourceMax.x, sourceMin.y, sourceMin.z);
                var expectedMax = new Vector3(sourceMin.x, sourceMax.y, sourceMax.z);
                var boundsError = Mathf.Max(Vector3.Distance(actualBounds.min, expectedMin), Vector3.Distance(actualBounds.max, expectedMax));
                checks.Add(new HeroCheck("actual_indexed_geometry_bounds", hasBounds && boundsError < .001f,
                    "Imported indexed geometry / max bounds error " + boundsError.ToString("R")));
                Physics.SyncTransforms();
                var groundingHits = 0;
                var groundingFailures = 0;
                HeroContract.Require(data.grounding_samples != null && data.grounding_samples.Length >= 80,
                    "Actual exported grounding samples are required.");
                foreach (var sample in data.grounding_samples)
                {
                    var point = CoordinateConvention.Vector(sample.position);
                    if (!Physics.Raycast(point + Vector3.up * 20, Vector3.down, out var hit, 50, 1 << HeroWorld.GroundLayer))
                    { groundingFailures++; continue; }
                    groundingHits++;
                    var error = Mathf.Abs(hit.point.y - sample.expected_height);
                    report.maximum_grounding_error = Mathf.Max(report.maximum_grounding_error, error);
                    if (error > sample.tolerance) groundingFailures++;
                }
                checks.Add(new HeroCheck("actual_mesh_grounding", groundingFailures == 0 && groundingHits == data.grounding_samples.Length,
                    groundingHits + " collider hits / max error " + report.maximum_grounding_error.ToString("R")));
                var camera = world.CameraController.WorldCamera;
                foreach (var tile in data.tiles)
                {
                    var point = CoordinateConvention.Vector(tile.center);
                    var expectedPosition = CoordinateConvention.Vector(data.camera.position);
                    var expectedForward = (CoordinateConvention.Vector(data.camera.target) - expectedPosition).normalized;
                    var expectedRight = Vector3.Cross(CoordinateConvention.Vector(data.camera.up), expectedForward).normalized;
                    var expectedUp = Vector3.Cross(expectedForward, expectedRight).normalized;
                    var relative = point - expectedPosition;
                    const float aspect = 1920f / 1080f;
                    var expected = new Vector2(.5f + Vector3.Dot(relative, expectedRight) /
                        (data.camera.vertical_span * aspect),
                        .5f + Vector3.Dot(relative, expectedUp) / data.camera.vertical_span);
                    var actual = camera.WorldToViewportPoint(point);
                    report.maximum_projection_error = Mathf.Max(report.maximum_projection_error,
                        Vector2.Distance(expected, new Vector2(actual.x, actual.y)));
                }
                checks.Add(new HeroCheck("actual_camera_projection", report.maximum_projection_error < .0001f,
                    "37 actual WorldToViewportPoint samples / max error " + report.maximum_projection_error.ToString("R")));
                checks.Add(new HeroCheck("single_geometry_build", world.GeometryBuildCount == 1 && world.Foliage.GeometryBuildCount == 1,
                    "Camera changes must not rebuild geometry."));
            }
            catch (Exception error)
            { report.error = error.ToString(); checks.Add(new HeroCheck("validation_exception", false, error.Message)); }
            report.checks = checks.ToArray();
            foreach (var check in checks)
                if (check.passed) report.checks_passed++; else report.checks_failed++;
            report.status = report.checks_failed == 0 ? "passed" : "failed";
            return report;
        }

        private static string NamedAncestor(Transform transform, params string[] names)
        {
            for (var current = transform; current != null; current = current.parent)
                foreach (var name in names) if (current.name == name) return name;
            return "";
        }

        public static string GeometryFingerprint(HeroWorld world)
        {
            var digest = new System.Text.StringBuilder();
            foreach (var filter in world.Geometry.GetComponentsInChildren<MeshFilter>(true))
            {
                digest.Append(filter.name).Append(':');
                var matrix = filter.transform.localToWorldMatrix;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = matrix.MultiplyPoint3x4(vertex);
                    digest.Append(point.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(point.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(point.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
                }
                for (var submesh = 0; submesh < filter.sharedMesh.subMeshCount; ++submesh)
                    foreach (var index in filter.sharedMesh.GetIndices(submesh)) digest.Append(index).Append(',');
            }
            return HeroContract.Sha256(digest.ToString());
        }
    }
}
