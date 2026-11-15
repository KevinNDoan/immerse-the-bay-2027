Shader "Zap/Crystal"
{
    Properties
    {
        _PreviewTex ("Destination", 2D) = "black" {}
        _HasPreview ("Prepared", Float) = 0
        _Focus ("Held emphasis", Range(0,1)) = 0
        _Recording ("Recording", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_PreviewTex); SAMPLER(sampler_PreviewTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _PreviewTex_ST;
            float _HasPreview, _Focus, _Recording;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionOS:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.positionOS=i.positionOS.xyz;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normalWS), view=normalize(GetWorldSpaceViewDir(i.positionWS));
                float facing=saturate(dot(n,view));
                float fresnel=pow(1-facing,4);
                // Trace a refracted ray to the far inner wall of the sphere.
                float3 ray=normalize(TransformWorldToObjectDir(refract(-view,n,1.0/1.33)));
                float3 p=normalize(i.positionOS)*.5;
                float3 interior=normalize(p+ray*max(.001,-2*dot(p,ray)));
                float2 uv=float2(atan2(interior.x,interior.z)/TWO_PI+.5,asin(clamp(interior.y,-1,1))/PI+.5);
                half3 destination=SAMPLE_TEXTURE2D(_PreviewTex,sampler_PreviewTex,uv).rgb;
                float mist=sin(interior.x*12+interior.y*8+_Time.y*.6)*sin(interior.z*9-interior.y*5-_Time.y*.4);
                half3 forming=lerp(half3(.025,.035,.09),half3(.12,.22,.35),mist*.5+.5);
                half3 color=lerp(forming,destination,_HasPreview);
                color*=.88+.12*facing;
                // Broad glass reflections and a fine rim remain visible without HDR or bloom.
                float glint=pow(saturate(dot(n,normalize(float3(-.45,.65,-.6)))),90);
                float soft=pow(saturate(dot(n,normalize(float3(.7,.3,-.6)))),24)*.24;
                color=lerp(color,half3(.57,.76,1),fresnel*.65);
                color+=half3(.83,.94,1)*(glint*.85+soft);
                float rim=pow(1-facing,14);
                color+=half3(.38,.62,1)*rim*(.45+_Focus*.3);
                float alpha=lerp(.17,.94,_HasPreview)+fresnel*.65+glint*.6;
                color += _Recording * half3(.1,.48,.75) * (.7+.15*sin(_Time.y*5));
                alpha = max(alpha, _Recording*.8);
                return half4(saturate(color),saturate(alpha));
            }
            ENDHLSL
        }
    }
}
