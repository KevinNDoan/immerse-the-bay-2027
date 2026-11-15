Shader "Zap/Frost"
{
    // Frosted glass for UI panels, cheap enough for Quest: instead of blurring the live scene,
    // it shows a heavily blurred copy of the sky panorama in the direction seen through the panel,
    // under a white tint, with a soft inner glow and a faint bright top edge. Flat colour, no gradients in the art.
    Properties
    {
        _Size ("Panel size (m)", Vector) = (.3,.2,0,0)
        _Radius ("Corner radius (m)", Float) = .012
        _Tint ("Tint", Color) = (1,1,1,.3)
        _SkyTex ("Sky panorama", 2D) = "white" {}
        _SkyLod ("Sky blur (mip level)", Float) = 6
        _Frost ("Sky seen through, 0 to 1", Range(0,1)) = 1
        _Glow ("Inner glow width (m)", Float) = .01
        _GlowStrength ("Inner glow strength", Range(0,1)) = .55
        _EdgeStrength ("Top edge strength", Range(0,1)) = .6
        _Opacity ("Opacity", Range(0,1)) = .92
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            // Writes depth so text behind a panel is hidden by it instead of showing through.
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_SkyTex);
            SAMPLER(sampler_SkyTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Size;
            float _Radius;
            half4 _Tint;
            float4 _SkyTex_ST;
            float _SkyLod;
            half _Frost;
            float _Glow;
            half _GlowStrength;
            half _EdgeStrength;
            half _Opacity;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 halfSize = _Size.xy * .5;
                float radius = min(_Radius, min(halfSize.x, halfSize.y));
                float2 p = (input.uv - .5) * _Size.xy;
                float2 q = abs(p) - halfSize + radius;
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius; // negative inside the panel
                float aa = max(fwidth(d), 1e-5);
                float mask = 1 - smoothstep(-aa, aa, d);
                clip(mask - .01);

                // Blurred sky behind the panel: look up the panorama along the view ray at a high mip level.
                float3 dir = normalize(input.positionWS - _WorldSpaceCameraPos);
                float2 skyUv = float2(atan2(dir.x, dir.z) * (.5 * INV_PI) + .5, asin(clamp(dir.y, -1, 1)) * INV_PI + .5);
                half3 sky = SAMPLE_TEXTURE2D_LOD(_SkyTex, sampler_SkyTex, skyUv, _SkyLod).rgb;
                half3 color = lerp(half3(.82, .83, .86), sky, _Frost);
                color = lerp(color, _Tint.rgb, _Tint.a);

                // Soft inner glow instead of a hard outline, and a faint bright top edge.
                float glow = exp(d / max(_Glow, 1e-4)) * _GlowStrength;
                color = lerp(color, half3(1, 1, 1), saturate(glow));
                float top = saturate(p.y / halfSize.y);
                float edge = exp(d / max(aa * 2.5, .0015)) * top * top * _EdgeStrength;
                color = lerp(color, half3(1, 1, 1), saturate(edge));
                return half4(color, mask * _Opacity);
            }
            ENDHLSL
        }
    }
}
