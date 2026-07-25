Shader "PS2/PS2Surface"
{
    // PS2-style surface shader for URP:
    //  - Vertex snapping (geometry "wobble")
    //  - Affine texture mapping (the classic PS2 texture warp)
    //  - Simple main-light diffuse + scene fog
    // Apply this to a Material and assign it to your objects.
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _SnapResolution ("Vertex Snap Resolution", Range(8, 640)) = 120
        _AffineAmount ("Affine Warp (0=off,1=full PS2)", Range(0,1)) = 1
        _LightBoost ("Light Boost", Range(0,2)) = 1
        _Ambient ("Ambient", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _SnapResolution;
                float _AffineAmount;
                float _LightBoost;
                float _Ambient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                noperspective float2 uvAffine : TEXCOORD0; // affine (warped) UVs
                float2 uvPersp    : TEXCOORD1;             // correct UVs
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                float4 color      : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float4 posCS = TransformWorldToHClip(posWS);

                // ---- Vertex snapping: quantize NDC position to a low-res grid ----
                float grid = max(_SnapResolution, 1.0);
                float2 ndc = posCS.xy / posCS.w;
                ndc = round(ndc * grid) / grid;
                posCS.xy = ndc * posCS.w;

                OUT.positionCS = posCS;

                float2 uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.uvAffine = uv;
                OUT.uvPersp  = uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogCoord = ComputeFogFactor(posCS.z);
                OUT.color    = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // blend perspective-correct and affine UVs for adjustable warp
                float2 uv = lerp(IN.uvPersp, IN.uvAffine, _AffineAmount);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor * IN.color;

                // cheap diffuse from the main directional light
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                float3 lit = tex.rgb * (_Ambient + ndotl * _LightBoost) * mainLight.color;

                lit = MixFog(lit, IN.fogCoord);
                return half4(lit, tex.a);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
