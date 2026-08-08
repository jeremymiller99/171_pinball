/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/
Shader "Smart Mouse/Demo Unlit Opaque"
{
    Properties
    {
        _ColorA ("Bottom Color", Color) = (0.3,0.3,0.3,1)
        _ColorB ("Top Color", Color) = (1,1,1,1)
        _GradientMinY ("Gradient Min Height", Float) = -2
        _GradientMaxY ("Gradient Max Height", Float) = 6
        _TilingOffset ("Tiling and Offset", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        ZWrite On
        Lighting Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float worldY : TEXCOORD1;
            };

            float4 _TilingOffset;
            fixed4 _ColorA;
            fixed4 _ColorB;
            float _GradientMinY;
            float _GradientMaxY;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * _TilingOffset.xy + _TilingOffset.zw;
                o.worldY = mul(unity_ObjectToWorld, v.vertex).y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = saturate((i.worldY - _GradientMinY) / max(1e-5, _GradientMaxY - _GradientMinY));
                return lerp(_ColorA, _ColorB, t);
            }
            ENDCG
        }
    }
}
