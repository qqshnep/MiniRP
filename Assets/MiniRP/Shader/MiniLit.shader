Shader "MiniRP/MiniLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "MiniRPLit"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 posOS : POSITION;

                // 新增
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;

                // 新增,世界空间计算光照
                float3 normalWS : TEXCOORD0;
            };


            // =========================
            // Material Data
            // =========================

            CBUFFER_START(UnityPerMaterial)

            float4 _BaseColor;

            CBUFFER_END


            // =========================
            // Pipeline Global Data
            // =========================

            float4 _MainLightDirection;
            float4 _MainLightColor;


            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.posCS = UnityObjectToClipPos(input.posOS);

                output.normalWS = UnityObjectToWorldNormal(input.normalOS);

                return output;
            }


            float4 Frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);

                float3 L = normalize(_MainLightDirection.xyz);

                //Lambert
                float NdotL = saturate(dot(N, L));

                float3 color = _BaseColor.rgb * _MainLightColor.rgb * NdotL;

                return float4( color, _BaseColor.a);
            }

            ENDHLSL
        }
    }
}