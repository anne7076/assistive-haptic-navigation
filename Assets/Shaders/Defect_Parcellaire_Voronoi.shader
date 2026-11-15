Shader "TP6/Defect_Parcellaire_Voronoi"
{
    Properties
    {
        _CellDensity ("Cell Density", Range(1.0, 15.0)) = 4.5
        _AnimSpeed ("Animation Speed", Range(0.0, 5.0)) = 1.0
        _Threshold ("Spot Size Threshold", Range(0.0, 1.0)) = 0.25
        _FadeWidth ("Fade Width", Range(0.001, 0.4)) = 0.12
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
                float _CellDensity;
                float _AnimSpeed;
                float _Threshold;
                float _FadeWidth;
            CBUFFER_END

            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float voronoiNoise(float2 uv, float angleOffset)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float minDist = 8.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 rnd = hash22(g + lattice);
                        float2 offset = 0.5 + 0.5 * sin(angleOffset + 6.2831853 * rnd);
                        float d = distance(lattice + offset, f);
                        minDist = min(minDist, d);
                    }
                }
                return minDist;
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
                float2 uv = float2(screenUV.x * aspect, screenUV.y) * _CellDensity;

                float angle = _Time.y * _AnimSpeed;
                float v = voronoiNoise(uv, angle);
                float alpha = 1.0 - smoothstep(_Threshold, _Threshold + _FadeWidth, v);
                return half4(0.0, 0.0, 0.0, alpha);
            }
            ENDHLSL
        }
    }
}
