using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MET.LivingWorld
{
    // One geometry prototype, six material/submesh batches, no per-tree objects.
    public sealed class HeroFoliageRenderer : MonoBehaviour
    {
        private Mesh sharedMesh;
        private Material[] sharedMaterials;
        private Matrix4x4[] instances;
        public int InstanceCount => instances == null ? 0 : instances.Length;
        public int BatchCount => sharedMaterials == null ? 0 : sharedMaterials.Length;
        public int PrototypeTriangles { get; private set; }
        public int GeometryBuildCount { get; private set; }
        public Bounds PrototypeBounds => sharedMesh == null ? new Bounds() : sharedMesh.bounds;

        public void Initialize(GameObject importedPrefab, HeroFoliage[] placements)
        {
            HeroContract.Require(sharedMesh == null, "Tree geometry is built only once per world load.");
            HeroContract.Require(importedPrefab != null && placements != null, "Imported tree data is missing.");
            HeroContract.Require(placements.Length <= 1023, "One instancing batch supports at most 1023 instances.");
            var filters = importedPrefab.GetComponentsInChildren<MeshFilter>(true);
            var pieces = new List<CombineInstance>();
            var materials = new List<Material>();
            var inverseRoot = importedPrefab.transform.worldToLocalMatrix;
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                HeroContract.Require(mesh != null && mesh.isReadable && renderer != null,
                    "glTFast tree meshes must be imported as readable geometry.");
                HeroContract.Require(renderer.sharedMaterials.Length == mesh.subMeshCount,
                    "Each tree submesh requires an imported material.");
                for (var submesh = 0; submesh < mesh.subMeshCount; ++submesh)
                {
                    pieces.Add(new CombineInstance {
                        mesh = mesh, subMeshIndex = submesh,
                        transform = inverseRoot * filter.transform.localToWorldMatrix
                    });
                    var material = renderer.sharedMaterials[submesh];
                    HeroContract.Require(material != null, "Tree material was not imported.");
                    material.enableInstancing = true;
                    materials.Add(material);
                    PrototypeTriangles += (int)mesh.GetIndexCount(submesh) / 3;
                }
            }
            HeroContract.Require(pieces.Count == 6, "Expected the six exported foliage material batches.");
            sharedMesh = new Mesh { name = "ValleyTreeSharedPrototype", indexFormat = IndexFormat.UInt32 };
            sharedMesh.CombineMeshes(pieces.ToArray(), false, true, false);
            sharedMesh.RecalculateBounds();
            sharedMaterials = materials.ToArray();
            instances = new Matrix4x4[placements.Length];
            for (var index = 0; index < placements.Length; ++index)
                instances[index] = Matrix4x4.TRS(CoordinateConvention.Vector(placements[index].position),
                    CoordinateConvention.Rotation(placements[index].rotation),
                    CoordinateConvention.Scale(placements[index].scale));
            GeometryBuildCount++;
        }

        private void LateUpdate()
        {
            if (sharedMesh == null || instances == null) return;
            for (var submesh = 0; submesh < sharedMaterials.Length; ++submesh)
                Graphics.DrawMeshInstanced(sharedMesh, submesh, sharedMaterials[submesh], instances,
                    instances.Length, null, ShadowCastingMode.On, true, gameObject.layer, null,
                    LightProbeUsage.Off, null);
        }

        private void OnDestroy()
        {
            if (sharedMesh != null) Destroy(sharedMesh);
        }
    }
}
