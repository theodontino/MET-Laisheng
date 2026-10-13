using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MET.City {
/// <summary>One original imported tree; committed placements share per-material instancing batches.</summary>
public sealed class CityFoliage : MonoBehaviour {
    Mesh combined;
    Material[] materials;
    Matrix4x4[] placements=Array.Empty<Matrix4x4>();
    public int InstanceCount=>placements.Length;
    public Matrix4x4[] CapturePlacements()=>(Matrix4x4[])placements.Clone();
    public void Initialize(GameObject prototype) {
        var pieces=new List<CombineInstance>();var imported=new List<Material>();
        foreach(var filter in prototype.GetComponentsInChildren<MeshFilter>(true)) {
            var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;
            if(mesh==null||renderer==null||!mesh.isReadable)throw new InvalidOperationException("City tree must be imported readable by glTFast.");
            for(int i=0;i<mesh.subMeshCount;i++) {
                pieces.Add(new CombineInstance {mesh=mesh,subMeshIndex=i,transform=prototype.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                var material=renderer.sharedMaterials[i];material.enableInstancing=true;imported.Add(material);
            }
        }
        combined=new Mesh {name="CityOriginalTreeShared",indexFormat=IndexFormat.UInt32};combined.CombineMeshes(pieces.ToArray(),false,true,false);combined.RecalculateBounds();materials=imported.ToArray();combined.UploadMeshData(true);
    }
    public void SetPlacements(List<Matrix4x4> values) {placements=values.ToArray();}
    void LateUpdate() {
        if(combined==null||placements.Length==0)return;
        for(int submesh=0;submesh<materials.Length;submesh++)Graphics.DrawMeshInstanced(combined,submesh,materials[submesh],placements,placements.Length,null,ShadowCastingMode.On,true,gameObject.layer,null,LightProbeUsage.Off);
    }
    void OnDestroy() {if(combined!=null)Destroy(combined);}
}
}
