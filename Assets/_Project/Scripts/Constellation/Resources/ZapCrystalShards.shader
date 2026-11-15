Shader "Zap/Crystal Shards"
{
    Properties { _Opacity ("Opacity",Range(0,1))=1 _PreviewTex ("Destination",2D)="white" {} _HasPreview ("Prepared",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_PreviewTex); SAMPLER(sampler_PreviewTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _PreviewTex_ST;
            float _Opacity, _HasPreview;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float2 edges:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float2 edges:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.color=i.color; o.uv=i.uv; o.edges=i.edges; return o;
            }
            half4 frag(Varyings i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 world=SAMPLE_TEXTURE2D(_PreviewTex,sampler_PreviewTex,i.uv).rgb;
                float distance=min(min(i.edges.x,i.edges.y),1-i.edges.x-i.edges.y);
                float edge=1-smoothstep(0,max(.002,fwidth(distance)*1.4),distance);
                half3 glass=lerp(i.color.rgb,world*.7+i.color.rgb*.3,_HasPreview);
                return half4(lerp(glass,half3(.65,.85,1),edge),_Opacity*(.23+edge*.65));
            }
            ENDHLSL
        }
    }
}
