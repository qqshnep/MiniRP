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
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;

                // 世界空间计算光照
                float3 posWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
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


            #define MAX_OTHER_LIGHT_COUNT 4
            int _OtherLightCount;
            float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
            float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
            float4 _OtherLightParams[MAX_OTHER_LIGHT_COUNT];

            float3 CalculatePointLight(int lightIndex, float3 positionWS, float3 normalWS)
            {
                float3 lightPositionWS = _OtherLightPositions[lightIndex].xyz;

                float3 toLight = lightPositionWS - positionWS;

                float3 L = normalize(toLight);

                float NdotL = saturate( dot(normalWS, L) );

                //衰减计算
                float distanceSquared = max( dot(toLight, toLight), 0.00001);
                float inverseRangeSquared = _OtherLightParams[lightIndex].x;

                float rangeAttenuation = saturate(1.0 - distanceSquared * inverseRangeSquared);
                rangeAttenuation *= rangeAttenuation;

                return _OtherLightColors[lightIndex].rgb * NdotL * rangeAttenuation;
            }


            Varyings Vert(Attributes input)
            {
                Varyings output;

                float4 posWS = mul(unity_ObjectToWorld, input.posOS);
                output.posWS = posWS.xyz;


                output.posCS = UnityObjectToClipPos(input.posOS);

                output.normalWS = UnityObjectToWorldNormal(input.normalOS);

                return output;
            }


            float4 Frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);

                //平行光
                float3 L = normalize(_MainLightDirection.xyz);
                float mainNdotL  = saturate(dot(N, L));
                float3 lighting = _BaseColor.rgb * _MainLightColor.rgb * mainNdotL;

                //点光
                for (int i = 0; i < _OtherLightCount;i++)
                {
                    lighting += CalculatePointLight( i, input.posWS, N);
                }


                float3 color = _BaseColor.rgb * lighting;

                return float4( color, _BaseColor.a );
            }

            ENDHLSL
        }
    }
}