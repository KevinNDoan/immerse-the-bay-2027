Shader "Zap/GaussianEnvironment"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Splat { float4 positionAlpha; float4 covarianceA; float4 covarianceB; float4 color; };
            StructuredBuffer<Splat> _Splats;
            StructuredBuffer<uint> _Order;
            struct Attributes { float3 positionOS:POSITION; uint vertexID:SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 gaussian:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                Splat s=_Splats[_Order[input.vertexID/4]];
                float4 view=mul(UNITY_MATRIX_MV,float4(s.positionAlpha.xyz,1));
                o.positionCS=mul(UNITY_MATRIX_P,view);
                if(view.z>-.05 || s.positionAlpha.w<.004) { o.positionCS=float4(2,2,2,1); return o; }
                float3x3 cov=float3x3(s.covarianceA.x,s.covarianceA.y,s.covarianceA.z,
                    s.covarianceA.y,s.covarianceA.w,s.covarianceB.x,
                    s.covarianceA.z,s.covarianceB.x,s.covarianceB.y);
                float3x3 mv=(float3x3)UNITY_MATRIX_MV;
                cov=mul(mul(mv,cov),transpose(mv));
                // Keep projection signs: render textures and Vulkan can invert Y.
                // Taking abs mirrors each ellipse while leaving its center in place.
                float2 focal=float2(UNITY_MATRIX_P._m00,UNITY_MATRIX_P._m11)*_ScreenParams.xy*.5;
                float2 limits=1.3/abs(float2(UNITY_MATRIX_P._m00,UNITY_MATRIX_P._m11));
                view.xy=clamp(view.xy/view.z,-limits,limits)*view.z;
                float invZ=rcp(view.z);
                float3 jx=float3(-focal.x*invZ,0,focal.x*view.x*invZ*invZ);
                float3 jy=float3(0,-focal.y*invZ,focal.y*view.y*invZ*invZ);
                float a=dot(jx,mul(cov,jx))+.3;
                float b=dot(jx,mul(cov,jy));
                float c=dot(jy,mul(cov,jy))+.3;
                float mid=(a+c)*.5, disc=sqrt(max(0,(a-c)*(a-c)*.25+b*b));
                float2 eigen=max(float2(mid+disc,mid-disc),.1);
                float2 axis=abs(b)>.00001 ? normalize(float2(b,eigen.x-a)) : (a>=c?float2(1,0):float2(0,1));
                uint corner=input.vertexID%4;
                float2 q=float2(corner&1,(corner>>1)&1)*2-1;
                float2 radius=min(3*sqrt(eigen),256);
                float2 delta=q.x*radius.x*axis+q.y*radius.y*float2(-axis.y,axis.x);
                o.positionCS.xy+=delta*2/_ScreenParams.xy*o.positionCS.w;
                o.gaussian=q*3;
                float3 color=s.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    color=SRGBToLinear(color);
                #endif
                o.color=half4(color,s.positionAlpha.w);
                return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float power=dot(input.gaussian,input.gaussian);
                clip(9-power);
                half alpha=min(.99,input.color.a*exp(-.5*power)); clip(alpha-.004);
                return half4(input.color.rgb*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
