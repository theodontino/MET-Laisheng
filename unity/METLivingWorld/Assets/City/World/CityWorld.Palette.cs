using System;
using System.Collections.Generic;
using UnityEngine;
namespace MET.City {
public sealed partial class CityWorld {
    readonly Dictionary<Material,Dictionary<string,Material>> importedPalette=new Dictionary<Material,Dictionary<string,Material>>();
    readonly Dictionary<Texture,Dictionary<string,Texture2D>> importedAtlases=new Dictionary<Texture,Dictionary<string,Texture2D>>();
    static string BuildingPaletteFamily(string purpose) {
        if(purpose=="special")return "special";
        if(purpose=="housing")return "home";
        if(purpose=="industry"||purpose=="extractive"||purpose=="energy"||purpose=="logistics")return "works";
        if(purpose=="agriculture")return "farm";
        return "public";
    }
    static Color BuildingRoofTone(string family,bool shadow=false) {
        switch(family) {
            case "home":return Srgb(shadow?"527956":"7FA883");
            case "works":return Srgb(shadow?"9F7C35":"D1AE59");
            case "special":return Srgb(shadow?"944E43":"C77060");
            case "farm":return Srgb("AA957C");
            default:return Srgb(shadow?"4B7893":"78A8C3");
        }
    }
    public static Color BuildingTypeColor(string purpose)=>purpose=="green"?Srgb("8D9985"):BuildingRoofTone(BuildingPaletteFamily(purpose));
    Material BuildingRoofMaterial(string purpose)=>Surface("BuildingTypeRoof_"+(purpose=="green"?"green":BuildingPaletteFamily(purpose)),BuildingTypeColor(purpose));
    static Color BuildingWallTone(string purpose,bool shadow=false) {
        // Broad, light facade colour carries the same use as the stronger roof.
        // Window bands and structural trim retain their separate atlas slots.
        if(purpose=="green")return Srgb(shadow?"B4B59D":"DEDCC5");
        switch(BuildingPaletteFamily(purpose)) {
            case "home":return Srgb(shadow?"A5B79B":"D1DEC5");
            case "works":return Srgb(shadow?"C0AE80":"E7D8AC");
            case "special":return Srgb(shadow?"C09A8E":"E6C7BC");
            case "farm":return Srgb(shadow?"C8B99F":"EFE3CA");
            default:return Srgb(shadow?"9EB9C3":"CEDFE5");
        }
    }
    Material BuildingWallMaterial(string purpose)=>Surface("BuildingTypeWall_"+(purpose=="green"?"green":BuildingPaletteFamily(purpose)),BuildingWallTone(purpose));
    static string RoofSlotProfile(string resource) {
        // These grey slots form roofs only in the audited models. Other models
        // use the same atlas slots for equipment, structure or dark glazing.
        if(resource.EndsWith("/Suburban/building-type-p",StringComparison.Ordinal))return "community-roof";
        if(resource.EndsWith("/Industrial/building-m",StringComparison.Ordinal))return "sawtooth-roof";
        if(resource.EndsWith("/Industrial/building-s",StringComparison.Ordinal))return "freight-roof";
        return "standard";
    }
    void HarmonizeImportedPalette(Renderer renderer,string resource,string purpose) {
        if(purpose=="green")return;
        string family=BuildingPaletteFamily(purpose),pack=resource.Contains("/Industrial/")?"industrial":resource.Contains("/Kenney/")?"town":"original";
        string roofProfile=RoofSlotProfile(resource),variant=pack+"/"+family+"/"+roofProfile;var materials=renderer.sharedMaterials;bool changed=false;
        for(int i=0;i<materials.Length;i++) {
            var source=materials[i];if(source==null)continue;
            string colorProperty=source.HasProperty("baseColorFactor")?"baseColorFactor":"_BaseColor";
            if(!source.HasProperty(colorProperty))continue;
            string textureProperty=source.HasProperty("baseColorTexture")?"baseColorTexture":"_BaseMap";
            var texture=source.HasProperty(textureProperty)?source.GetTexture(textureProperty):null;
            bool atlas=pack!="original"&&source.name.StartsWith("colormap",StringComparison.OrdinalIgnoreCase)&&texture!=null;
            Color tone=Color.white;
            if(!atlas&&!NamedBuildingTone(source.name,out tone))continue;
            if(!importedPalette.TryGetValue(source,out var variants)){variants=new Dictionary<string,Material>(StringComparer.Ordinal);importedPalette[source]=variants;}
            if(!variants.TryGetValue(variant,out var styled)) {
                styled=new Material(source){name="CitySemanticPalette_"+variant+"_"+source.name,enableInstancing=true};
                var original=source.GetColor(colorProperty);
                if(atlas) {
                    // The original atlas already separates plaster, roof, glass
                    // and structure through UVs. Restore its white color factor;
                    // remap only the evidenced wall/roof slots, never the glass.
                    styled.SetTexture(textureProperty,BuildingAtlas(texture,pack,family,roofProfile));tone=Color.white;
                } else tone=SurfaceColor(tone);
                tone.a=original.a;styled.SetColor(colorProperty,tone);ownedMaterials.Add(styled);variants[variant]=styled;
            }
            materials[i]=styled;changed=true;
        }
        if(changed)renderer.sharedMaterials=materials;
    }
    static bool NamedBuildingTone(string name,out Color tone) {
        // These are the actual named materials in the original homestead GLB.
        // Glass, all metal grades, wood, gold and vegetation keep their authored
        // factors/roughness; hue cannot tell us their physical role.
        if(name.StartsWith("mat_ivory",StringComparison.Ordinal))tone=Srgb("EFE3CA");
        else if(name.StartsWith("mat_shadow_wall",StringComparison.Ordinal))tone=Srgb("C8B99F");
        else if(name.StartsWith("mat_trim",StringComparison.Ordinal))tone=Srgb("E6D8BD");
        else if(name.StartsWith("mat_stone",StringComparison.Ordinal))tone=Srgb("B0AE98");
        else {tone=Color.white;return false;}
        return true;
    }
    static bool AtlasBuildingTone(int column,int row,string pack,string family,string roofProfile,out Color tone,out Color reference) {
        tone=reference=Color.white;
        if(row!=4&&row!=5&&(pack=="industrial"||column!=0||(row!=2&&row!=3)))return false;
        bool shadow=row==5||row==3;
        if(pack=="industrial") {
            if(column==7) {reference=Srgb(shadow?"CBCBDE":"EDEDF4");tone=BuildingWallTone("industry",shadow);}
            else if(column==5) {reference=Srgb(shadow?"414350":"4E515F");tone=BuildingRoofTone(family,shadow);}
            else if(column==4&&(roofProfile=="freight-roof"||roofProfile=="sawtooth-roof"&&row==4)) {
                reference=Srgb(shadow?"6A6B80":"7D8096");tone=BuildingRoofTone(family,shadow);
            }
            else return false;
        } else {
            if(column==3) {
                reference=Srgb(shadow?"C8C8DC":"ECECF3");
                tone=BuildingWallTone(family=="home"?"housing":"community",shadow);
            } else if(column==1) {
                reference=Srgb(shadow?"414350":"4E515E");
                tone=BuildingRoofTone(family,shadow);
            } else if(column==0&&(row==2||row==3)) {
                reference=Srgb(shadow?"2A9370":"4FBA82");
                tone=BuildingRoofTone(family,shadow);
            } else if(column==0&&roofProfile=="community-roof") {
                reference=Srgb(shadow?"626880":"787D95");tone=BuildingRoofTone(family,shadow);
            } else return false;
        }
        return true;
    }
    Texture2D BuildingAtlas(Texture source,string pack,string family,string roofProfile) {
        string variant=pack+"/"+family+"/"+roofProfile;
        if(!importedAtlases.TryGetValue(source,out var variants)){variants=new Dictionary<string,Texture2D>(StringComparer.Ordinal);importedAtlases[source]=variants;}
        if(variants.TryGetValue(variant,out var cached))return cached;
        // glTFast remains responsible for import. Copy the official imported
        // albedo once, preserving source files, UVs, shaders and texture transforms.
        var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        var atlas=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,false){name="CityBuildingAtlas_"+variant,wrapMode=source.wrapMode,filterMode=source.filterMode,anisoLevel=source.anisoLevel};
        try {
            Graphics.Blit(source,target);RenderTexture.active=target;atlas.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);
            var pixels=atlas.GetPixels32();
            var remap=new bool[64];var tones=new Color[64];var referenceLuminance=new float[64];
            for(int row=0;row<8;row++)for(int column=0;column<8;column++) {
                int slot=row*8+column;remap[slot]=AtlasBuildingTone(column,row,pack,family,roofProfile,out tones[slot],out var reference);
                referenceLuminance[slot]=reference.r*.2126f+reference.g*.7152f+reference.b*.0722f;
            }
            for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++) {
                // glTF/PNG analysis uses top-left rows; Unity pixel arrays start
                // at the bottom. These 8x8 slots are from the installed CC0 atlas.
                int column=x*8/source.width,row=(source.height-1-y)*8/source.height;
                int slot=row*8+column;if(!remap[slot])continue;
                int index=y*source.width+x;Color original=pixels[index];
                float luminance=original.r*.2126f+original.g*.7152f+original.b*.0722f;
                var tone=tones[slot]*(luminance/referenceLuminance[slot]);tone.a=original.a;pixels[index]=tone;
            }
            atlas.SetPixels32(pixels);atlas.Apply(true,true);variants[variant]=atlas;return atlas;
        } catch {Destroy(atlas);throw;}
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
    }
    void DisposeImportedPalette() {
        foreach(var variants in importedAtlases.Values)foreach(var atlas in variants.Values)if(atlas!=null)Destroy(atlas);
        importedAtlases.Clear();importedPalette.Clear();
    }
}
}
