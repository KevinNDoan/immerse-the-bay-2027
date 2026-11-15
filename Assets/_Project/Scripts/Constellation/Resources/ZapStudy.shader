Shader "Zap/Study"
{
    Properties { _BaseColor ("Color", Color) = (.3,.8,1,1) _EnvironmentTint ("Environment tint", Color) = (1,1,1,1) _Highlight ("Focus", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EnvironmentTint;
            float _Highlight;
            CBUFFER_END
            Varyings vert(Attributes input) {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); return output;
            }
            half4 frag(Varyings input):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                float diffuse = .45 + .55 * saturate(dot(normal, normalize(float3(-.3,.8,.4))));
                float rim = pow(1-saturate(dot(normal,view)),3);
                float specular = pow(saturate(dot(normal,normalize(view+normalize(float3(-.3,.8,.4))))),32);
                return half4(lerp(_BaseColor.rgb, half3(.75,.95,1), _Highlight*.18) * diffuse
                    + rim * (.14 + _Highlight*.3) + specular * .24, 1);
            }
            ENDHLSL
        }
    }
}
