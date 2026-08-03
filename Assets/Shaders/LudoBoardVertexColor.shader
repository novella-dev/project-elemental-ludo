Shader "Elemental Ludo/Board Vertex Color"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Board"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LudoTerrainAnimation.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float4 animationData : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                half visibility : TEXCOORD0;
                half brightness : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                ApplyLudoTerrainAnimation(
                    positionOS,
                    input.animationData,
                    output.visibility,
                    output.brightness);
                output.positionHCS = TransformObjectToHClip(positionOS);
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half alpha = input.color.a * input.visibility;
                clip(alpha - 0.01h);

                half3 normal = normalize(input.normalWS);
                half3 lightDirection = normalize(half3(-0.35h, 0.55h, -0.8h));
                half lightAmount = saturate(dot(normal, lightDirection));
                half lightBand = lightAmount > 0.68h
                    ? 1.0h
                    : (lightAmount > 0.24h ? 0.80h : 0.62h);
                return half4(
                    saturate(input.color.rgb * input.brightness * lightBand),
                    alpha);
            }
            ENDHLSL
        }
    }
}
