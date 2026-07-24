// World-space planar sand shader (URP).
// Ignores the mesh's UVs entirely - samples the sand texture by world XZ,
// so meshes with broken/radial UVs (like downloaded dune models) still get
// clean, even texturing. Includes a DepthOnly pass so depth-based effects
// (like Volumetric Fog 2) work correctly.
Shader "Drifter/SandWorld"
{
    Properties
    {
        _BaseMap ("Sand Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _TileSize ("Meters Per Tile", Float) = 15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _TileSize;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 uv = input.positionWS.xz / max(_TileSize, 0.01);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

                // The source texture has bright white speckles (it's a snow/
                // noise map). Use only its LUMINANCE as a subtle sand-grain
                // variation around the tint, so pure-white texels never pop -
                // the surface is fundamentally the sandy _BaseColor.
                half lum = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half grain = lerp(0.82, 1.06, lum);          // narrow band, no white
                half3 albedo = _BaseColor.rgb * grain;

                float3 n = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                // Soft two-sided lighting: lights up- and down-facing faces
                // by angle MAGNITUDE (no hard normal flip), so imported flipped
                // meshes aren't dark AND intersections don't get a bright seam.
                half nl = dot(n, mainLight.direction);
                half ndotl = max(saturate(nl), 0.22);   // front-lit only + flat fill, no bright back-face seam
                // Ambient from the REAL normal: down-facing faces (undersides)
                // get the darker ground ambient, not the bright sky, so they
                // don't look pale/washed - they blend into shadow.
                half3 ambient = SampleSH(n);
                half3 color = albedo
                            * (mainLight.color * mainLight.shadowAttenuation * ndotl + ambient);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half frag (Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return o;
            }

            half4 frag (Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
