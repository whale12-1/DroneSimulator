// HorizonTreeSilhouette.shader
// Alpha-clipped, double-sided, matte shader for the combined far-forest card mesh.
//
// Design goals (AMD Radeon 780M / Unity 6 URP Forward+):
//   * One texture sample (_BaseMap: a 4-row atlas made from the existing SpeedTree
//     billboard textures), one UnityPerMaterial CBUFFER (SRP Batcher compatible).
//   * No shadows (the renderer has shadow casting disabled), no normal maps, no
//     specular: distant forest only needs a believable silhouette and value.
//   * Vertex colour carries the per-card tint so a single draw call can vary the
//     forest without extra material properties.
//   * Same shader-level aerial perspective as HorizonFields (global fog stays off).
Shader "DroneSimulator/HorizonTreeSilhouette"
{
    Properties
    {
        [MainTexture] _BaseMap("Forest Strip Atlas (sRGB)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.32
        _RootDarken("Root Darken", Range(0.0, 1.0)) = 0.55

        [Header(Aerial Perspective)]
        _HazeColor("Haze Color (linear)", Color) = (0.55, 0.60, 0.68, 1)
        _HazeStart("Haze Start (meters)", Float) = 2600
        _HazeEnd("Haze End (meters)", Float) = 12000
        _HazeStrength("Haze Strength", Range(0.0, 1.0)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _HazeColor;
            half _AlphaCutoff;
            half _RootDarken;
            float _HazeStart;
            float _HazeEnd;
            half _HazeStrength;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        struct ForestAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct ForestVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half4 color : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        ForestVaryings ForestVertex(ForestAttributes input)
        {
            ForestVaryings output = (ForestVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.color = input.color;
            return output;
        }

        half4 ForestFragment(ForestVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            half alpha = baseSample.a * _BaseColor.a;
            clip(alpha - _AlphaCutoff);

            // Each atlas row is exactly 0.25 of the texture height, so the local card
            // height can be recovered from the final V coordinate.
            half heightFraction = saturate(frac(input.uv.y * 4.0));
            half rootAO = lerp(1.0 - _RootDarken, 1.0, smoothstep(0.0, 0.42, heightFraction));

            half3 albedo = baseSample.rgb * _BaseColor.rgb * input.color.rgb * rootAO;

            Light mainLight = GetMainLight();
            half3 normalWS = normalize(input.normalWS);
            half nDotL = saturate(dot(normalWS, mainLight.direction));
            half3 ambient = SampleSH(normalWS);
            half3 color = albedo * (ambient + mainLight.color * nDotL);

            float cameraDistance = distance(input.positionWS, GetCameraPositionWS());
            half haze = smoothstep(_HazeStart, max(_HazeEnd, _HazeStart + 1.0), cameraDistance) * _HazeStrength;
            color = lerp(color, _HazeColor.rgb, haze);

            return half4(color, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ForestVertex
            #pragma fragment ForestFragment

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
            #pragma vertex ForestDepthVertex
            #pragma fragment ForestDepthFragment
            #pragma multi_compile_instancing

            struct ForestDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ForestDepthVaryings ForestDepthVertex(ForestAttributes input)
            {
                ForestDepthVaryings output = (ForestDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ForestDepthFragment(ForestDepthVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _AlphaCutoff);
                return half4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
