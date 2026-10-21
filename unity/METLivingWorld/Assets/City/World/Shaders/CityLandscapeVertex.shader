Shader "MET/City/LandscapeVertexPBR"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.12
        _Metallic("Metallic", Range(0,1)) = 0
        _DetailStrength("Ground material detail", Range(0,1)) = 0.65
        _GroundDetail("Hand-painted grass detail", 2D) = "gray" {}
        _ForestDetail("Original forest litter", 2D) = "gray" {}
        _DryDetail("Original dry turf and soil", 2D) = "gray" {}
        _RockDetail("Hand-painted exposed rock", 2D) = "gray" {}
        _RockSurface("Rock prop coverage", Range(0,1)) = 0
        [Toggle(_CITY_RIVER)] _River("River Surface", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_GroundDetail); SAMPLER(sampler_GroundDetail);
        TEXTURE2D(_ForestDetail); SAMPLER(sampler_ForestDetail);
        TEXTURE2D(_DryDetail); SAMPLER(sampler_DryDetail);
        TEXTURE2D(_RockDetail); SAMPLER(sampler_RockDetail);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            half _Metallic;
            half _River;
            half _DetailStrength;
            half _RockSurface;
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
            #pragma shader_feature_local _CITY_RIVER
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct CityAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 biome : TEXCOORD1;
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
                half2 biome : TEXCOORD4;
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
                output.biome = input.biome;
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
                half3 albedo = input.color.rgb * _BaseColor.rgb;
                #if defined(_CITY_RIVER)
                    // Long, sparse flow glints. Color/depth comes from the mesh, never a photo texture.
                    float flow = input.uv.y * 5.4 + sin(input.uv.x * 11 + input.uv.y * 1.3) * .5;
                    float ripple = sin(input.positionWS.z * 29 + input.positionWS.x * 6) * .7
                                 + sin(input.positionWS.x * 17 - input.positionWS.z * 12) * .3;
                    normal = normalize(normal + half3(sin(flow) * .035, 0, ripple * .028));
                    float glint = pow(saturate(ripple), 14) * .014;
                    albedo += glint * half3(.5,.8,1);
                #else
                    // World-space material detail is evaluated per pixel. It does not stretch a
                    // small map bitmap or create new terrain/gameplay vertices when zooming.
                    float2 p = input.positionWS.xz;
                    float macro = GroundNoise(p * .22) * .72 + GroundNoise(p * .57) * .28;
                    half3 painted = SAMPLE_TEXTURE2D(_GroundDetail,sampler_GroundDetail,p*.82).rgb;
                    half3 litter = SAMPLE_TEXTURE2D(_ForestDetail,sampler_ForestDetail,p*.83).rgb;
                    half3 dryPaint = SAMPLE_TEXTURE2D(_DryDetail,sampler_DryDetail,p*.68).rgb;
                    float forestWeight = saturate(input.biome.x);
                    float dryWeight = saturate(input.biome.y);
                    // Preserve source biome tints, while leaf/grass/soil shapes differ.
                    // Normalize their mean value to avoid darkening all the landscape.
                    half3 surfaceMarks = lerp(painted/.26,litter/.15,forestWeight);
                    surfaceMarks = lerp(surfaceMarks,dryPaint/.28,dryWeight);
                    surfaceMarks = clamp(surfaceMarks,half3(.50,.50,.50),half3(1.65,1.65,1.65));
                    albedo *= lerp(half3(1,1,1),surfaceMarks,.26);
                    albedo *= lerp(half3(1,1,1),half3(1.20,1.02,.83),forestWeight*.55);
                    half paintValue = dot(painted,half3(.2126,.7152,.0722));
                    // Short directional strokes break the turf into grass-scale marks.
                    // The derivative fade removes them before they can shimmer at distance.
                    float meso = GroundNoise(float2(p.x*21+p.y*9,p.y*57-p.x*5));
                    float footprint = max(length(ddx(p)),length(ddy(p)));
                    float fineFade = 1 - smoothstep(.012,.045,footprint);
                    float fine = GroundNoise(p * 87.0);
                    float grass = saturate((albedo.g - max(albedo.r,albedo.b) * .91) * 7);
                    float rock = max(_RockSurface,smoothstep(.10,.32,1-normal.y));
                    float dry = smoothstep(.48,.78,macro) * grass * .28;
                    albedo = lerp(albedo,albedo * half3(1.10,.93,.70),dry);
                    float materialTone = (macro-.5)*.07 + (meso-.5)*.16*fineFade + (fine-.5)*.035*fineFade;
                    albedo *= 1 + materialTone * _DetailStrength;
                    albedo *= lerp(1,clamp(.53+paintValue*2.8,.76,1.35),grass*.20);
                    float dryPatch = saturate((painted.r-painted.g*.91)*4);
                    albedo = lerp(albedo,albedo*half3(1.13,.96,.80),dryPatch*grass*.45);
                    // World-space triplanar rock avoids stretching the cliff texture
                    // up steep faces. Rock and turf have distinct shapes and scale.
                    float3 stoneWeights = pow(abs(normal),4);
                    stoneWeights /= max(stoneWeights.x+stoneWeights.y+stoneWeights.z,.0001);
                    float3 stonePoint = input.positionWS*.92;
                    half3 stone = SAMPLE_TEXTURE2D(_RockDetail,sampler_RockDetail,stonePoint.zy).rgb*stoneWeights.x
                                + SAMPLE_TEXTURE2D(_RockDetail,sampler_RockDetail,stonePoint.xz).rgb*stoneWeights.y
                                + SAMPLE_TEXTURE2D(_RockDetail,sampler_RockDetail,stonePoint.xy).rgb*stoneWeights.z;
                    half stoneValue = dot(stone,half3(.2126,.7152,.0722));
                    albedo = lerp(albedo,stone*half3(1.04,.98,.89),rock*.68);
                    float height = (meso*.0008*fineFade + fine*.0002*fineFade + paintValue*.0015*grass + stoneValue*.012*rock) * _DetailStrength;
                    float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
                    float3 rx = cross(dy,normal), ry = cross(normal,dx);
                    float determinant = dot(dx,rx);
                    float3 gradient = (rx*ddx(height)+ry*ddy(height)) * sign(determinant)/max(abs(determinant),1e-7);
                    normal = normalize(normal-gradient);
                #endif
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
