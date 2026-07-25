Shader "PS2/PS2PostEffect"
{
    // PS2-style full-screen post effect for URP's built-in
    // "Full Screen Pass Renderer Feature":
    //   - Pixelation (low-res sampling grid)
    //   - Ordered (Bayer 4x4) dithering
    //   - Color-depth quantization (banding)
    // Create a Material with this shader, then add a Full Screen Pass
    // Renderer Feature to your URP Renderer and assign the material.
    Properties
    {
        _PixelWidth  ("Pixel Columns", Float) = 320
        _PixelHeight ("Pixel Rows", Float) = 240
        _ColorLevels ("Color Levels per channel", Range(2, 64)) = 16
        _DitherAmount ("Dither Amount", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "PS2Post"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelWidth;
            float _PixelHeight;
            float _ColorLevels;
            float _DitherAmount;

            // 4x4 Bayer ordered-dither matrix (0..15 normalized)
            static const float bayer4x4[16] = {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            half4 frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // ---- Pixelation: snap UV to a low-res grid ----
                float2 grid = float2(max(_PixelWidth, 1.0), max(_PixelHeight, 1.0));
                float2 pUV = (floor(uv * grid) + 0.5) / grid;

                half3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pUV).rgb;

                // ---- Ordered dithering based on the pixel cell ----
                int2 cell = int2(fmod(floor(uv * grid), 4.0));
                float threshold = bayer4x4[cell.y * 4 + cell.x] - 0.5;
                col += (threshold / _ColorLevels) * _DitherAmount;

                // ---- Color quantization (16-bit-ish banding) ----
                col = floor(col * _ColorLevels + 0.5) / _ColorLevels;

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
