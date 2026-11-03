Shader "MET/City/RoadSurface"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1,1,1,1)
        _BaseMap("Original road albedo", 2D) = "gray" {}
        _WorldScale("World repetition", Float) = 1
        _Smoothness("Smoothness", Range(0,1)) = .08
        _Metallic("Metallic", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            half _Metallic;
            float _WorldScale;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CityVertex
            #pragma fragment CityFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct CityAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct CityVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 uv : TEXCOORD2;
                half3 vertexLighting : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            CityVaryings CityVertex(CityAttributes input)
            {
                CityVaryings output = (CityVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                output.vertexLighting = VertexLighting(output.positionWS, output.normalWS);
                return output;
            }
            float GroundHash(float2 p)
            {
                p = frac(p * float2(.1031,.11369));
                p += dot(p,p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }
            float GroundNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(GroundHash(i),GroundHash(i+float2(1,0)),f.x),
                            lerp(GroundHash(i+float2(0,1)),GroundHash(i+1),f.x),f.y);
            }
            half4 CityFragment(CityVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = NormalizeNormalPerPixel(input.normalWS);
                // World triplanar projection also serves steps and retaining walls:
                // vertical faces retain stone size instead of collapsing XZ UVs.
                float3 roadPoint = input.positionWS * _WorldScale;
                float3 projectionWeights = pow(abs(normal),4);
                projectionWeights /= max(projectionWeights.x+projectionWeights.y+projectionWeights.z,.0001);
                half3 paint = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,roadPoint.zy).rgb * projectionWeights.x
                            + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,roadPoint.xz).rgb * projectionWeights.y
                            + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,roadPoint.xy).rgb * projectionWeights.z;
                half3 albedo = paint * _BaseColor.rgb * .90;
                // Fine grain comes from mipmapped authored maps. Broad irregularity
                // stays restrained and fixed in world space across clipped triangles.
                float macro = GroundNoise(input.positionWS.xz*.47);
                albedo *= .96 + macro*.08;
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                data.normalWS = normal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                data.bakedGI = SampleSH(normal);
                data.vertexLighting = input.vertexLighting;
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = _Metallic;
                surface.specular = half3(.04,.04,.04);
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;
                return UniversalFragmentPBR(data, surface);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
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
