Shader "Zap/Hyperspace"
{
    Properties { _Opacity ("Opacity", Range(0,1)) = 0 _Travel ("Travel", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Cull Front ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Opacity, _Travel;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.direction=i.positionOS.xyz; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);
            }
            half4 frag(Varyings i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.direction);
                float radial=max(.045,length(d.xy));
                float depth=clamp(d.z/radial,-14,20);
                float angle=atan2(d.y,d.x);
                // Periodic angular coordinates keep the seam invisible when looking behind.
                float swirl=angle+depth*.14+sin(depth*.35-_Travel*.3)*.2;
                float2 cloud=float2(cos(swirl)*3.2,sin(swirl)*3.2)+float2(depth*.3-_Travel*.8,depth*.19);
                float fog=noise(cloud)*.4+noise(cloud*2.17)*.3+noise(cloud*4.3)*.2+noise(cloud*9.1)*.1;
                float filament=pow(saturate((fog-.24)*1.65),3)*2.7;
                float ray=pow(saturate(sin(angle*37+sin(angle*7)*3)),24);
                float pulse=pow(saturate(sin(depth*2.3-_Travel*12+angle*9)),12);
                float stars=ray*pulse*smoothstep(-.2,.8,d.z)*.7;
                float core=smoothstep(.987,.9995,d.z);
                half3 color=half3(.002,.004,.018);
                color+=half3(.004,.022,.09)*fog;
                color+=half3(.025,.18,.65)*filament;
                color+=half3(.2,.5,.9)*stars;
                color=lerp(color,half3(.4,.73,.95),core);
                return half4(saturate(color),_Opacity);
            }
            ENDHLSL
        }
    }
}
