Shader "Elemental Ludo/Smoke Particle"
{
    Properties
    {
        _EdgeSoftness("Edge Softness", Range(0.5, 4)) = 1.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
        }

        Pass
        {
            Name "Smoke"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex SmokeVertex
            #pragma fragment SmokeFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings SmokeVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 SmokeFragment(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radiusSquared = dot(centeredUv, centeredUv);
                clip(1.0 - radiusSquared);

                half softCircle = pow(
                    saturate(1.0 - radiusSquared),
                    _EdgeSoftness);
                half alpha = input.color.a * softCircle;
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
