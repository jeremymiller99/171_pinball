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
    internal sealed class SmartMouseAlignDiagram : VisualElement
    {
        static readonly Color GroundStroke = new Color(0.52f, 0.55f, 0.60f);
        static readonly Color GroundFill = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color BoxFill = new Color(0.23f, 0.47f, 0.87f, 0.35f);
        static readonly Color BoxStroke = new Color(0.29f, 0.53f, 0.93f);
        static readonly Color NormalColor = new Color(0.38f, 0.61f, 0.93f);
        static readonly Color GuideColor = new Color(1f, 1f, 1f, 0.12f);

        readonly VisualElement _scene;
        float _angle = 30f;

        public SmartMouseAlignDiagram()
        {
            style.flexDirection = FlexDirection.Column;

            _scene = new VisualElement();
            _scene.style.flexGrow = 1f;
#if UNITY_2022_1_OR_NEWER
            // MeshGenerationContext.painter2D is the 2022.1 vector API; older editors skip the illustration.
            _scene.generateVisualContent += OnGenerateScene;
#endif
            Add(_scene);

            var slider = new Slider(0f, 90f) { value = _angle };
            slider.AddToClassList("sm-diagram-slider");
            slider.tooltip = "Drag to see how your settings orient an object on a steeper or gentler surface.";
            slider.RegisterValueChangedCallback(evt => { _angle = evt.newValue; Refresh(); });
            Add(slider);

            Refresh();
        }

        float ResultingTilt()
        {
            return _angle < SmartMouseSettings.AlignFlatnessDegrees
                ? 0f
                : Mathf.Min(_angle, SmartMouseSettings.AlignMaxTiltDegrees);
        }

        public void Refresh() => _scene.MarkDirtyRepaint();

        static Vector2 Pt(float cx, float cy, float wx, float wy) => new Vector2(cx + wx, cy - wy);

#if UNITY_2022_1_OR_NEWER
        void OnGenerateScene(MeshGenerationContext mgc)
        {
            Rect rect = _scene.contentRect;
            if (rect.width < 12f || rect.height < 12f) return;

            float w = rect.width;
            float h = rect.height;
            float cx = w * 0.5f;
            float cy = h * 0.62f;

            var p = mgc.painter2D;

            float slopeRad = _angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(slopeRad), Mathf.Sin(slopeRad));
            float L = w * 0.75f;
            Vector2 gA = Pt(cx, cy, -dir.x * L, -dir.y * L);
            Vector2 gB = Pt(cx, cy, dir.x * L, dir.y * L);

            p.fillColor = GroundFill;
            p.BeginPath();
            p.MoveTo(gA);
            p.LineTo(gB);
            p.LineTo(new Vector2(gB.x, h + 2f));
            p.LineTo(new Vector2(gA.x, h + 2f));
            p.ClosePath();
            p.Fill();

            p.strokeColor = GroundStroke;
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(gA);
            p.LineTo(gB);
            p.Stroke();

            Vector2 normalWorld = new Vector2(-Mathf.Sin(slopeRad), Mathf.Cos(slopeRad));
            Vector2 nTip = Pt(cx, cy, normalWorld.x * 30f, normalWorld.y * 30f);
            p.strokeColor = NormalColor;
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(Pt(cx, cy, 0f, 0f));
            p.LineTo(nTip);
            p.Stroke();

            float tilt = ResultingTilt();
            float tiltRad = tilt * Mathf.Deg2Rad;
            Vector2 up = new Vector2(-Mathf.Sin(tiltRad), Mathf.Cos(tiltRad));
            Vector2 right = new Vector2(Mathf.Cos(tiltRad), Mathf.Sin(tiltRad));

            float offsetPx = SmartMouseSettings.SurfacePlacementOffset * 24f;
            Vector2 baseC = up * offsetPx;
            const float bw = 16f, bh = 24f;

            p.strokeColor = GuideColor;
            p.lineWidth = 1f;
            for (float yy = 0f; yy < bh + 8f; yy += 6f)
            {
                p.BeginPath();
                p.MoveTo(Pt(cx, cy, baseC.x, baseC.y + yy));
                p.LineTo(Pt(cx, cy, baseC.x, baseC.y + Mathf.Min(yy + 3f, bh + 8f)));
                p.Stroke();
            }

            Vector2 bl = baseC - right * bw;
            Vector2 br = baseC + right * bw;
            Vector2 tl = bl + up * bh;
            Vector2 tr = br + up * bh;

            p.fillColor = BoxFill;
            p.BeginPath();
            p.MoveTo(Pt(cx, cy, bl.x, bl.y));
            p.LineTo(Pt(cx, cy, br.x, br.y));
            p.LineTo(Pt(cx, cy, tr.x, tr.y));
            p.LineTo(Pt(cx, cy, tl.x, tl.y));
            p.ClosePath();
            p.Fill();

            p.strokeColor = BoxStroke;
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(Pt(cx, cy, bl.x, bl.y));
            p.LineTo(Pt(cx, cy, br.x, br.y));
            p.LineTo(Pt(cx, cy, tr.x, tr.y));
            p.LineTo(Pt(cx, cy, tl.x, tl.y));
            p.ClosePath();
            p.Stroke();
        }
#endif
    }
}
