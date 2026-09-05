Shader "MiniRP/Bloom"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

        TEXTURE2D(_SourceTexture);
        SAMPLER(sampler_SourceTexture);

        float4 _SourceTextureSize;
        float _BloomThreshold;

        struct Varyings
        {
            float4 posCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(uint vertexID : SV_VertexID)
        {
            Varyings o;

            o.posCS = GetFullScreenTriangleVertexPosition(vertexID);

            o.uv = GetFullScreenTriangleTexCoord(vertexID);

            return o;
        }

        ENDHLSL

        Pass
        {
            Name "Bloom Prefilter"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragPrefilter

            float4 FragPrefilter(Varyings input) : SV_Target
            {
                float3 color =SAMPLE_TEXTURE2D(
                        _SourceTexture,
                        sampler_SourceTexture,
                        input.uv
                    ).rgb;

                float brightness = max(color.r,max(color.g, color.b));

                float contribution = max(brightness - _BloomThreshold,0.0);

                contribution /= max(brightness, 0.00001);

                color *= contribution;

                return float4(color, 1.0);
            }

            ENDHLSL
        }


        Pass
        {
            Name "Bloom Horizontal"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragHorizontal

            float4 FragHorizontal(Varyings input) : SV_Target
            {
                float2 texel = _SourceTextureSize.xy;

                float3 color = 0.0;

                color += SAMPLE_TEXTURE2D(
                        _SourceTexture,
                        sampler_SourceTexture,
                        input.uv + float2(-2.0 * texel.x, 0)
                    ).rgb * 0.0625;

                color += SAMPLE_TEXTURE2D(
                        _SourceTexture,
                        sampler_SourceTexture,
                        input.uv + float2(-1.0 * texel.x, 0)
                    ).rgb * 0.25;

                color += SAMPLE_TEXTURE2D(
                        _SourceTexture,
                        sampler_SourceTexture,
                        input.uv
                    ).rgb * 0.375;

                color += SAMPLE_TEXTURE2D(
                        _SourceTexture,
                        sampler_SourceTexture,
                        input.uv + float2(1.0 * texel.x, 0)
                    ).rgb * 0.25;

                color += SAMPLE_TEXTURE2D(
                        _SourceTexture,
                        sampler_SourceTexture,
                        input.uv + float2(2.0 * texel.x, 0)
                    ).rgb * 0.0625;

                return float4(color, 1);
            }

            ENDHLSL
        }

        Pass
    {
        Name "Bloom Vertical"

        HLSLPROGRAM

        #pragma vertex Vert
        #pragma fragment FragVertical

        float4 FragVertical(Varyings input) : SV_Target
        {
            float2 texel = _SourceTextureSize.xy;

            float3 color = 0.0;

            color += SAMPLE_TEXTURE2D(
                    _SourceTexture,
                    sampler_SourceTexture,
                    input.uv + float2(0, -2.0 * texel.y)
                ).rgb * 0.0625;

            color += SAMPLE_TEXTURE2D(
                    _SourceTexture,
                    sampler_SourceTexture,
                    input.uv + float2(0, -1.0 * texel.y)
                ).rgb * 0.25;

            color += SAMPLE_TEXTURE2D(
                    _SourceTexture,
                    sampler_SourceTexture,
                    input.uv
                ).rgb * 0.375;

            color += SAMPLE_TEXTURE2D(
                    _SourceTexture,
                    sampler_SourceTexture,
                    input.uv + float2(0, 1.0 * texel.y)
                ).rgb * 0.25;

            color += SAMPLE_TEXTURE2D(
                    _SourceTexture,
                    sampler_SourceTexture,
                    input.uv + float2(0, 2.0 * texel.y)
                ).rgb * 0.0625;

            return float4(color, 1);
        }

        ENDHLSL
    }
    }
}