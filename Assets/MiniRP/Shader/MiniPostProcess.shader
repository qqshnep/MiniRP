Shader "MiniRP/PostProcess"
{
    Properties
    {
    }

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

            TEXTURE2D(_SourceTexture);
            SAMPLER(sampler_SourceTexture);

            float4 _BlitScaleBias;

            float _Exposure;


            TEXTURE2D(_BloomTexture);
            SAMPLER(sampler_BloomTexture);
            float _BloomIntensity;

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };


            float3 ToneMapACES(float3 x)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;

                return saturate(
                    (x * (a * x + b)) /
                    (x * (c * x + d) + e)
                );
            }


            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;

                o.posCS = GetFullScreenTriangleVertexPosition(vertexID);

                float2 uv = GetFullScreenTriangleTexCoord(vertexID);

                o.uv = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;

                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 source = SAMPLE_TEXTURE2D(_SourceTexture, sampler_SourceTexture, input.uv).rgb;
                
                float3 bloom = SAMPLE_TEXTURE2D(_BloomTexture, sampler_BloomTexture, input.uv).rgb;

                float3 color = source + bloom * _BloomIntensity;

                //曝光
                float exposureMultiplier = exp2(_Exposure);
                color *= exposureMultiplier;

                //HDR 调试
                // {
                //     float maxChannel = max( color.r, max(color.g, color.b));

                //     if (maxChannel > 1.0)
                //     {
                //         return float4(1,0,0,1);
                //     }

                //     return float4(0,0,0,1);
                // }


                //Tone Mapping -- Reinhard
                //color = color / (1 + color);

                //Tone Mapping -- ACES-ish
                color = ToneMapACES(color);

                //return float4(bloom,1);
                return float4(color, 1);
            }

            ENDHLSL
        }
    }
}