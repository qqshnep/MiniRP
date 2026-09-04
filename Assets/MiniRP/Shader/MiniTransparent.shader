Shader "MiniRP/MiniTransparent"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 0.5)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "MiniRPUnlit"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

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

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.posCS = UnityObjectToClipPos(input.posOS);

                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }

            ENDHLSL
        }
    }
}