Shader "Zap/GlassPane"
{
    Properties
    {
        _Size ("Panel size", Vector) = (.82,.62,0,0)
        _Pending ("Thinking", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float4 _Size;
            float _Pending;
            CBUFFER_END
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.uv=i.uv; return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=(i.uv-.5)*_Size.xy;
                float2 q=abs(p)-_Size.xy*.5+.026;
                float distance=length(max(q,0))+min(max(q.x,q.y),0)-.026;
                float aa=max(fwidth(distance),.0005);
                float mask=1-smoothstep(-aa,aa,distance);
                clip(mask-.01);
                float rim=1-smoothstep(.0015,.0045,-distance);
                float innerRim=exp(-abs(distance+.009)*650)*.18;
                float3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                float sheen=pow(saturate(1-abs(i.uv.x*.65+i.uv.y*.38-.67+view.x*.05)),22);
                half3 tint=lerp(half3(.045,.12,.17),half3(.18,.32,.38),i.uv.y);
                tint+=sheen*.10;
                half3 edge=lerp(half3(.32,.60,.70),half3(.82,.98,1),saturate(i.uv.y*.6+(1-i.uv.x)*.4));
                float pulse=_Pending*(.5+.5*sin(_Time.y*3))*saturate((i.uv.y-.94)*16);
                half3 color=lerp(tint,edge,saturate(rim+innerRim+pulse*.35));
                // Translucent smoked glass, independent of HDR or a costly scene-color copy.
                return half4(color,mask*saturate(.69+sheen*.08+rim*.25+innerRim));
            }
            ENDHLSL
        }
    }
}
