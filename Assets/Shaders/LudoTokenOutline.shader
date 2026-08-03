Shader "Elemental Ludo/Token Outline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.01, 0.015, 0.02, 1)
        _OutlineWidth("Outline Width (world units)", Range(0, 0.08)) = 0.018
        [Toggle] _TerrainAnimationEnabled("Terrain Animation", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry-1"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LudoTerrainAnimation.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _TerrainAnimationEnabled;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 animationData : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half visibility : TEXCOORD0;
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                output.visibility = 1.0h;
                if (_TerrainAnimationEnabled > 0.5)
                {
                    half brightness;
                    ApplyLudoTerrainAnimation(
                        positionOS,
                        input.animationData,
                        output.visibility,
                        brightness);
                }

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                clip(input.visibility - 0.01h);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
