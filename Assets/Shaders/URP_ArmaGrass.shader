// URP_ArmaGrass.shader
// Optimized two-sided terrain vegetation shader for Unity 6 URP (Forward+).
//
// Design goals (AMD Radeon 780M / integrated GPU):
//   * SRP Batcher compatible: every material property lives in one UnityPerMaterial CBUFFER.
//   * GPU Instancing / Terrain Detail Instancing: #pragma multi_compile_instancing in every pass.
//   * Exactly one texture sample in the fragment shader (_BaseMap). No normal/mask/thickness maps,
//     no specular, no smoothness - vegetation is completely matte.
//   * No ShadowCaster pass (grass never casts shadows). DepthOnly remains for Depth Priming.
//   * Soft up-facing normals, fake root AO, cheap sun backlight, macro tone variation,
//     distance sink + soil color fade and wind cutoff for a clean far boundary.
Shader "DroneSimulator/URP_ArmaGrass"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGBA)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Alpha)]
        _AlphaClipThreshold("Alpha Clip Threshold", Range(0.0, 1.0)) = 0.38

        [Header(Lighting)]
        _NormalUpBlend("Normal Up Blend", Range(0.0, 1.0)) = 0.95
        _GroundFalloff("Ground Falloff (fake root AO)", Range(0.0, 1.0)) = 0.45
        _BacklightStrength("Backlight Strength", Range(0.0, 2.0)) = 0.25
        _ReceiveShadows("Receive Shadows", Range(0.0, 1.0)) = 1.0

        [Header(Macro Variation)]
        _MacroScale("Macro Scale (meters)", Range(1.0, 200.0)) = 35.0
        _MacroVariationStrength("Macro Tint Strength", Range(0.0, 1.0)) = 0.18
        _MacroBrightnessStrength("Macro Brightness Strength", Range(0.0, 1.0)) = 1.0
        _MacroTintColor("Macro Tint Color", Color) = (0.92, 1.0, 0.88, 1)

        [Header(Distance Fade)]
        _FadeStart("Fade Start (meters)", Float) = 130.0
        _FadeEnd("Fade End (meters)", Float) = 190.0
        _FadeColor("Distance Fade Color (soil)", Color) = (0.2901961, 0.3058824, 0.2117647, 1)

        [Header(Wind)]
        _WindStrength("Wind Strength", Range(0.0, 1.0)) = 0.15
        _WindSpeed("Wind Speed", Range(0.0, 10.0)) = 1.4
        _WindCutoffStart("Wind Cutoff Start (meters)", Float) = 35.0
        _WindCutoffEnd("Wind Cutoff End (meters)", Float) = 55.0
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

        // Single SRP Batcher compatible material buffer shared by every pass.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _MacroTintColor;
            half4 _FadeColor;
            half _AlphaClipThreshold;
            half _NormalUpBlend;
            half _GroundFalloff;
            half _BacklightStrength;
            half _MacroScale;
            half _MacroVariationStrength;
            half _MacroBrightnessStrength;
            float _FadeStart;
            float _FadeEnd;
            float _WindCutoffStart;
            float _WindCutoffEnd;
            half _WindStrength;
            half _WindSpeed;
            half _ReceiveShadows;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        struct GrassAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Cheap hash / value noise. Evaluated per vertex only, so two octaves are affordable.
        float Hash12(float2 p)
        {
            float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash12(i);
            float b = Hash12(i + float2(1.0, 0.0));
            float c = Hash12(i + float2(0.0, 1.0));
            float d = Hash12(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        float MacroNoise(float2 worldXZ)
        {
            float2 p = worldXZ / max(_MacroScale, 1.0);
            return saturate(ValueNoise(p) * 0.72 + ValueNoise(p * 2.17 + 17.31) * 0.28);
        }

        // Applies wind sway and distance fade (vertical sink toward the root) and returns the
        // final world position. Used by both the forward and the depth pass so the depth
        // prepass matches the visible geometry exactly (required for Depth Priming).
        void ApplyGrassDeformation(
            float3 positionOS,
            float2 uv,
            out float3 positionWS,
            out float distToCam,
            out float fade)
        {
            positionWS = TransformObjectToWorld(positionOS);
            distToCam = distance(positionWS, GetCameraPositionWS());

            fade = smoothstep(_FadeStart, max(_FadeEnd, _FadeStart + 0.01), distToCam);

            // Cut wind animation off by distance: no sub-pixel shimmer far away.
            float windFade = 1.0 - smoothstep(
                _WindCutoffStart,
                max(_WindCutoffEnd, _WindCutoffStart + 0.01),
                distToCam);
            float windMask = uv.y * uv.y * windFade * _WindStrength;
            float windPhase = _Time.y * _WindSpeed + positionWS.x * 0.37 + positionWS.z * 0.29;
            float windWave = sin(windPhase) * 0.65 + sin(windPhase * 1.73 + 1.7) * 0.35;
            positionWS.x += windWave * windMask;
            positionWS.z += windWave * windMask * 0.55;

            // Sink the tuft toward its base (uv.y weighted) so the far boundary melts into
            // the terrain instead of ending with a hard line.
            float3 baseWS = TransformObjectToWorld(float3(positionOS.x, 0.0, positionOS.z));
            positionWS.y = lerp(positionWS.y, baseWS.y, fade * saturate(uv.y));
        }

        struct VaryingsForward
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 macroTint : TEXCOORD3;     // rgb = tint, a = brightness
            half3 fadeHeightFog : TEXCOORD4; // x = distance fade, y = height (uv.y), z = fog
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        VaryingsForward ArmaGrassForwardVertex(GrassAttributes input)
        {
            VaryingsForward output = (VaryingsForward)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionWS;
            float distToCam;
            float fade;
            ApplyGrassDeformation(input.positionOS.xyz, input.uv, positionWS, distToCam, fade);

            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

            // Blend the per-vertex normal toward world up. Crossed grass quads stop showing
            // hard shading seams and the whole carpet reads much softer without any extra
            // texture samples.
            half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.normalWS = normalize(lerp(normalWS, half3(0.0, 1.0, 0.0), _NormalUpBlend));

            float macroNoise = MacroNoise(positionWS.xz);
            half macroTintAmount = (half)macroNoise * _MacroVariationStrength;
            half3 macroTint = lerp(half3(1.0, 1.0, 1.0), _MacroTintColor.rgb, macroTintAmount);
            half macroBrightness = (half)lerp(
                1.0,
                lerp(0.88, 1.08, macroNoise),
                saturate(_MacroBrightnessStrength));
            output.macroTint = half4(macroTint, macroBrightness);

            output.fadeHeightFog = half3(
                (half)fade,
                (half)input.uv.y,
                (half)ComputeFogFactor(output.positionCS.z));

            return output;
        }

        half4 ArmaGrassForwardFragment(VaryingsForward input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            // The single texture sample of this shader.
            half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

            half alpha = baseSample.a * _BaseColor.a;
            clip(alpha - _AlphaClipThreshold);

            half fade = input.fadeHeightFog.x;
            half height = input.fadeHeightFog.y;

            // Fake root AO: darken the bottom 30-40% of the tuft so it grows into the soil.
            half rootAO = lerp(1.0 - _GroundFalloff, 1.0, smoothstep(0.0, 0.45, height));

            half3 albedo = baseSample.rgb * _BaseColor.rgb;
            albedo *= input.macroTint.rgb * input.macroTint.a;
            albedo *= rootAO;
            albedo = lerp(albedo, _FadeColor.rgb, fade);

            half3 normalWS = normalize(input.normalWS);
            half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            half shadow = lerp(1.0, MainLightRealtimeShadow(shadowCoord), saturate(_ReceiveShadows));

            Light mainLight = GetMainLight();
            half NdotL = saturate(dot(normalWS, mainLight.direction));
            half3 ambient = SampleSH(normalWS);
            half3 lighting = ambient + mainLight.color * (NdotL * shadow);

            half3 color = albedo * lighting;

            // Cheap backlight instead of a ThicknessMap: tips glow slightly when looking
            // toward the sun. Fades out in shadow and with the same distance fade.
            half backlight = pow(saturate(dot(viewDirWS, -mainLight.direction)), 3.0);
            backlight *= height * _BacklightStrength * shadow;
            color += albedo * mainLight.color * backlight;

            color = MixFog(color, input.fadeHeightFog.z);
            return half4(color, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ArmaGrassForwardVertex
            #pragma fragment ArmaGrassForwardFragment

            // Sun shadows + soft shadow filtering.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // URP 6.1+ Forward+ keyword (the old _FORWARD_PLUS keyword is deprecated).
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ArmaGrassDepthVertex
            #pragma fragment ArmaGrassDepthFragment
            #pragma multi_compile_instancing

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepth ArmaGrassDepthVertex(GrassAttributes input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS;
                float distToCam;
                float fade;
                ApplyGrassDeformation(input.positionOS.xyz, input.uv, positionWS, distToCam, fade);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ArmaGrassDepthFragment(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _AlphaClipThreshold);
                return half4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}

