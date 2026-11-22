// HorizonFields.shader
// Matte, SRP Batcher compatible shader for the low-poly far-horizon ring mesh.
//
// Design goals (AMD Radeon 780M / Unity 6 URP Forward+):
//   * All material properties live in one UnityPerMaterial CBUFFER (SRP Batcher).
//   * Albedo comes from a 4K macro field/forest-belt map sampled in world space
//     (no UVs on the mesh) plus one tiled linear micro-noise map at three scales.
//   * No normal maps, no specular/smoothness, no shadow sampling: pure Lambert + SH
//     ambient, which keeps the far ground as cheap as the terrain it replaces.
//   * Local aerial perspective (shader-level haze, NOT RenderSettings fog) fades the
//     distant ridges into the overcast sky without touching the scene fog settings.
//   * A dedicated near-seam blend hides the resolution drop where the mesh meets the
//     main Terrain (the mesh carries "distance from terrain border" in UV0.x).
Shader "DroneSimulator/HorizonFields"
{
    Properties
    {
        [MainTexture] _MacroMap("Macro Fields Map (sRGB)", 2D) = "gray" {}
        _DetailMap("Detail Noise (linear)", 2D) = "gray" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Macro Map)]
        _MacroCenter("Macro Center (world XZ)", Vector) = (0, 0, 0, 0)
        _MacroSpan("Macro Span (meters)", Float) = 20000

        [Header(Micro Detail)]
        _DetailTilingA("Detail Tiling A (meters)", Float) = 96
        _DetailTilingB("Detail Tiling B (meters)", Float) = 13
        _DetailTilingC("Detail Tiling C (meters)", Float) = 3.1
        _DetailStrengthA("Detail Strength A", Range(0.0, 1.0)) = 0.04
        _DetailStrengthB("Detail Strength B", Range(0.0, 1.0)) = 0.02
        _DetailStrengthC("Detail Strength C", Range(0.0, 1.0)) = 0.01

        [Header(Relief)]
        _SlopeDarken("Slope Darken", Range(0.0, 1.0)) = 0.12

        [Header(Seam Blend)]
        _NearColor("Near Seam Tint", Color) = (1, 1, 1, 1)
        _NearBlendStart("Near Blend Start (meters)", Float) = 0
        _NearBlendEnd("Near Blend End (meters)", Float) = 220

        [Header(Aerial Perspective)]
        _HazeColor("Haze Color (linear)", Color) = (0.55, 0.60, 0.68, 1)
        _HazeStart("Haze Start (meters)", Float) = 1800
        _HazeEnd("Haze End (meters)", Float) = 9000
        _HazeStrength("Haze Strength", Range(0.0, 1.0)) = 0.70
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }

        Cull Back
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MacroMap_ST;
            float4 _MacroCenter;
            half4 _BaseColor;
            half4 _NearColor;
            half4 _HazeColor;
            float _MacroSpan;
            float _DetailTilingA;
            float _DetailTilingB;
            float _DetailTilingC;
            half _DetailStrengthA;
            half _DetailStrengthB;
            half _DetailStrengthC;
            half _SlopeDarken;
            float _NearBlendStart;
            float _NearBlendEnd;
            float _HazeStart;
            float _HazeEnd;
            half _HazeStrength;
        CBUFFER_END

        TEXTURE2D(_MacroMap);
        SAMPLER(sampler_MacroMap);
        TEXTURE2D(_DetailMap);
        SAMPLER(sampler_DetailMap);

        struct HorizonAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;  // x = distance from the main terrain border / 1000
            half4 color : COLOR;    // linear colour of the real Terrain border layer
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct HorizonVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float borderDistance : TEXCOORD2;
            half3 borderColor : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        HorizonVaryings HorizonVertex(HorizonAttributes input)
        {
            HorizonVaryings output = (HorizonVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.borderDistance = input.uv.x * 1000.0;
            output.borderColor = input.color.rgb;
            return output;
        }

        half3 HorizonShade(half3 albedo, half3 normalWS, float3 positionWS)
        {
            Light mainLight = GetMainLight();
            half nDotL = saturate(dot(normalWS, mainLight.direction));
            half3 ambient = SampleSH(normalWS);
            half3 color = albedo * (ambient + mainLight.color * nDotL);

            float cameraDistance = distance(positionWS, GetCameraPositionWS());
            half haze = smoothstep(_HazeStart, max(_HazeEnd, _HazeStart + 1.0), cameraDistance) * _HazeStrength;
            return lerp(color, _HazeColor.rgb, haze);
        }

        half4 HorizonFragment(HorizonVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 macroUV = (input.positionWS.xz - _MacroCenter.xz) / max(_MacroSpan, 1.0) + 0.5;
            half3 macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, macroUV).rgb;

            half detailA = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, input.positionWS.xz / _DetailTilingA).r;
            half detailB = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, input.positionWS.xz / _DetailTilingB + 0.37).r;
            half detailC = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, input.positionWS.xz / _DetailTilingC + 0.71).r;

            half detail = 1.0
                + (detailA * 2.0 - 1.0) * _DetailStrengthA
                + (detailB * 2.0 - 1.0) * _DetailStrengthB
                + (detailC * 2.0 - 1.0) * _DetailStrengthC;

            half3 normalWS = normalize(input.normalWS);
            half slope = 1.0 - saturate(normalWS.y);

            half3 albedo = macro * _BaseColor.rgb * saturate(detail);

            // Gentle slope shading: the flat plain must not turn into a dark strip just
            // behind the seam. Near the border the term is fully suppressed.
            half slopeGuard = smoothstep(150.0, 600.0, input.borderDistance);
            albedo *= 1.0 - (_SlopeDarken * slopeGuard * slope);

            // The mesh carries the real Terrain border layer colour in vertex colour, so
            // the ring continues the visible soil/grass instead of a generic tone. The
            // transition to fields is noise-warped over ~130-310 m.
            half nearVariation = (detailC * 2.0 - 1.0) * 0.05 + (detailB * 2.0 - 1.0) * 0.03;
            half3 nearTerrain = saturate(input.borderColor * _NearColor.rgb) * saturate(1.0 + nearVariation);
            half nearEnd = max(80.0, _NearBlendEnd + (detailB * 2.0 - 1.0) * 90.0);
            half nearBlend = 1.0 - smoothstep(_NearBlendStart, nearEnd, input.borderDistance);
            albedo = lerp(albedo, nearTerrain, nearBlend);

            half3 color = HorizonShade(albedo, normalWS, input.positionWS);
            return half4(color, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HorizonVertex
            #pragma fragment HorizonFragment

            // URP 6.1+ Forward+ keyword (the old _FORWARD_PLUS keyword is deprecated).
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HorizonDepthVertex
            #pragma fragment HorizonDepthFragment
            #pragma multi_compile_instancing

            struct HorizonDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            HorizonDepthVaryings HorizonDepthVertex(HorizonAttributes input)
            {
                HorizonDepthVaryings output = (HorizonDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 HorizonDepthFragment(HorizonDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
