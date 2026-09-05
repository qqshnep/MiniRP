Shader "MiniRP/MiniLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ( "Smoothness",Range(0,1)) = 0.5
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
            Name "Forward"

            Tags
            {
                "LightMode" = "MiniRPLit"
            }

            ZTest Equal
            ZWrite Off

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
            float _Metallic;
            float _Smoothness;

            CBUFFER_END


            // =========================
            // Pipeline Global Data
            // =========================
            float4 _CameraPositionWS;
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


            // cubemap
            TEXTURECUBE(_EnvironmentCube);
            SAMPLER(sampler_EnvironmentCube);

            float _EnvironmentMipCount;
            float4 _AmbientColor;

            int GetCascadeIndex(float3 positionWS)
            {
                for (int i = 0; i < _CascadeCount; i++)
                {
                    float3 offset = positionWS - _CascadeCullingSpheres[i].xyz;

                    float distanceSquared = dot(offset, offset);

                    if ( distanceSquared < _CascadeCullingSpheres[i].w) //w 在代码里已经做过乘方
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


            //BRDF=漫反射 (Diffuse)+镜面反射 (Specular)
            /*
                             D × F × G
                Specular = ────────
                           4(N·L)(N·V)
            
            */

            float3 FresnelSchlick(float cosTheta, float3 F0)
            {
                return F0 + (1.0 - F0) * pow( 1.0 - cosTheta, 5.0 );
            }

            float DistributionGGX(float NdotH,float roughness)
            {
                float a = roughness * roughness;

                float a2 = a * a;

                float NdotH2 = NdotH * NdotH;

                float denominator = NdotH2 * (a2 - 1.0) + 1.0;

                denominator = PI * denominator * denominator;

                return a2 / max( denominator, 0.00001 );
            }

            float GeometrySchlickGGX( float NdotX, float roughness)
            {
                float r = roughness + 1.0;

                float k = (r * r) / 8.0;

                return NdotX / ( NdotX * (1.0 - k) + k );
            }

            float GeometrySmith( float NdotV, float NdotL, float roughness)
            {
                float ggxV = GeometrySchlickGGX( NdotV, roughness );

                float ggxL = GeometrySchlickGGX( NdotL, roughness );

                return ggxV * ggxL;
            }

            float3 EvaluateBRDF( float3 N, float3 V, float3 L, float3 lightColor, float3 baseColor, float metallic, float roughness)
            {
                float3 H = normalize(L + V);

                float NdotL = saturate(dot(N, L));

                float NdotV = saturate(dot(N, V));

                float NdotH = saturate(dot(N, H));

                float VdotH = saturate(dot(V, H));


                if ( NdotL <= 0.0 || NdotV <= 0.0)
                {
                    return 0;
                }


                float3 F0 = lerp( float3( 0.04, 0.04, 0.04 ), baseColor, metallic );


                float D = DistributionGGX( NdotH, roughness );

                float3 F = FresnelSchlick( VdotH, F0 );

                float G = GeometrySmith( NdotV, NdotL, roughness );


                float3 numerator = D * G * F;

                float denominator = 4.0 * NdotV * NdotL;

                float3 specular = numerator / max( denominator, 0.00001 );


                float3 kS = F;

                float3 kD = (1.0 - kS) * (1.0 - metallic);

                float3 diffuse = kD * baseColor / PI;


                return ( diffuse + specular ) * lightColor * NdotL;
            }

            float3 CalculatePointLightPBR(int lightIndex, float3 positionWS, float3 N, float3 V, float3 basecolor, float metallic, float roughness)
            {
                float3 lightPositionWS = _OtherLightPositions[lightIndex].xyz;
                float3 toLight = lightPositionWS - positionWS;
                float3 L = normalize(toLight);

                float3 lightColor = _OtherLightColors[lightIndex].rgb;
                float3 brdf = EvaluateBRDF(
                    N,
                    V,
                    L,
                    lightColor,
                    basecolor,
                    metallic,
                    roughness);

                //衰减计算
                float distanceSquared = max( dot(toLight, toLight), 0.00001);
                float inverseRangeSquared = _OtherLightParams[lightIndex].x;

                float rangeAttenuation = saturate(1.0 - distanceSquared * inverseRangeSquared);
                rangeAttenuation *= rangeAttenuation;

                return brdf * rangeAttenuation;
            }

            float3 EvaluateEnvironmentSpecular(float3 N, float3 V, float3 F0, float roughness)
            {
                float3 R = reflect(-V, N);

                float mip = roughness * _EnvironmentMipCount;

                float3 environment = SAMPLE_TEXTURECUBE_LOD(
                        _EnvironmentCube,
                        sampler_EnvironmentCube,
                        R,
                        mip
                    ).rgb;

                float NdotV = saturate( dot(N, V) );

                float3 F = FresnelSchlick( NdotV, F0 );

                return environment * F;
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
                float3 V = normalize( _CameraPositionWS.xyz - input.posWS);
                float3 L = normalize(_MainLightDirection.xyz);

                //cubemap 测试
                // {
                //     float3 R = reflect(-V, N);

                //     float roughness = 1.0 - _Smoothness;
                //     float mip = roughness * _EnvironmentMipCount;
                //     //float3 environment = SAMPLE_TEXTURECUBE( _EnvironmentCube, sampler_EnvironmentCube, R ).rgb;
                //     float3 environment = SAMPLE_TEXTURECUBE_LOD( _EnvironmentCube, sampler_EnvironmentCube, R, mip ).rgb;

                //     return float4(environment, 1);
                // }

                float baseColor = _BaseColor.rgb;
                float metallic = _Metallic;
                float roughness = max( 1.0 - _Smoothness, 0.045 );

                float3 F0 = lerp( float3(0.04, 0.04, 0.04), baseColor, metallic );



                //平行光
                float3 directLighting = 0;
                {
                    float mainNdotL  = saturate(dot(N, L));
                    float shadow = GetMainLightShadow(input.posWS, input.normalWS, L);
                    //float3 lighting = _BaseColor.rgb * _MainLightClr.rgb * mainNdotL * shadow;

                    float3 lighting = EvaluateBRDF(
                        N,
                        V,
                        L,
                        baseColor,
                        _BaseColor.rgb,
                        metallic,
                        roughness);

                    directLighting = lighting * shadow;
                }
                

                //点光
                float3 pointLighting = 0;
                {
                    for (int i = 0; i < _OtherLightCount;i++)
                    {
                        pointLighting += CalculatePointLightPBR( i, input.posWS, N, V, _BaseColor.rgb, _Metallic, roughness);
                    }
                }
                

                //间接光
                float3 indirectDiffuse = 0;
                float3 indirectSpecular = 0;
                {
                    float NdotV = saturate(dot(N, V));
                    float3 F = FresnelSchlick( NdotV, F0 );
                    float3 kD = (1.0 - F) * (1.0 - metallic);

                    indirectDiffuse = _AmbientColor.rgb * baseColor * kD;

                    indirectSpecular = EvaluateEnvironmentSpecular( N, V, F0, roughness );
                }

                float3 finalLighting = directLighting + pointLighting + indirectDiffuse + indirectSpecular;
                
                return float4( finalLighting, _BaseColor.a );
                
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

        Pass
        {
            Name "DepthOnly"

            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ZTest LEqual

            // 完全不写颜色
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

            struct Attributes
            {
                float3 posOS : POSITION;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;

                float3 posWS = TransformObjectToWorld(input.posOS);

                output.posCS = TransformWorldToHClip(posWS);

                return output;
            }

            float4 DepthFrag() : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}