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

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

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
            float4 _MainLightClr;


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


            //shadow
            TEXTURE2D_SHADOW(_MainLightShadowmap);
            SAMPLER_CMP(sampler_MainLightShadowmap);
            //float4x4 _MainLightWorldToShadow;
            float _MainLightShadowStrength;
            float _MainLightShadowNormalBias;
            float4 _MainLightShadowmapSize;

            
            //cascade shadow
            #define MAX_CASCADE_COUNT 4
            float4x4 _MainLightShadowMatrices[MAX_CASCADE_COUNT];
            float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
            int _CascadeCount;


            int GetCascadeIndex(float3 positionWS)
            {
                for (int i = 0; i < _CascadeCount; i++)
                {
                    float3 offset = positionWS - _CascadeCullingSpheres[i].xyz;

                    float distanceSquared = dot(offset, offset);

                    if ( distanceSquared < _CascadeCullingSpheres[i].w)
                    {
                        return i;
                    }
                }

                return -1;
            }


            float SampleMainShadow(float3 shadowCoord)
            {
                return SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmap, sampler_MainLightShadowmap, shadowCoord);
            }

            float SampleMainShadowPCF3x3(float3 shadowCoord)
            {
                float2 texelSize =  _MainLightShadowmapSize.xy;

                float shadow = 0.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y) * texelSize;

                        float3 uvz = float3(shadowCoord.xy + offset,shadowCoord.z);
                        shadow += SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmap, sampler_MainLightShadowmap, uvz);
                    }
                }

                return shadow / 9.0;
            }

            float GetMainLightShadow(float3 positionWS, float3 normalWS, float3 lightDirection)
            {
                int cascadeIndex = GetCascadeIndex(positionWS);
                if(cascadeIndex < 0)
                {
                    return 1.0;
                }


                // normal bias
                float NdotL = saturate( dot( normalWS, lightDirection));
                float inverseNdotL = 1.0 - NdotL;
                float3 biasedPositionWS = positionWS + normalWS * _MainLightShadowNormalBias * inverseNdotL;



                //float4 shadowCoord = mul( _MainLightWorldToShadow, float4(biasedPositionWS, 1.0) );
                float4 shadowCoord = mul( _MainLightShadowMatrices[cascadeIndex], float4(biasedPositionWS, 1.0) );

                shadowCoord.xyz /= shadowCoord.w;


                float shadow = SampleMainShadow(shadowCoord.xyz);
                //float shadow = SampleMainShadowPCF3x3(shadowCoord.xyz);

                //1=有光（光源视角可见）
                return lerp(1.0, shadow, _MainLightShadowStrength);
            }



            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 posWS = TransformObjectToWorld(input.posOS.xyz);
                output.posWS = posWS;


                output.posCS = TransformWorldToHClip(posWS);

                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }


            float4 Frag(Varyings input) : SV_Target
            {
                //cascade debug
                // {
                //     int cascadeIndex = GetCascadeIndex(input.posWS);

                //     if (cascadeIndex == 0)
                //     {
                //         return float4(1,0,0,1);
                //     }

                //     if (cascadeIndex == 1)
                //     {
                //         return float4(0,1,0,1);
                //     }

                //     if (cascadeIndex == 2)
                //     {
                //         return float4(0,0,1,1);
                //     }

                //     if (cascadeIndex == 3)
                //     {
                //         return float4(1,1,0,1);
                //     }

                //     return float4(0,0,0,1);
                // }


                float3 N = normalize(input.normalWS);

                //平行光
                float3 L = normalize(_MainLightDirection.xyz);
                float mainNdotL  = saturate(dot(N, L));
                float shadow = GetMainLightShadow(input.posWS, input.normalWS, L);
                float3 lighting = _BaseColor.rgb * _MainLightClr.rgb * mainNdotL * shadow;

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


        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0 //不需要写 Color

            HLSLPROGRAM

            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "UnityCG.cginc"

            CBUFFER_START(UnityPerMaterial)

            float4 _BaseColor;

            CBUFFER_END


            struct Attributes
            {
                float4 posOS : POSITION;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
            };


            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                output.posCS = UnityObjectToClipPos(input.posOS);

                return output;
            }


            float4 ShadowFrag() : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}