Shader "UI/NoiseBackground"
{
    // UGUI port of "Noise/BackgroundGPU Lit" so the animated noise
    // backgrounds can be used on Image components (tooltips, panels).
    // Lighting/fog dropped; sprite alpha shapes the noise and the
    // Image tint / CanvasGroup alpha come through vertex color.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Coordinates)]
        _Resolution ("Resolution (for coord space)", Vector) = (256, 144, 0, 0)
        _Aspect ("Aspect (x = width/height)", Float) = 1.7777778
        _Tiling ("Tiling (XY)", Vector) = (1, 1, 0, 0)

        [Header(Animation)]
        _Speed ("Evolve Speed", Float) = 0.35
        _ScrollDir ("Scroll Direction", Vector) = (0.7, 0.0, 0, 0)
        _ScrollSpeed ("Scroll Speed", Float) = 0.15

        [Header(Noise)]
        _Seed ("Seed", Float) = 1337
        _Scale ("Scale (bigger = larger blobs)", Float) = 80
        _Octaves ("Octaves (1..6)", Float) = 4
        _Lacunarity ("Lacunarity", Float) = 2
        _Gain ("Gain", Float) = 0.5

        [Header(Domain Warp)]
        _WarpEnabled ("Warp Enabled (0/1)", Float) = 1
        _WarpSeed ("Warp Seed", Float) = 1337
        _WarpScale ("Warp Scale", Float) = 55
        _WarpAmp ("Warp Amplitude", Float) = 18
        _WarpOctaves ("Warp Octaves (1..4)", Float) = 3

        [Header(Swirl)]
        _SwirlEnabled ("Swirl Enabled (0/1)", Float) = 0
        _SwirlDegrees ("Swirl Degrees (edge)", Float) = 0
        _SwirlFalloff ("Swirl Falloff", Float) = 1.6
        _SpinEnabled ("Spin Enabled (0/1)", Float) = 0
        _SpinDegPerSec ("Spin Deg/Sec", Float) = 30

        [Header(Stars)]
        _StarsEnabled ("Stars Enabled (0/1)", Float) = 0
        _StarDensity ("Star Density (grid cells)", Float) = 20
        _StarSize ("Star Size (0..0.5)", Float) = 0.04
        _StarBrightness ("Star Brightness", Float) = 1.5
        _StarTwinkleSpeed ("Star Twinkle Speed", Float) = 3.0
        _StarSeed ("Star Seed", Float) = 42

        [Header(Stylization)]
        _Contrast ("Contrast", Float) = 1.25
        _Brightness ("Brightness", Float) = 0

        [Header(Arcade)]
        _PixelateEnabled ("Pixelate (0/1)", Float) = 0
        _PixelSize ("Pixel Size (res px)", Float) = 3
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0
        _ScanlinePeriod ("Scanline Period (res px)", Float) = 3

        [Header(Outline)]
        // The border ring is baked into the sprite's red channel
        // (see RoundedRect_Tooltip.png); alpha scales its opacity.
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineBoost ("Outline Brightness", Float) = 1

        [Header(Palette Banding)]
        _UsePalette ("Use Palette (0/1)", Float) = 1
        _Palette0 ("Palette 0", Color) = (0.05, 0.05, 0.07, 1)
        _Palette1 ("Palette 1", Color) = (0.20, 0.16, 0.30, 1)
        _Palette2 ("Palette 2", Color) = (0.44, 0.18, 0.52, 1)
        _Palette3 ("Palette 3", Color) = (0.85, 0.72, 0.92, 1)

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
            Name "NoiseBG"
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

            float4 _Resolution;
            float _Aspect;
            float4 _Tiling;

            float _Speed;
            float4 _ScrollDir;
            float _ScrollSpeed;

            float _Seed;
            float _Scale;
            float _Octaves;
            float _Lacunarity;
            float _Gain;

            float _WarpEnabled;
            float _WarpSeed;
            float _WarpScale;
            float _WarpAmp;
            float _WarpOctaves;

            float _SwirlEnabled;
            float _SwirlDegrees;
            float _SwirlFalloff;
            float _SpinEnabled;
            float _SpinDegPerSec;

            float _StarsEnabled;
            float _StarDensity;
            float _StarSize;
            float _StarBrightness;
            float _StarTwinkleSpeed;
            float _StarSeed;

            float _Contrast;
            float _Brightness;

            float _PixelateEnabled;
            float _PixelSize;
            float _ScanlineIntensity;
            float _ScanlinePeriod;

            fixed4 _OutlineColor;
            float _OutlineBoost;

            float _UsePalette;
            float4 _Palette0;
            float4 _Palette1;
            float4 _Palette2;
            float4 _Palette3;

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

            // ---- cheap hash / value noise ----
            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash12(i + 0.0);
                float b = hash12(i + float2(1.0, 0.0));
                float c = hash12(i + float2(0.0, 1.0));
                float d = hash12(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p, float octaves, float lacunarity, float gain)
            {
                float sum = 0.0;
                float amp = 0.5;
                float2 pp = p;

                [unroll] for (int i = 0; i < 6; i++)
                {
                    if (i >= (int)round(clamp(octaves, 1.0, 6.0)))
                        break;
                    sum += amp * valueNoise(pp);
                    pp *= max(1.01, lacunarity);
                    amp *= saturate(gain);
                }
                return sum;
            }

            float2 rotate2(float2 v, float s, float c)
            {
                return float2(v.x * c - v.y * s, v.x * s + v.y * c);
            }

            half4 palette4(float v)
            {
                int idx = clamp((int)floor(v * 4.0), 0, 3);
                if (idx == 0) return (half4)_Palette0;
                if (idx == 1) return (half4)_Palette1;
                if (idx == 2) return (half4)_Palette2;
                return (half4)_Palette3;
            }

            half3 twinklingStars(float2 starUV, float time)
            {
                half3 col = 0;
                float2 gridUV = starUV * _StarDensity;
                float2 cellID = floor(gridUV);
                float2 cellUV = frac(gridUV);

                [unroll] for (int yy = -1; yy <= 1; yy++)
                [unroll] for (int xx = -1; xx <= 1; xx++)
                {
                    float2 neighbor = float2(xx, yy);
                    float2 id = cellID + neighbor;

                    float2 starPos = float2(
                        hash12(id + _StarSeed * 0.17),
                        hash12(id + _StarSeed * 0.31 + 71.0)
                    );
                    float brightness = hash12(id + _StarSeed * 0.53 + 113.0);

                    float dist = length(cellUV - neighbor - starPos);
                    float star = 1.0 - smoothstep(0.0, _StarSize, dist);
                    star = pow(max(star, 0.0), 4.0);

                    float phase = hash12(id + 200.0) * 6.2831;
                    float freq  = 0.5 + hash12(id + 300.0) * 1.5;
                    float twinkle = 0.5 + 0.5 * sin(time * _StarTwinkleSpeed * freq + phase);

                    col += star * twinkle * brightness * _StarBrightness;
                }
                return col;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;

                float2 res = max(_Resolution.xy, 1.0.xx);
                float2 uv = IN.texcoord;
                float2 tiling = max(_Tiling.xy, 0.0001.xx);

                // Snap noise coords to a chunky grid for a retro look.
                float2 noiseUV = uv;
                if (_PixelateEnabled > 0.5 && _PixelSize > 0.5)
                {
                    float2 grid = res / _PixelSize;
                    noiseUV = (floor(uv * grid) + 0.5) / grid;
                }

                // UV coord mode only: UI quads have no useful object/world space.
                float2 base = ((noiseUV * tiling) - (0.5 * tiling)) * res;

                if (_SpinEnabled > 0.5 && abs(_SpinDegPerSec) > 0.0001)
                {
                    float ang = radians(_SpinDegPerSec) * t;
                    base = rotate2(base, sin(ang), cos(ang));
                }

                if (_SwirlEnabled > 0.5 && abs(_SwirlDegrees) > 0.0001)
                {
                    float2 swirlBase = float2(base.x * _Aspect, base.y);
                    float r = length(swirlBase);
                    float maxR = length(float2(res.x * 0.5 * _Aspect, res.y * 0.5));
                    float r01 = (maxR > 0.0) ? saturate(r / maxR) : 0.0;

                    float edgeRad = radians(_SwirlDegrees);
                    float ang = edgeRad * pow(r01, max(0.0001, _SwirlFalloff));
                    base = rotate2(base, sin(ang), cos(ang));
                }

                float2 scrollDirRaw = _ScrollDir.xy;
                float scrollLen = length(scrollDirRaw);
                float2 scrollDir = (scrollLen > 1e-5) ? (scrollDirRaw / scrollLen) : float2(0.0, 0.0);
                float2 scroll = scrollDir * (_ScrollSpeed * t);
                float2 coordPx = base + (res * 0.5) + float2(scroll.x * res.x, scroll.y * res.y);

                float freq = 1.0 / max(0.0001, _Scale);
                float2 p = coordPx * freq;
                p += (_Seed * 0.001) * float2(37.0, 91.0);

                float timeZ = t * _Speed;
                p += float2(timeZ, -timeZ) * 0.25;

                if (_WarpEnabled > 0.5 && _WarpAmp > 0.0)
                {
                    float warpFreq = 1.0 / max(0.0001, _WarpScale);
                    float2 wp = coordPx * warpFreq;
                    wp += (_WarpSeed * 0.001) * float2(13.0, 57.0);
                    wp += float2(timeZ, timeZ) * 0.15;

                    float wx = fbm(wp + 11.7, _WarpOctaves, _Lacunarity, _Gain);
                    float wy = fbm(wp + 63.2, _WarpOctaves, _Lacunarity, _Gain);
                    float2 wv = (float2(wx, wy) * 2.0 - 1.0) * (_WarpAmp * freq);
                    p += wv;
                }

                float noise01 = fbm(p, _Octaves, _Lacunarity, _Gain); // 0..1
                float v = (noise01 - 0.5) * _Contrast + 0.5 + _Brightness;
                v = saturate(v);

                half4 noiseCol = (_UsePalette > 0.5) ? palette4(v) : half4(v, v, v, 1.0);

                if (_StarsEnabled > 0.5)
                {
                    noiseCol.rgb += twinklingStars(noiseUV, t);
                }

                // CRT-style horizontal scanlines, using unsnapped UVs so the
                // lines stay thin even when the noise is pixelated.
                if (_ScanlineIntensity > 0.001)
                {
                    float scan = 0.5 + 0.5 * sin(
                        uv.y * res.y * 6.2831853 / max(1.0, _ScanlinePeriod));
                    noiseCol.rgb *= lerp(1.0, scan, _ScanlineIntensity);
                }

                // Sprite alpha keeps the panel's shape (rounded corners etc.);
                // its red channel carries the baked-in border ring.
                fixed4 spriteTex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half outlineMask = spriteTex.r * _OutlineColor.a;
                noiseCol.rgb = lerp(
                    noiseCol.rgb,
                    _OutlineColor.rgb * _OutlineBoost,
                    outlineMask);

                fixed4 col = fixed4(noiseCol.rgb, spriteTex.a) * IN.color;

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
