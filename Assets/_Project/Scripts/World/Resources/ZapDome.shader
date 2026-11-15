Shader "Zap/Dome"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _MappingMode ("Panorama or ambient surround", Float) = 0
        _SeedTex ("Shared panoramic background", 2D) = "black" {}
        _Projection ("Four-view atlas", Float) = 0
        _LiveViews ("Available views", Float) = 0
        _LiveReady ("Video received", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
        }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_SeedTex);
            SAMPLER(sampler_SeedTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _MappingMode;
                float _Projection, _LiveViews, _LiveReady;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                if (_Projection > .5)
                {
                    half4 background = SAMPLE_TEXTURE2D(_SeedTex, sampler_SeedTex, uv);
                    if (_MappingMode > .5 || _LiveReady < .5) return background;
                    float longitude = (uv.x - .5) * 6.2831853;
                    float latitude = (uv.y - .5) * 3.14159265;
                    float3 ray = float3(sin(longitude)*cos(latitude), sin(latitude), cos(longitude)*cos(latitude));
                    half3 accumulated = 0;
                    float totalWeight = 0;
                    [unroll] for (int i = 0; i < 4; i++)
                    {
                        if ((((int)_LiveViews) & (1 << i)) == 0) continue;
                        float angle = i * 1.57079633;
                        float3 viewRay = float3(ray.x*cos(angle)-ray.z*sin(angle), ray.y, ray.x*sin(angle)+ray.z*cos(angle));
                        float z = max(.001, viewRay.z);
                        // 110 horizontal degrees, 16:9 rectilinear source views.
                        float2 viewUV = .5 + float2(viewRay.x / (z*1.428148), viewRay.y / (z*.803333)) * .5;
                        float edgeX = .5-abs(viewUV.x-.5), edgeY = .5-abs(viewUV.y-.5);
                        float weight = smoothstep(0,.12,edgeX) * smoothstep(0,.16,edgeY) * step(.01,viewRay.z);
                        float2 atlasUV = (saturate(viewUV) + float2(i % 2, 1-i/2)) * .5;
                        // Keep bilinear samples inside each tile, avoiding atlas-edge leakage.
                        atlasUV = clamp(atlasUV, float2(i%2,1-i/2)*.5+.0006, float2(i%2,1-i/2)*.5+.4994);
                        accumulated += SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,atlasUV).rgb * weight;
                        totalWeight += weight;
                    }
                    half3 live = accumulated / max(.0001,totalWeight);
                    return half4(lerp(background.rgb, live, saturate(totalWeight)),1);
                }
                if (_MappingMode > .5)
                {
                    // A comparison mode for streams that lose the panoramic projection.
                    // Keep the central picture and soften its extension over the full sphere.
                    float2 front = float2((uv.x - .5) * 360.0 / 120.0 + .5,
                                          (uv.y - .5) * 180.0 / 75.0 + .5);
                    float2 mirrored = 1.0 - abs(frac(front * .5) * 2.0 - 1.0);
                    half4 ambient = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mirrored);
                    ambient += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(mirrored + float2(.045,.025)));
                    ambient += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(mirrored - float2(.045,.025)));
                    ambient += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(mirrored + float2(-.045,.025)));
                    ambient *= .25;
                    float edge = min(min(front.x, 1.0-front.x), min(front.y, 1.0-front.y));
                    half4 center = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(front));
                    return lerp(ambient, center, smoothstep(0.0, .12, edge));
                }
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                // Gently soften the back seam and singular poles without repeating the view.
                half4 seam = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(.006,uv.y)) +
                              SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(.994,uv.y))) * .5;
                col = lerp(seam, col, smoothstep(0.0,.012,min(uv.x,1.0-uv.x)));
                half4 pole = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(.125,uv.y)) +
                              SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(.375,uv.y)) +
                              SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(.625,uv.y)) +
                              SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(.875,uv.y))) * .25;
                col = lerp(pole, col, smoothstep(0.0,.035,min(uv.y,1.0-uv.y)));
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
