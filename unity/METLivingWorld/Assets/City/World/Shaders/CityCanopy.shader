Shader "MET/City/Canopy"
{
    Properties
    {
        _BaseMap("Official leaf color and silhouette", 2D) = "white" {}
        _BaseColor("Soft canopy palette", Color) = (.46,.52,.36,1)
        _Cutoff("Leaf silhouette cutoff", Range(0,1)) = .14
        _TextureDetail("Maximum source color detail", Range(0,.08)) = .08
        _InstanceVariation("Whole tree palette variation", Range(0,.12)) = .06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off ZWrite On
        HLSLINCLUDE
        #define _ALPHATEST_ON 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _TextureDetail;
            half _InstanceVariation;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "CanopyForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CanopyVertex
            #pragma fragment CanopyFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct CanopyAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct CanopyVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half instanceTint : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half crownTone : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float CanopyPositionVariation(float2 position)
            {
                float2 p = frac(position * float2(.1031,.11369));
                p += dot(p,p.yx + 19.19);
                return frac((p.x+p.y)*p.x) * 2 - 1;
            }
            CanopyVaryings CanopyVertex(CanopyAttributes input)
            {
                CanopyVaryings output = (CanopyVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv,_BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.crownTone = input.color.r;
                // Instance origin supplies one stable tint for the entire tree, not per-leaf speckles.
                output.instanceTint = CanopyPositionVariation(TransformObjectToWorld(float3(0,0,0)).xz);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 CanopyFragment(CanopyVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 source = SampleAlbedoAlpha(input.uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap));
                Alpha(source.a,_BaseColor,_Cutoff);
                // Original RGB supplies only bounded brightness; the original alpha remains authoritative.
                half sourceLuma = dot(source.rgb,half3(.2126,.7152,.0722));
                half detail = 1 + clamp((sourceLuma-.14h)*3,-1,1) * _TextureDetail;
                half tint = input.instanceTint * _InstanceVariation;
                half3 albedo = _BaseColor.rgb * half3(1+tint,1+tint*.35h,1-tint*.65h) * detail * input.crownTone;
                // Both sides use the authored whole-crown normal. Flipping every leaf card recreates black spots.
                half3 normalWS = normalize(input.normalWS);
                Light sun = GetMainLight();
                half wrappedDiffuse = pow(saturate((dot(normalWS,sun.direction)+.18h)/1.18h),1.12h);
                half3 ambient = min(max(SampleSH(normalWS),0)*.16h,.10h);
                // Let the coherent crown normal carry broad sun/side contrast instead of clipping it into a flat fill.
                half3 light = .33h + ambient + sun.color * wrappedDiffuse * .70h;
                return half4(MixFog(albedo*light,input.fogFactor),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            // Official URP bias/clamping and the same original alpha clip cast the ground canopy shadow.
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
