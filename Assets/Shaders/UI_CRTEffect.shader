Shader "UI/CRT Effect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Scanlines)]
        _ScanlineCount ("Scanline Count", Float) = 240
        _ScanlineIntensity ("Scanline Intensity", Range(0,1)) = 0.35
        _ScanlineSpeed ("Scanline Scroll Speed", Float) = 8
        _ScanlineSharpness ("Scanline Sharpness", Range(0.5,8)) = 2

        [Header(Aperture Grille (vertical RGB mask))]
        _GrilleIntensity ("Grille Intensity", Range(0,1)) = 0.15
        _GrilleCount ("Grille Count", Float) = 320

        [Header(Rolling Scan Bar)]
        _RollIntensity ("Roll Bar Intensity", Range(0,1)) = 0.1
        _RollSpeed ("Roll Bar Speed", Float) = 0.4
        _RollHeight ("Roll Bar Height", Range(0.01,1)) = 0.25

        [Header(Chromatic Aberration)]
        _Aberration ("RGB Split Amount", Range(0,0.02)) = 0.0025

        [Header(Screen Curvature)]
        _Curvature ("Barrel Curvature", Range(0,1)) = 0.15
        _CurvatureMaskHardness ("Edge Mask Hardness", Range(0,500)) = 200

        [Header(Vignette)]
        _VignetteIntensity ("Vignette Intensity", Range(0,2)) = 0.6
        _VignetteRoundness ("Vignette Roundness", Range(0.1,2)) = 1

        [Header(Flicker and Noise)]
        _Flicker ("Flicker Intensity", Range(0,0.5)) = 0.04
        _FlickerSpeed ("Flicker Speed", Float) = 18
        _Noise ("Static Noise", Range(0,0.5)) = 0.05

        [Header(Color)]
        _Brightness ("Brightness", Range(0,3)) = 1.15
        _Tint ("CRT Color Cast", Color) = (1,1,1,1)

        // --- Required UI / masking plumbing ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
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
            Name "CRT"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _ScanlineCount;
            float _ScanlineIntensity;
            float _ScanlineSpeed;
            float _ScanlineSharpness;

            float _GrilleIntensity;
            float _GrilleCount;

            float _RollIntensity;
            float _RollSpeed;
            float _RollHeight;

            float _Aberration;

            float _Curvature;
            float _CurvatureMaskHardness;

            float _VignetteIntensity;
            float _VignetteRoundness;

            float _Flicker;
            float _FlickerSpeed;
            float _Noise;

            float _Brightness;
            fixed4 _Tint;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // cheap hash for static noise
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // barrel distortion around center; returns curved uv
            float2 curveUV(float2 uv, float amt)
            {
                uv = uv * 2.0 - 1.0;          // -1..1
                float2 offset = abs(uv.yx) / float2(6.0, 5.0);
                uv = uv + uv * offset * offset * amt * 4.0;
                return uv * 0.5 + 0.5;        // back to 0..1
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                float2 uv = IN.texcoord;

                // ---- Screen curvature ----
                float2 cuv = (_Curvature > 0.0001) ? curveUV(uv, _Curvature) : uv;

                // mask out pixels pushed outside the screen by curvature
                float2 edge = smoothstep(0.0, 1.0 / max(1.0, _CurvatureMaskHardness),
                                         cuv) *
                              smoothstep(0.0, 1.0 / max(1.0, _CurvatureMaskHardness),
                                         1.0 - cuv);
                float screenMask = edge.x * edge.y;

                // ---- Chromatic aberration (sample R/G/B with horizontal offset) ----
                float2 dir = cuv - 0.5;
                float ab = _Aberration;
                fixed4 col;
                col.r = tex2D(_MainTex, cuv + dir * ab).r;
                col.g = tex2D(_MainTex, cuv).g;
                col.b = tex2D(_MainTex, cuv - dir * ab).b;
                col.a = tex2D(_MainTex, cuv).a;
                col += _TextureSampleAdd;

                // ---- Animated horizontal scanlines ----
                float scan = sin((cuv.y * _ScanlineCount - t * _ScanlineSpeed) * 6.2831853);
                scan = pow(saturate(scan * 0.5 + 0.5), _ScanlineSharpness);
                float scanline = lerp(1.0, scan, _ScanlineIntensity);

                // ---- Vertical aperture grille (RGB phosphor stripes) ----
                float g = cuv.x * _GrilleCount;
                float3 grille = float3(
                    sin(g * 3.14159265) * 0.5 + 0.5,
                    sin((g + 0.6667) * 3.14159265) * 0.5 + 0.5,
                    sin((g + 1.3333) * 3.14159265) * 0.5 + 0.5);
                float3 grilleMask = lerp(float3(1,1,1), grille, _GrilleIntensity);

                // ---- Rolling scan bar ----
                float roll = frac(cuv.y - t * _RollSpeed);
                float rollBar = smoothstep(0.0, _RollHeight, roll) *
                                (1.0 - smoothstep(_RollHeight, _RollHeight * 2.0, roll));
                float rolling = 1.0 - rollBar * _RollIntensity;

                // ---- Vignette ----
                float2 vd = (cuv - 0.5) * _VignetteRoundness;
                float vig = saturate(1.0 - dot(vd, vd) * _VignetteIntensity);

                // ---- Flicker ----
                float flick = 1.0 - _Flicker * (0.5 + 0.5 * sin(t * _FlickerSpeed));

                // ---- Static noise ----
                float n = hash21(cuv * float2(640, 480) + frac(t) * 113.0);
                float noise = lerp(1.0, n, _Noise);

                // ---- Combine ----
                col.rgb *= scanline;
                col.rgb *= grilleMask;
                col.rgb *= rolling;
                col.rgb *= vig;
                col.rgb *= flick;
                col.rgb *= noise;
                col.rgb *= _Brightness;
                col.rgb *= _Tint.rgb;
                col.rgb *= screenMask;
                col.a   *= screenMask;

                col *= IN.color;

                // ---- UI clipping / masking ----
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
