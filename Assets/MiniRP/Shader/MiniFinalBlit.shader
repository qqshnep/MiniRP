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

            float4 _BlitScaleBias;

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;

                o.posCS = GetFullScreenTriangleVertexPosition(vertexID);
                
                float2 uv = GetFullScreenTriangleTexCoord(vertexID);
                uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                o.uv = uv;

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