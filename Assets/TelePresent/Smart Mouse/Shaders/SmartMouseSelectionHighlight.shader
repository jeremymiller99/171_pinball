/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

Shader "Hidden/SmartMouse/SelectionHighlight"
{
    Properties
    {
        _ColorA      ("Edge Color",   Color) = (1.0, 0.85, 0.2, 1.0)
        _ColorB      ("Center Color", Color) = (1.0, 0.45, 0.05, 1.0)
        _FillOpacity ("Fill Opacity", Float) = 0.35
        _EdgePower   ("Edge Power",   Float) = 2.0
        _BaseFill    ("Base Fill",    Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay+1" }

        Pass
        {
            Name "Fill"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float  facing : TEXCOORD0;
            };

            float4 _ColorA;
            float4 _ColorB;
            float  _FillOpacity;
            float  _EdgePower;
            float  _BaseFill;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldNormal = normalize(UnityObjectToWorldNormal(v.normal));
                float3 worldPos    = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 viewDir     = normalize(UnityWorldSpaceViewDir(worldPos));
                o.facing           = saturate(dot(viewDir, worldNormal));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float edge  = pow(saturate(1.0 - i.facing), _EdgePower);
                float3 col  = lerp(_ColorB.rgb, _ColorA.rgb, edge);

                float alpha = lerp(_BaseFill, 1.0, edge) * _FillOpacity;
                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }
}
