Shader "MiniRP/FinalBlit"
{
    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_CameraColorTexture);
            SAMPLER(sampler_CameraColorTexture);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;

                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                o.uv.y = 1.0 - o.uv.y; // 翻转
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, input.uv);
            }

            ENDHLSL
        }
    }
}