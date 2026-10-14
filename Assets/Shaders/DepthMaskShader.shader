Shader "Custom/DepthMask"
{
    SubShader
    {
        // Отрисовывается ДО стандартных объектов (Geometry - 1)
        Tags { "Queue" = "Geometry-1" "RenderType" = "Opaque" }

        // Отключаем запись цвета (объект полностью невидим)
        ColorMask 0
        
        // Включаем запись в буфер глубины
        ZWrite On

        Pass
        {
            // Пустой пасс: пишется только глубина
        }
    }
}