/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEngine;
using UnityEngine.UIElements;

namespace TelePresent.SmartMouse
{
    internal sealed class SmartMouseSampleDiagram : VisualElement
    {
        const float ExampleSlopeDegrees = 8f;

        static readonly Color GroundStroke = new Color(0.52f, 0.55f, 0.60f);
        static readonly Color GroundFill = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color BoxFill = new Color(0.23f, 0.47f, 0.87f, 0.35f);
        static readonly Color BoxStroke = new Color(0.29f, 0.53f, 0.93f);
        static readonly Color RayColor = new Color(0.62f, 0.64f, 0.68f);
        static readonly Color HitColor = new Color(0.38f, 0.61f, 0.93f);
        static readonly Color MissColor = new Color(0.62f, 0.40f, 0.40f, 0.7f);

        readonly VisualElement _scene;
        float _roughness = 0.4f;

        public SmartMouseSampleDiagram()
        {
            style.flexDirection = FlexDirection.Column;

            _scene = new VisualElement();
            _scene.style.flexGrow = 1f;
#if UNITY_2022_1_OR_NEWER
            //older editors skip the illustration.
            _scene.generateVisualContent += OnGenerateScene;
#endif
            Add(_scene);

            var slider = new Slider(0f, 1f) { value = _roughness };
            slider.AddToClassList("sm-diagram-slider");
            slider.tooltip = "Preview surface roughness: drag to make the example ground bumpier and see how well your "
                + "ray settings still capture the slope.";
            slider.RegisterValueChangedCallback(evt => { _roughness = evt.newValue; _scene.MarkDirtyRepaint(); });
            Add(slider);
        }

        public void Refresh() => _scene.MarkDirtyRepaint();

        static float Wave(float t) =>
            Mathf.Sin(t * 9f + 0.6f) + 0.55f * Mathf.Sin(t * 22f + 2.1f) + 0.3f * Mathf.Sin(t * 41f + 4f);

        static float GroundY(float x, float w, float cyMid, float span, float bump, float originY)
        {
            float t = x / Mathf.Max(1f, w);
            float y = cyMid + (0.5f - t) * span + bump * Wave(t);
            return Mathf.Max(y, originY + 14f); // stays below the ray origin so rays always cast downward
        }

#if UNITY_2022_1_OR_NEWER
        void OnGenerateScene(MeshGenerationContext mgc)
        {
            Rect rect = _scene.contentRect;
            if (rect.width < 12f || rect.height < 12f) return;

            float w = rect.width;
            float h = rect.height;
            var p = mgc.painter2D;

            float originY = h * 0.26f;
            float cyMid = h * 0.64f;
            float span = w * Mathf.Tan(ExampleSlopeDegrees * Mathf.Deg2Rad);
            float bump = _roughness * Mathf.Clamp(h * 0.12f, 6f, 16f);

            const int segs = 40;
            p.fillColor = GroundFill;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, h));
            for (int s = 0; s <= segs; s++)
            {
                float x = w * s / segs;
                p.LineTo(new Vector2(x, GroundY(x, w, cyMid, span, bump, originY)));
            }
            p.LineTo(new Vector2(w, h));
            p.ClosePath();
            p.Fill();

            p.strokeColor = GroundStroke;
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, GroundY(0f, w, cyMid, span, bump, originY)));
            for (int s = 1; s <= segs; s++)
            {
                float x = w * s / segs;
                p.LineTo(new Vector2(x, GroundY(x, w, cyMid, span, bump, originY)));
            }
            p.Stroke();

            int n = Mathf.Clamp(SmartMouseSettings.AlignRayGrid, 1, 5);
            // inset goes negative when spread is above 1
            float inset = Mathf.Clamp((1f - SmartMouseSettings.AlignRaySpread) * 0.5f, -0.5f, 0.5f);
            float depth = Mathf.Max(0f, SmartMouseSettings.AlignRayDepth);
            float fhw = w * 0.20f;
            float cx = w * 0.5f;
            float barL = cx - fhw;
            float barR = cx + fhw;

            float lowestGround = cyMid + 0.5f * span + bump * 1.85f;
            float fullReach = (lowestGround + 6f) - originY;
            float rayLen = fullReach * Mathf.Clamp(0.25f + 0.75f * depth, 0.12f, 3f);

            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            int hits = 0;
            for (int k = 0; k < n; k++)
            {
                float x = n == 1 ? cx : Mathf.LerpUnclamped(barL, barR, Mathf.Lerp(inset, 1f - inset, k / (float)(n - 1)));
                float gy = GroundY(x, w, cyMid, span, bump, originY);
                if (originY + rayLen >= gy)
                {
                    sx += x; sy += gy; sxx += x * x; sxy += x * gy;
                    hits++;
                }
            }

            float m = 0f;
            if (n == 1)
            {
                float dx = Mathf.Max(1f, w * 0.01f);
                m = (GroundY(cx + dx, w, cyMid, span, bump, originY) - GroundY(cx - dx, w, cyMid, span, bump, originY)) / (2f * dx);
            }
            else if (hits >= 2)
            {
                double denom = hits * sxx - sx * sx;
                if (System.Math.Abs(denom) > 1e-3) m = (float)((hits * sxy - sx * sy) / denom);
            }
            m = Mathf.Clamp(m, -1.2f, 1.2f);
            Vector2 along = new Vector2(1f, m).normalized;
            Vector2 up = new Vector2(along.y, -along.x);

            for (int k = 0; k < n; k++)
            {
                float x = n == 1 ? cx : Mathf.LerpUnclamped(barL, barR, Mathf.Lerp(inset, 1f - inset, k / (float)(n - 1)));
                float gy = GroundY(x, w, cyMid, span, bump, originY);
                float baseY = originY + (x - cx) * m;
                bool hit = originY + rayLen >= gy;
                float endY = hit ? gy : baseY + rayLen;

                p.strokeColor = hit ? RayColor : MissColor;
                p.lineWidth = 1.5f;
                for (float yy = baseY; yy < endY; yy += 6f)
                {
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, yy));
                    p.LineTo(new Vector2(x, Mathf.Min(yy + 3.5f, endY)));
                    p.Stroke();
                }

                if (hit)
                {
                    p.fillColor = HitColor;
                    p.BeginPath();
                    p.Arc(new Vector2(x, gy), 2.5f, 0f, 360f);
                    p.Fill();
                }
            }

            Vector2 baseC = new Vector2(cx, originY);
            const float bh = 20f;
            Vector2 bl = baseC - along * fhw;
            Vector2 br = baseC + along * fhw;
            Vector2 tl = bl + up * bh;
            Vector2 tr = br + up * bh;

            p.fillColor = BoxFill;
            p.BeginPath();
            p.MoveTo(bl);
            p.LineTo(br);
            p.LineTo(tr);
            p.LineTo(tl);
            p.ClosePath();
            p.Fill();

            p.strokeColor = BoxStroke;
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(bl);
            p.LineTo(br);
            p.LineTo(tr);
            p.LineTo(tl);
            p.ClosePath();
            p.Stroke();
        }
#endif
    }
}
