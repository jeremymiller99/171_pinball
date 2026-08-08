/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

Shader "Hidden/SmartMouse/OutlineComposite"
{
    Properties
    {
        _MainTex      ("Mask",          2D)    = "black" {}
        _OutlineColor ("Outline Color", Color) = (0.35, 1.0, 0.5, 1.0)
        _OutlineWidth ("Outline Width", Float) = 4.0
        _GlowWidth    ("Glow Width",    Float) = 8.0
        _Glow         ("Glow Strength", Float) = 0.55
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay+2" }

        Pass
        {
            Name "OutlineComposite"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define DIRS 24

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float4    _OutlineColor;
            float     _OutlineWidth;
            float     _GlowWidth;
            float     _Glow;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            float sampleMask(float2 uv)
            {
                return tex2D(_MainTex, uv).r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // Some graphics APIs store the render texture flipped; TexelSize.y goes negative then.
                if (_MainTex_TexelSize.y < 0) uv.y = 1.0 - uv.y;

                float2 texel = abs(_MainTex_TexelSize.xy);
                float  m = sampleMask(uv);

                float outlineCov = 0.0;
                float glowCov    = 0.0;

                [unroll]
                for (int k = 0; k < DIRS; k++)
                {
                    float  ang = (6.28318530718 * k) / DIRS;
                    float2 dir = float2(cos(ang), sin(ang)) * texel;

                    outlineCov = max(outlineCov, sampleMask(uv + dir * (_OutlineWidth * 0.5)));
                    outlineCov = max(outlineCov, sampleMask(uv + dir * _OutlineWidth));

                    float g0 = _OutlineWidth + _GlowWidth * 0.34;
                    float g1 = _OutlineWidth + _GlowWidth * 0.67;
                    float g2 = _OutlineWidth + _GlowWidth;
                    glowCov += sampleMask(uv + dir * g0) * 0.6;
                    glowCov += sampleMask(uv + dir * g1) * 0.3;
                    glowCov += sampleMask(uv + dir * g2) * 0.15;
                }
                glowCov /= DIRS;

                float outline = saturate(outlineCov) * (1.0 - m);
                float glow    = saturate(glowCov) * (1.0 - m) * (1.0 - outline);

                float a = (outline + glow * _Glow) * _OutlineColor.a;
                return fixed4(_OutlineColor.rgb, saturate(a));
            }
            ENDCG
        }
    }
}
