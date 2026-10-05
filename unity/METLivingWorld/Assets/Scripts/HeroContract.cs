using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MET.LivingWorld
{
    // Serialized visual data only. This project has no gameplay or save API.
    [Serializable] public sealed class HeroContract
    {
        public int schema;
        public string source_phase;
        public bool forecast;
        public int world_seed;
        public int visual_seed;
        public string snapshot_signature;
        public HeroModelStats model_stats;
        public HeroTile[] tiles;
        public HeroCamera camera;
        public HeroEnvironment environment;
        public HeroFoliage[] foliage;
        public HeroGroundingSample[] grounding_samples;
        public HeroBounds bounds;
        public HeroBuilding[] buildings;

        public void Validate()
        {
            Require(schema == 1, "Unsupported Hero sidecar schema.");
            Require(source_phase == "committed_tick" && !forecast,
                "Only a detached committed visual snapshot is accepted.");
            Require(world_seed == 905003 && visual_seed == 314159, "Unexpected Golden Scene seeds.");
            Require(!string.IsNullOrEmpty(snapshot_signature), "Snapshot signature is missing.");
            Require(tiles != null && tiles.Length == 37, "Hero Valley requires 37 logical Hexes.");
            Require(foliage != null && foliage.Length == 29, "Hero Valley requires 29 tree instances.");
            Require(model_stats != null && model_stats.buildings == 4 && model_stats.tiles == 37 &&
                model_stats.trees == 29 && model_stats.foliage_batches == 6,
                "Exported Hero geometry inventory does not match V1.");
            Require(camera != null && camera.vertical_span > 0 && camera.near > 0 && camera.far > camera.near,
                "Camera metadata is invalid.");
            CoordinateConvention.Vector(camera.position);
            CoordinateConvention.Vector(camera.target);
            CoordinateConvention.Vector(camera.up);
            Require(environment != null, "Fixed environment metadata is missing.");
            CoordinateConvention.Vector(environment.sun_direction);
            foreach (var tile in tiles)
            {
                Require(tile != null && !string.IsNullOrEmpty(tile.key), "Tile identity is missing.");
                CoordinateConvention.Vector(tile.center);
                Require(tile.outline != null && tile.outline.Length >= 6, "Hex outline is incomplete.");
                foreach (var vertex in tile.outline) CoordinateConvention.Vector(vertex);
            }
            foreach (var tree in foliage)
            {
                Require(tree != null && !string.IsNullOrEmpty(tree.id), "Foliage identity is missing.");
                CoordinateConvention.Vector(tree.position);
                CoordinateConvention.Rotation(tree.rotation);
                var scale = CoordinateConvention.Scale(tree.scale);
                Require(scale.x > 0 && scale.y > 0 && scale.z > 0, "Tree scale must be positive.");
            }
        }

        public static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
                    .Replace("-", "").ToLowerInvariant();
        }
    }

    [Serializable] public sealed class HeroModelStats
    {
        public int tiles, buildings, trees, foliage_batches, triangles_world, triangles_per_tree;
        public int mesh_instances_world, terrain_triangles, terrain_skirt_triangles;
        public int foundation_meshes, foundation_triangles, river_triangles, building_triangles, road_triangles;
    }
    [Serializable] public sealed class HeroTile
    {
        public string key, terrain;
        public int q, r;
        public float[] center;
        public float[][] outline;
    }
    [Serializable] public sealed class HeroCamera
    {
        public float[] position, target, up;
        public float vertical_span, near, far;
    }
    [Serializable] public sealed class HeroEnvironment
    {
        public float[] sun_direction, sun_color, background, ambient_color, wind;
        public float sun_intensity, ambient_intensity, world_time, cloudiness, fog_density, humidity, wetness;
        public string color_space, temperature_band;
    }
    [Serializable] public sealed class HeroFoliage
    {
        public string id, tile;
        public float[] position, rotation, scale;
    }
    [Serializable] public sealed class HeroGroundingSample
    {
        public string building_id;
        public float[] position;
        public float expected_height, tolerance;
    }
    [Serializable] public sealed class HeroBounds
    {
        public float[] min, max;
    }
    [Serializable] public sealed class HeroBuilding
    {
        public string id, node_name, asset_id, tile;
        public HeroBounds bounds;
        public float[] position, pivot, scale;
    }

    public static class CoordinateConvention
    {
        // glTFast already mirrors GLB geometry on X. Mirror only the sidecar.
        public static Vector3 Vector(float[] value)
        {
            HeroContract.Require(value != null && value.Length == 3, "Expected a finite XYZ vector.");
            foreach (var component in value)
                HeroContract.Require(!float.IsNaN(component) && !float.IsInfinity(component), "Non-finite XYZ.");
            return new Vector3(-value[0], value[1], value[2]);
        }

        public static Vector3 Scale(float[] value)
        {
            var mirrored = Vector(value);
            return new Vector3(-mirrored.x, mirrored.y, mirrored.z);
        }

        public static Quaternion Rotation(float[] value)
        {
            HeroContract.Require(value != null && value.Length == 4, "Expected an XYZW quaternion.");
            var result = new Quaternion(value[0], -value[1], -value[2], value[3]);
            HeroContract.Require(Mathf.Abs(Quaternion.Dot(result, result) - 1f) < 0.005f,
                "Tree rotation must be a unit quaternion.");
            return result.normalized;
        }

        public static Color Color(float[] value, bool srgb)
        {
            HeroContract.Require(value != null && (value.Length == 3 || value.Length == 4), "Expected RGB(A).");
            var result = new Color(value[0], value[1], value[2], value.Length == 4 ? value[3] : 1f);
            // Light, Camera and RenderSettings take sRGB Color inputs. Unity/URP
            // performs the active-color-space conversion; COLOR_0 stays glTF-linear.
            return srgb ? result : result.gamma;
        }
    }
}
