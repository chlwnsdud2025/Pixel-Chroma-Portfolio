Shader "Custom/UIGlowPulse"
{
    Properties
    {
        _MainTex       ("Sprite Texture", 2D) = "white" {}
        _Color         ("Tint", Color) = (1,1,1,1)

        // Glow (순위 변동 펄스)
        _GlowColor     ("Glow Color", Color) = (0.3, 0.6, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 1)) = 0

        // Sweep (갱신 스캔라인)
        _SweepProgress ("Sweep Progress", Range(-1, 2)) = -1
        _SweepWidth    ("Sweep Width", Range(0.01, 0.8)) = 0.8
        _SweepColor    ("Sweep Color", Color) = (1, 1, 1, 0.6)
        _SweepSkew     ("Sweep Skew", Range(-1, 1)) = -0.4

        // UI Stencil
        _StencilComp      ("Stencil Comp", Float) = 8
        _Stencil          ("Stencil ID", Float) = 0
        _StencilOp        ("Stencil Op", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask        ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref   [_Stencil]
            Comp  [_StencilComp]
            Pass  [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            fixed4 _GlowColor;
            float  _GlowIntensity;

            float  _SweepProgress;
            float  _SweepWidth;
            fixed4 _SweepColor;
            float  _SweepSkew;

            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.uv       = v.uv;
                o.color    = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = fixed4(0, 0, 0, 0);

                // ── 1. Sweep (좌→우 사선 스캔라인) ──
                float sweepCoord = i.uv.x + (i.uv.y - 0.5) * _SweepSkew;
                float dist  = abs(sweepCoord - _SweepProgress);
                float sweep = smoothstep(_SweepWidth, 0.0, dist);
                col.rgb += _SweepColor.rgb * sweep;
                col.a    = sweep * _SweepColor.a;

                // ── 2. Glow (순위 변동 펄스) ──
                float2 center = i.uv - 0.5;
                float vignette = saturate(length(center) * 1.8);
                float glow = _GlowIntensity * (vignette * 0.7 + 0.3);
                col.rgb += _GlowColor.rgb * glow;
                col.a    = max(col.a, glow * 0.6);

                // ── 3. UI Clipping ──
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, i.worldPos.xy)
                              * step(i.worldPos.xy, _ClipRect.zw);
                col.a *= inside.x * inside.y;
                #endif

                return col;
            }
            ENDCG
        }
    }
}