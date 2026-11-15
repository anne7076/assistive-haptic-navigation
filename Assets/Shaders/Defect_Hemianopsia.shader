Shader "TP6/Defect_Hemianopsia"
{
    Properties
    {
        _Radius ("Radius", Range(0.0, 1.0)) = 0.42
        _FadeWidth ("Fade Width", Range(0.001, 0.5)) = 0.14
        [Toggle] _Unilateral ("Unilateral", Float) = 1
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
                float _Unilateral;
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
                float alphaUnilateral = 1.0 - smoothstep(_Radius, _Radius + _FadeWidth, screenUV.x);
                float edgeDist = min(screenUV.x, 1.0 - screenUV.x);
                float alphaBilateral = 1.0 - smoothstep(_Radius, _Radius + _FadeWidth, edgeDist);
                float alpha = (_Unilateral > 0.5) ? alphaUnilateral : alphaBilateral;
                return half4(0.0, 0.0, 0.0, alpha);
            }
            ENDHLSL
        }
    }
}
