using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A filled, framed holographic panel drawn as one mesh. Used as the window
/// chrome for the mission popup, and as the dimming blocker behind it.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StarMapFramePanel : MaskableGraphic
{
    [SerializeField] Color _fillColor = new Color(0.02f, 0.08f, 0.12f, 0.92f);
    [SerializeField] Color _frameColor = new Color(0.5f, 0.9f, 1f, 0.85f);
    [SerializeField] float _lineWidth = 1.2f;
    [SerializeField] bool _drawFrame = true;

    public void SetStyle(Color fill, Color frame, float lineWidth, bool drawFrame)
    {
        _fillColor = fill;
        _frameColor = frame;
        _lineWidth = lineWidth;
        _drawFrame = drawFrame;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;

        var vert = UIVertex.simpleVert;
        vert.color = _fillColor;
        int baseIndex = vh.currentVertCount;

        vert.position = new Vector2(r.xMin, r.yMin); vh.AddVert(vert);
        vert.position = new Vector2(r.xMin, r.yMax); vh.AddVert(vert);
        vert.position = new Vector2(r.xMax, r.yMax); vh.AddVert(vert);
        vert.position = new Vector2(r.xMax, r.yMin); vh.AddVert(vert);

        vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
        vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);

        if (!_drawFrame) return;

        var topLeft     = new Vector2(r.xMin, r.yMax);
        var topRight    = new Vector2(r.xMax, r.yMax);
        var bottomRight = new Vector2(r.xMax, r.yMin);
        var bottomLeft  = new Vector2(r.xMin, r.yMin);

        AddLine(vh, topLeft, topRight);
        AddLine(vh, topRight, bottomRight);
        AddLine(vh, bottomRight, bottomLeft);
        AddLine(vh, bottomLeft, topLeft);
    }

    void AddLine(VertexHelper vh, Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.0001f) return;

        Vector2 normal = new Vector2(-delta.y, delta.x) / length * (_lineWidth * 0.5f);
        int baseIndex = vh.currentVertCount;

        var vert = UIVertex.simpleVert;
        vert.color = _frameColor;

        vert.position = a - normal; vh.AddVert(vert);
        vert.position = a + normal; vh.AddVert(vert);
        vert.position = b + normal; vh.AddVert(vert);
        vert.position = b - normal; vh.AddVert(vert);

        vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
        vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);
    }
}
