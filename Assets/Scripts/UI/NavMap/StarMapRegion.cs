using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One territory on the zoomed-out map: a filled Voronoi polygon with a hard
/// border, drawn as a single mesh and clickable on its true polygon shape
/// rather than its bounding rect.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StarMapRegion : MaskableGraphic, ICanvasRaycastFilter,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int Index { get; private set; }
    public Vector2 Centre { get; private set; }
    public IList<Vector2> Polygon { get { return _polygon; } }

    public event Action<StarMapRegion> Clicked;
    public event Action<StarMapRegion, bool> HoverChanged;

    /// <summary>Name shown in the hover tooltip.</summary>
    public string DisplayName { get; set; }
    /// <summary>Number of stars inside this territory, for the tooltip.</summary>
    public int StarCount { get; set; }

    readonly List<Vector2> _polygon = new List<Vector2>();
    // Cached ear-clipping result: the outline only changes on Initialise, so
    // there's no reason to re-triangulate on every hover repaint.
    readonly List<int> _triangles = new List<int>();

    Color _fillColor = new Color(0.3f, 0.7f, 1f, 0.12f);
    Color _borderColor = new Color(0.5f, 0.9f, 1f, 0.85f);
    float _borderWidth = 2f;

    float _hoverBlend;
    bool _hovered;
    bool _interactive = true;

    public void Initialise(int index, Vector2 centre, IList<Vector2> polygon,
                           Color fillColor, Color borderColor, float borderWidth)
    {
        Index = index;
        Centre = centre;

        _polygon.Clear();
        if (polygon != null) _polygon.AddRange(polygon);

        _triangles.Clear();
        _triangles.AddRange(PolygonUtility.Triangulate(_polygon));

        _fillColor = fillColor;
        _borderColor = borderColor;
        _borderWidth = borderWidth;

        SetVerticesDirty();
    }

    /// <summary>
    /// Regions stop accepting clicks and fade back once the map drills into a
    /// single region, so they read as context rather than targets.
    /// </summary>
    public void SetInteractive(bool interactive)
    {
        _interactive = interactive;
        raycastTarget = interactive;
        if (!interactive) _hovered = false;
    }

    public void SetColors(Color fillColor, Color borderColor)
    {
        _fillColor = fillColor;
        _borderColor = borderColor;
        SetVerticesDirty();
    }

    void Update()
    {
        float target = (_hovered && _interactive) ? 1f : 0f;
        if (Mathf.Approximately(_hoverBlend, target)) return;

        _hoverBlend = Mathf.MoveTowards(_hoverBlend, target, Time.unscaledDeltaTime * 6f);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_polygon.Count < 3) return;

        Color fill = _fillColor;
        Color border = _borderColor;
        if (_hoverBlend > 0f)
        {
            // Hover brightens the fill and firms up the border.
            fill = Color.Lerp(fill, new Color(fill.r, fill.g, fill.b, fill.a * 3.5f), _hoverBlend);
            border = Color.Lerp(border, Color.white, _hoverBlend * 0.5f);
        }

        AddFill(vh, fill);
        AddBorder(vh, border);
    }

    /// <summary>
    /// Ear-clipped fill. Roughened borders make the cell concave, so a centroid
    /// fan would bleed outside the outline at every inward bend.
    /// </summary>
    void AddFill(VertexHelper vh, Color color)
    {
        if (_triangles.Count == 0) return;

        var vert = UIVertex.simpleVert;
        vert.color = color;

        for (int i = 0; i < _polygon.Count; i++)
        {
            vert.position = _polygon[i];
            vh.AddVert(vert);
        }

        for (int i = 0; i + 2 < _triangles.Count; i += 3)
            vh.AddTriangle(_triangles[i], _triangles[i + 1], _triangles[i + 2]);
    }

    /// <summary>
    /// Quads centred on each edge. Neighbouring cells share an edge exactly, so
    /// their borders overlap and read as one crisp line rather than two.
    /// </summary>
    void AddBorder(VertexHelper vh, Color color)
    {
        var vert = UIVertex.simpleVert;
        vert.color = color;
        float half = _borderWidth * 0.5f;

        for (int i = 0; i < _polygon.Count; i++)
        {
            Vector2 a = _polygon[i];
            Vector2 b = _polygon[(i + 1) % _polygon.Count];

            Vector2 direction = b - a;
            float length = direction.magnitude;
            if (length < 0.0001f) continue;

            Vector2 normal = new Vector2(-direction.y, direction.x) / length * half;
            int baseIndex = vh.currentVertCount;

            vert.position = a - normal; vh.AddVert(vert);
            vert.position = a + normal; vh.AddVert(vert);
            vert.position = b + normal; vh.AddVert(vert);
            vert.position = b - normal; vh.AddVert(vert);

            vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);
        }
    }

    /// <summary>
    /// Hit-test the polygon itself. Without this every region would claim its
    /// whole bounding rect and the cells would steal each other's clicks.
    /// Comes from ICanvasRaycastFilter — Graphic does not declare it, so this
    /// implements the interface rather than overriding a base method.
    /// </summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!_interactive || _polygon.Count < 3) return false;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out local))
            return false;

        return VoronoiPartition.ContainsPoint(_polygon, local);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_interactive) return;
        var handler = Clicked;
        if (handler != null) handler(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactive) return;
        _hovered = true;
        var handler = HoverChanged;
        if (handler != null) handler(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        var handler = HoverChanged;
        if (handler != null) handler(this, false);
    }
}
