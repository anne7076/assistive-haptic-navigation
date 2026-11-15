Shader "TP6/Defect_CentralScotoma"
{
    Properties
    {
        _Radius ("Radius", Range(0.0, 1.5)) = 0.20
        _FadeWidth ("Fade Width", Range(0.001, 0.5)) = 0.12
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
                float _Radius;
                float _FadeWidth;
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
                float2 centered = screenUV * 2.0 - 1.0;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                centered.x *= aspect;

                float dist = distance(centered, float2(0.0, 0.0));
                float t = smoothstep(_Radius, _Radius + _FadeWidth, dist);
                float alpha = 1.0 - t;
                return half4(0.0, 0.0, 0.0, alpha);
            }
            ENDHLSL
        }
    }
}
