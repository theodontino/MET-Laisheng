using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MET.City {
/// <summary>Official imported nature prototypes, shared once per variant and drawn in bounded material batches.</summary>
public sealed class CityFoliage : MonoBehaviour {
    public struct Placement {
        public int variant;
        public Matrix4x4 matrix;
        public Placement(int variant,Matrix4x4 matrix){this.variant=variant;this.matrix=matrix;}
    }
    sealed class Variant {
        public Mesh mesh;
        public Material[] materials;
        public readonly List<Matrix4x4[]> batches=new List<Matrix4x4[]>();
    }
    readonly List<Variant> variants=new List<Variant>();
    readonly List<Material> adaptedMaterials=new List<Material>();
    Matrix4x4[] placements=Array.Empty<Matrix4x4>();
    public int InstanceCount=>placements.Length;
    public int DrawBatchCount {get {int count=0;foreach(var variant in variants)count+=variant.materials.Length*variant.batches.Count;return count;}}
    public Matrix4x4[] CapturePlacements()=>(Matrix4x4[])placements.Clone();
    public Bounds VariantBounds(int index)=>variants[index].mesh.bounds;
    public void Initialize(GameObject prototype)=>Initialize(new[]{prototype});
    public void Initialize(GameObject[] prototypes) {
        foreach(var prototype in prototypes) {
            var groups=new Dictionary<Material,List<CombineInstance>>();var imported=new List<Material>();
            var adapted=new Dictionary<Material,Material>();var canopyCopies=new List<Mesh>();
            foreach(var filter in prototype.GetComponentsInChildren<MeshFilter>(true)) {
                var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;
                if(mesh==null||renderer==null||!mesh.isReadable)throw new InvalidOperationException("City nature must be imported readable by official glTFast.");
                for(int i=0;i<mesh.subMeshCount;i++) {
                    // Adapt only a shared display copy; preserve the official imported/source asset.
                    var original=renderer.sharedMaterials[i];bool leaf=IsCanopyMaterial(original);var material=original;var displayMesh=mesh;
                    if(leaf) {
                        if(!adapted.TryGetValue(original,out material)) {
                            var template=Resources.Load<Material>("Rendering/CityCanopy_"+original.name);
                            if(template==null||template.shader.name!="MET/City/Canopy")throw new InvalidOperationException("Run CityNatureAssets.Prepare to import the serialized CityCanopy material: "+original.name);
                            material=new Material(template){name="CityCanopy_"+original.name};adapted.Add(original,material);adaptedMaterials.Add(material);
                        }
                        displayMesh=CanopyDisplayMesh(mesh,i);canopyCopies.Add(displayMesh);
                    } else if(original.name=="Rocks") {
                        if(!adapted.TryGetValue(original,out material)) {
                            material=new Material(Resources.Load<Material>("Rendering/CityLandscapeVertex")){name="CityRockOutcrop",enableInstancing=true};
                            material.SetFloat("_RockSurface",1);material.SetColor("_BaseColor",new Color(.52f,.52f,.49f).linear);
                            adapted.Add(original,material);adaptedMaterials.Add(material);
                        }
                    }
                    material.enableInstancing=true;
                    if(!groups.TryGetValue(material,out var pieces)){pieces=new List<CombineInstance>();groups[material]=pieces;imported.Add(material);}
                    pieces.Add(new CombineInstance {mesh=displayMesh,subMeshIndex=i,transform=prototype.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
            var merged=new List<CombineInstance>();var temporary=new List<Mesh>();
            foreach(var material in imported){var mesh=new Mesh {indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(groups[material].ToArray(),true,true,false);temporary.Add(mesh);merged.Add(new CombineInstance {mesh=mesh,transform=Matrix4x4.identity});}
            var combined=new Mesh {name="CityNatureShared_"+prototype.name,indexFormat=IndexFormat.UInt32};combined.CombineMeshes(merged.ToArray(),false,true,false);combined.RecalculateBounds();combined.UploadMeshData(true);
            variants.Add(new Variant{mesh=combined,materials=imported.ToArray()});
            foreach(var mesh in temporary)Destroy(mesh);
            foreach(var mesh in canopyCopies)Destroy(mesh);
        }
    }
    public static bool IsCanopyMaterial(Material material)=>material.name.StartsWith("Leaves_",StringComparison.Ordinal)||material.name.StartsWith("CityCanopy_",StringComparison.Ordinal);
    public static void TuneCanopyMaterial(Material material) {
        bool pine=material.name.IndexOf("Pine",StringComparison.Ordinal)>=0;
        bool shrub=material.name.IndexOf("TwistedTree",StringComparison.Ordinal)>=0;
        // These are the tuned canopy shader working values. Keep the authored palette
        // at this brightness; do not apply an additional sRGB-to-linear transform.
        material.SetColor("_BaseColor",shrub?new Color(.38f,.51f,.27f):pine?new Color(.30f,.44f,.25f):new Color(.39f,.51f,.27f));
        material.SetFloat("_Cutoff",.14f);material.SetFloat("_TextureDetail",.08f);material.SetFloat("_InstanceVariation",.06f);
        material.enableInstancing=true;material.doubleSidedGI=true;material.renderQueue=(int)RenderQueue.AlphaTest;
    }
    static Mesh CanopyDisplayMesh(Mesh source,int submesh) {
        var mesh=Instantiate(source);mesh.name=source.name+"_SharedSoftCanopy";
        var vertices=mesh.vertices;var normals=mesh.normals;var colors=mesh.colors;var sourceIndices=source.GetIndices(submesh);var indices=new HashSet<int>(sourceIndices);
        var bounds=new Bounds(vertices[sourceIndices[0]],Vector3.zero);foreach(int index in indices)bounds.Encapsulate(vertices[index]);
        var center=bounds.center;float halfHeight=Mathf.Max(.001f,bounds.extents.y);
        foreach(int index in indices) {
            var vertex=vertices[index];float normalizedHeight=(vertex.y-center.y)/halfHeight;
            // Ellipsoidal crown: top points up, equator sideways, underside down. A constant up bias flattened v5.
            float nx=(vertex.x-center.x)/Mathf.Max(.001f,bounds.extents.x),nz=(vertex.z-center.z)/Mathf.Max(.001f,bounds.extents.z);
            var outward=new Vector3(nx,.10f+normalizedHeight*.85f,nz).normalized;
            normals[index]=Vector3.Lerp(normals[index],outward,.88f).normalized;
            // Broad height/interior occlusion, continuous across neighbouring cards rather than random leaf spots.
            float lowerCrown=Mathf.Lerp(.69f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.75f,.60f,normalizedHeight)));
            float radius=Mathf.Sqrt(nx*nx+nz*nz+normalizedHeight*normalizedHeight);
            float interior=Mathf.Lerp(.86f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.90f,radius)));
            float tone=lowerCrown*interior;colors[index]=new Color(tone,tone,tone,colors[index].a);
            // Fill the lower crown enough to veil trunks without changing the tree's apex or source file.
            vertex.y=Mathf.Lerp(bounds.max.y,vertex.y,1.09f);vertices[index]=vertex;
        }
        mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.RecalculateBounds();return mesh;
    }
    // The original scatter path remains valid; the active fabric path selects variants explicitly.
    public void SetPlacements(List<Matrix4x4> values) {var converted=new List<Placement>(values.Count);foreach(var value in values)converted.Add(new Placement(0,value));SetPlacements(converted);}
    public void SetPlacements(List<Placement> values) {
        placements=new Matrix4x4[values.Count];var grouped=new List<Matrix4x4>[variants.Count];
        for(int i=0;i<variants.Count;i++){grouped[i]=new List<Matrix4x4>();variants[i].batches.Clear();}
        for(int i=0;i<values.Count;i++){placements[i]=values[i].matrix;grouped[values[i].variant].Add(values[i].matrix);}
        for(int i=0;i<variants.Count;i++)for(int offset=0;offset<grouped[i].Count;offset+=1023) {
            int count=Mathf.Min(1023,grouped[i].Count-offset);var batch=new Matrix4x4[count];grouped[i].CopyTo(offset,batch,0,count);variants[i].batches.Add(batch);
        }
    }
    void LateUpdate() {
        foreach(var variant in variants)foreach(var batch in variant.batches)for(int submesh=0;submesh<variant.materials.Length;submesh++)
            Graphics.DrawMeshInstanced(variant.mesh,submesh,variant.materials[submesh],batch,batch.Length,null,ShadowCastingMode.On,true,gameObject.layer,null,LightProbeUsage.Off);
    }
    void OnDestroy() {foreach(var variant in variants)if(variant.mesh!=null)Destroy(variant.mesh);foreach(var material in adaptedMaterials)Destroy(material);}
}
}
