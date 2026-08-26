Shader "Custom/MedalShine"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Shine
        _ShineProgress ("Shine Progress", Range(-0.5, 1.5)) = -0.5
        _ShineWidth    ("Shine Width",    Range(0.01, 0.3)) = 0.12
        _ShineColor    ("Shine Color",    Color) = (1, 1, 1, 0.7)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"    = "UniversalPipeline"
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4  color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float  _ShineProgress;
                float  _ShineWidth;
                half4  _ShineColor;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv     = v.uv;
                o.color  = v.color * _Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // 1) 스프라이트 샘플링
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                tex *= i.color;

                // 2) ↗ 대각선 위치: (0,0)=좌하단=0, (1,1)=우상단=1
                float diag = (i.uv.x + i.uv.y) * 0.5;

                // 3) 빛 밴드
                float dist  = abs(diag - _ShineProgress);
                float shine = smoothstep(_ShineWidth, 0.0, dist);

                // 4) 스프라이트 픽셀이 있는 곳에만 빛 추가
                tex.rgb += _ShineColor.rgb * _ShineColor.a * shine * tex.a;

                return tex;
            }
            ENDHLSL
        }
    }
}