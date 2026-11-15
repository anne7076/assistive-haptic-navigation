Shader "TP6/Defect_Parcellaire"
{
    Properties
    {
        _NoiseScale ("Noise Scale", Range(1.0, 20.0)) = 6.0
        _Threshold ("Threshold", Range(0.0, 1.0)) = 0.58
        _FadeWidth ("Fade Width", Range(0.001, 0.3)) = 0.08
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
                float _NoiseScale;
                float _Threshold;
                float _FadeWidth;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

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
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uv = float2(screenUV.x * aspect, screenUV.y) * _NoiseScale;

                // 2-octave smooth noise for organic scotoma patches
                float n = valueNoise(uv) * 0.7 + valueNoise(uv * 2.1 + 13.7) * 0.3;
                float alpha = smoothstep(_Threshold, _Threshold + _FadeWidth, n);
                return half4(0.0, 0.0, 0.0, alpha);
            }
            ENDHLSL
        }
    }
}
