using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws every edge of the star map as a single mesh, so the whole link web
/// costs one draw call instead of one Image per line.
/// Edge endpoints are in this RectTransform's local space.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StarMapLinkRenderer : MaskableGraphic
{
    public struct Link
    {
        public Vector2 A;
        public Vector2 B;
        public Color Color;
        public float Width;
    }

    readonly List<Link> _links = new List<Link>();

    [SerializeField] float _defaultWidth = 2f;
    [SerializeField] Color _defaultColor = new Color(0.35f, 0.85f, 1f, 0.35f);

    public float DefaultWidth { get { return _defaultWidth; } set { _defaultWidth = value; } }
    public Color DefaultColor { get { return _defaultColor; } set { _defaultColor = value; } }

    public void Clear()
    {
        _links.Clear();
        SetVerticesDirty();
    }

    public void AddLink(Vector2 a, Vector2 b)
    {
        AddLink(a, b, _defaultColor, _defaultWidth);
    }

    public void AddLink(Vector2 a, Vector2 b, Color color, float width)
    {
        _links.Add(new Link { A = a, B = b, Color = color, Width = width });
        SetVerticesDirty();
    }

    /// <summary>Recolour an already-added link, e.g. to highlight a route.</summary>
    public void SetLinkColor(int index, Color color)
    {
        if (index < 0 || index >= _links.Count) return;
        Link l = _links[index];
        l.Color = color;
        _links[index] = l;
        SetVerticesDirty();
    }

    public int LinkCount { get { return _links.Count; } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        for (int i = 0; i < _links.Count; i++)
        {
            Link link = _links[i];
            Vector2 dir = link.B - link.A;
            float length = dir.magnitude;
            if (length < 0.0001f) continue;

            // Perpendicular offset gives the line its thickness.
            Vector2 normal = new Vector2(-dir.y, dir.x) / length * (link.Width * 0.5f);

            int baseIndex = vh.currentVertCount;
            var vert = UIVertex.simpleVert;
            vert.color = link.Color;

            vert.position = link.A - normal; vert.uv0 = new Vector2(0f, 0f); vh.AddVert(vert);
            vert.position = link.A + normal; vert.uv0 = new Vector2(0f, 1f); vh.AddVert(vert);
            vert.position = link.B + normal; vert.uv0 = new Vector2(1f, 1f); vh.AddVert(vert);
            vert.position = link.B - normal; vert.uv0 = new Vector2(1f, 0f); vh.AddVert(vert);

            vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);
        }
    }
}
