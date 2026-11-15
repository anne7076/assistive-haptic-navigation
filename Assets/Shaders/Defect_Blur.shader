Shader "TP6/Defect_Blur"
{
    Properties
    {
        _BlurRadius ("Blur Radius (Pixels)", Range(0.0, 24.0)) = 8.0
        _Intensity ("Blend Intensity", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos   : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _BlurRadius;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float2 texelSize = _ScreenParams.zw - 1.0;

                // 13-tap Gaussian-weighted disk kernel for smooth low-acuity / cataract blur
                static const float2 kOffsets[12] = {
                    float2( 1.000,  0.000), float2( 0.500,  0.866), float2(-0.500,  0.866),
                    float2(-1.000,  0.000), float2(-0.500, -0.866), float2( 0.500, -0.866),
                    float2( 0.000,  0.500), float2( 0.433,  0.250), float2( 0.433, -0.250),
                    float2( 0.000, -0.500), float2(-0.433, -0.250), float2(-0.433,  0.250)
                };

                half3 accum = SampleSceneColor(screenUV) * 0.16h;

                [unroll]
                for (int i = 0; i < 6; i++)
                {
                    float2 uvOuter = saturate(screenUV + kOffsets[i] * (_BlurRadius * texelSize));
                    accum += SampleSceneColor(uvOuter) * 0.06h;
                }

                [unroll]
                for (int j = 6; j < 12; j++)
                {
                    float2 uvInner = saturate(screenUV + kOffsets[j] * (_BlurRadius * texelSize));
                    accum += SampleSceneColor(uvInner) * 0.08h;
                }

                return half4(accum, saturate(_Intensity));
            }
            ENDHLSL
        }
    }
}
