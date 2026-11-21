Shader "Custom/RealisticNightVision"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1.0
        _NoiseAmount ("Noise Amount", Float) = 0.5
        _VignetteRadius ("Vignette Radius", Float) = 0.8
        _NVColor ("NV Color", Color) = (0.2, 1.0, 0.3, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "NightVisionPass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _Intensity;
            float _NoiseAmount;
            float _VignetteRadius;
            half4 _NVColor;

            // Функция генерации шума
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. Читаем пиксель с экрана
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                
                // 2. Считаем яркость (Luminance)
                float lum = dot(sceneColor.rgb, float3(0.299, 0.587, 0.114));

                // 3. Усиление света (Light Amplification) и ограничение пиков (Clamping)
                // Возводим в степень для усиления темных участков и ограничиваем до 1.0
                lum = pow(saturate(lum), 0.6) * _Intensity;
                lum = min(lum, 1.0);

                // 4. Окрашиваем в цвет ПНВ (обычно зеленый)
                half3 nvColor = lum * _NVColor.rgb;

                // 5. Добавляем матричный шум (зависит от времени и яркости)
                float noise = hash(uv * _ScreenParams.xy + _Time.y * 50.0);
                // В темноте шум сильнее
                float dynamicNoise = noise * _NoiseAmount * (1.0 - lum); 
                nvColor += dynamicNoise;

                // 6. Виньетка (эффект объектива)
                float2 centerDist = uv - 0.5;
                float vignette = smoothstep(_VignetteRadius, _VignetteRadius - 0.2, length(centerDist));
                nvColor *= vignette;

                // 7. Хроматическая аберрация (смещение каналов к краям)
                float chroma = length(centerDist) * 0.005;
                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(chroma, 0)).r;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(chroma, 0)).b;
                nvColor.r += r * vignette * 0.3 * _NVColor.r;
                nvColor.b += b * vignette * 0.3 * _NVColor.b;

                return half4(nvColor, 1.0);
            }
            ENDHLSL
        }
    }
}