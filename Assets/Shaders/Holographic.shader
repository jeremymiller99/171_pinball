Shader "FX/Holographic"
{
    Properties
    {
        [Header(Base)]
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Hologram Tint", Color) = (0.25, 0.85, 1, 1)
        _Brightness ("Brightness", Range(0,5)) = 1.4

        [Header(Fresnel Rim)]
        _RimColor ("Rim Color", Color) = (0.5, 0.95, 1, 1)
        _RimPower ("Rim Power", Range(0.5,8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0,4)) = 1.6

        [Header(Scanlines)]
        _ScanlineCount ("Scanline Count", Float) = 180
        _ScanlineIntensity ("Scanline Intensity", Range(0,1)) = 0.4
        _ScanlineSpeed ("Scanline Scroll Speed", Float) = -1.5
        _ScanlineSharpness ("Scanline Sharpness", Range(0.5,8)) = 1.5

        [Header(Sweep Bar)]
        _SweepIntensity ("Sweep Intensity", Range(0,3)) = 0.8
        _SweepSpeed ("Sweep Speed", Float) = 0.35
        _SweepHeight ("Sweep Height", Range(0.01,1)) = 0.12

        [Header(Grid)]
        _GridCount ("Grid Cells", Float) = 16
        _GridIntensity ("Grid Intensity", Range(0,1)) = 0.18
        _GridLineWidth ("Grid Line Width", Range(0.001,0.2)) = 0.02

        [Header(Glitch and Flicker)]
        _Flicker ("Flicker Intensity", Range(0,0.5)) = 0.05
        _FlickerSpeed ("Flicker Speed", Float) = 14
        _GlitchIntensity ("Glitch Intensity", Range(0,0.2)) = 0.02
        _GlitchSpeed ("Glitch Speed", Float) = 6
        _Noise ("Static Noise", Range(0,0.5)) = 0.06

        [Header(Transparency)]
        _Alpha ("Base Alpha", Range(0,1)) = 0.55
        _VignetteIntensity ("Vignette Intensity", Range(0,2)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha One   // additive-ish: hologram adds light, never occludes

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Brightness;

                float4 _RimColor;
                float  _RimPower;
                float  _RimIntensity;

                float  _ScanlineCount;
                float  _ScanlineIntensity;
                float  _ScanlineSpeed;
                float  _ScanlineSharpness;

                float  _SweepIntensity;
                float  _SweepSpeed;
                float  _SweepHeight;

                float  _GridCount;
                float  _GridIntensity;
                float  _GridLineWidth;

                float  _Flicker;
                float  _FlickerSpeed;
                float  _GlitchIntensity;
                float  _GlitchSpeed;
                float  _Noise;

                float  _Alpha;
                float  _VignetteIntensity;
            CBUFFER_END

            // cheap hash for noise / glitch
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS   = nrm.normalWS;
                OUT.viewDirWS  = GetWorldSpaceViewDir(pos.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t  = _Time.y;
                float2 uv = IN.uv;

                // ---- Horizontal glitch displacement (banded, occasional) ----
                float band      = floor(uv.y * 24.0);
                float glitchRnd = hash21(float2(band, floor(t * _GlitchSpeed)));
                // only the top ~15% of random bands actually shift
                float glitchAmt = step(0.85, glitchRnd) * (glitchRnd - 0.85) / 0.15;
                uv.x += (glitchRnd - 0.5) * 2.0 * glitchAmt * _GlitchIntensity;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half3 col = tex.rgb * _Color.rgb;

                // ---- Scanlines ----
                float scan = sin((uv.y * _ScanlineCount - t * _ScanlineSpeed) * 6.2831853);
                scan = pow(saturate(scan * 0.5 + 0.5), _ScanlineSharpness);
                float scanline = lerp(1.0, scan, _ScanlineIntensity);

                // ---- Grid lines ----
                float2 g     = frac(uv * _GridCount);
                float2 gLine = min(g, 1.0 - g);                       // distance to nearest cell edge
                float  grid  = 1.0 - smoothstep(0.0, _GridLineWidth, min(gLine.x, gLine.y));

                // ---- Vertical sweep bar ----
                float sweep    = frac(uv.y - t * _SweepSpeed);
                float sweepBar = smoothstep(0.0, _SweepHeight, sweep) *
                                 (1.0 - smoothstep(_SweepHeight, _SweepHeight * 2.0, sweep));

                // ---- Fresnel rim ----
                float3 N     = normalize(IN.normalWS);
                float3 V     = normalize(IN.viewDirWS);
                float  fres  = pow(1.0 - saturate(abs(dot(N, V))), _RimPower);

                // ---- Flicker / noise / vignette ----
                float flick = 1.0 - _Flicker * (0.5 + 0.5 * sin(t * _FlickerSpeed));
                float n     = hash21(uv * float2(512, 512) + frac(t) * 113.0);
                float noise = lerp(1.0, n, _Noise);

                float2 vd  = uv - 0.5;
                float  vig = saturate(1.0 - dot(vd, vd) * _VignetteIntensity * 4.0);

                // ---- Combine ----
                col *= scanline * flick * noise * vig;
                col += _Color.rgb  * grid     * _GridIntensity;
                col += _Color.rgb  * sweepBar * _SweepIntensity;
                col += _RimColor.rgb * fres   * _RimIntensity;
                col *= _Brightness;

                // alpha: base plate + everything that emits light
                float alpha = _Alpha * tex.a * vig;
                alpha = saturate(alpha
                              + grid     * _GridIntensity
                              + sweepBar * _SweepIntensity * 0.5
                              + fres     * _RimIntensity   * 0.5);
                alpha *= _Color.a * flick;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
