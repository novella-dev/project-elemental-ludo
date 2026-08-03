Shader "Elemental Ludo/Arena Cel"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeBands ("Shade Bands", Range(2, 5)) = 3
        _ShadeFloor ("Darkest Band", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ArenaCel"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // Off rather than Back, matching the Token shader: this geometry is
            // generated at runtime and a winding slip would punch holes in a
            // column rather than just shading it oddly.
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ShadeBands;
                float _ShadeFloor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);

                // Same key direction the Token shader uses, so a column and a
                // piece standing next to it are lit from the same place.
                half3 lightDirection = normalize(half3(-0.35, 0.55, -0.8));
                half diffuse = saturate(dot(normal, lightDirection));

                // Quantised instead of smooth — the hard steps are the whole
                // point of cel shading, and they sit next to the flat blocks of
                // colour the tokens already have.
                float bands = max(_ShadeBands, 2.0);
                float quantised =
                    min(floor(diffuse * bands), bands - 1.0) / (bands - 1.0);
                half lighting = lerp((half)_ShadeFloor, 1.0h, (half)quantised);

                half3 albedo = _BaseColor.rgb * input.color.rgb;
                return half4(albedo * lighting, _BaseColor.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
