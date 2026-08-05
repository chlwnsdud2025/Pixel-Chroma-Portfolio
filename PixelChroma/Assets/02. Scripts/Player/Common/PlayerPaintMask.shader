Shader "PixelChroma/PlayerPaintMask"
{
    // 원본 스프라이트(_MainTex) 위에, 페인트 마스크(_PaintMask)에 칠해진 부분만
    // 외곽선 엠보스(_EmbossTex의 법선)를 적용해 합성한다.
    //  - _MainTex   : SpriteRenderer가 자동으로 넣어주는 스프라이트 시트(아틀라스)
    //  - _PaintMask : 이 인스턴스 전용 페인트 마스크(rgb=칠한색, a=칠한양). 셀(프레임) 좌표계
    //  - _EmbossTex : 현재 프레임의 외곽선 법선(rg=normal.xy 인코딩, b=테두리 플래그). 셀 좌표계
    //  - _MaskRect  : 시트 UV(_MainTex) → 셀 UV(마스크/엠보스) 변환 (xy=오프셋, zw=스케일)
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _PaintMask ("Paint Mask", 2D) = "black" {}
        _EmbossTex ("Emboss Normal", 2D) = "black" {}
        _MaskRect ("Mask Rect (xy=offset, zw=scale)", Vector) = (0,0,1,1)

        _ShadingStrength ("Shading Strength", Range(0,1)) = 1
        _EdgeHighlight ("Edge Highlight", Range(0,1)) = 0.05
        _EdgeShadow ("Edge Shadow", Range(0,1)) = 0.05
        _LightAngle ("Light Angle", Range(0,360)) = 135

        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)

        _PaintVisible ("Paint Visible", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _PaintMask;
            sampler2D _EmbossTex;
            float4 _MaskRect;
            float _ShadingStrength;
            float _EdgeHighlight;
            float _EdgeShadow;
            float _LightAngle;
            float _FlashAmount;
            float _PaintVisible;
            fixed4 _Color;
            fixed4 _RendererColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, IN.texcoord);

                // 시트 UV → 셀(프레임) UV
                float2 cellUV = (IN.texcoord - _MaskRect.xy) / _MaskRect.zw;

                fixed4 mask = tex2D(_PaintMask, cellUV);   // rgb=칠한색, a=칠한양(0~1)
                fixed4 e    = tex2D(_EmbossTex, cellUV);   // rg=normal 인코딩, b=테두리 플래그

                // 외곽선 엠보스: edge = 법선 · 빛방향 (테두리에서만 b=1)
                float2 n = e.rg * 2.0 - 1.0;
                float ang = _LightAngle * 3.14159265 / 180.0;
                float2 lightDir = float2(cos(ang), sin(ang));
                float edge = dot(n, lightDir) * e.b;

                float3 paint = mask.rgb;
                float hi = saturate(edge)  * _EdgeHighlight * _ShadingStrength;
                float sh = saturate(-edge) * _EdgeShadow    * _ShadingStrength;
                paint = lerp(paint, float3(1,1,1), hi);
                paint = lerp(paint, float3(0,0,0), sh);

                // 원본 위에 칠한 양(mask.a)만큼 페인트를 덮는다. _PaintVisible=0이면 페인트 숨김(데이터는 유지).
                float3 rgb = lerp(sprite.rgb, paint, mask.a * _PaintVisible);

                fixed4 col = fixed4(rgb, sprite.a) * IN.color;
                col.rgb = lerp(col.rgb, float3(1,1,1), _FlashAmount);
                col.rgb *= col.a;
                return col;
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
