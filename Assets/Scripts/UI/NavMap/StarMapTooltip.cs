using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Holographic hover readout for territories and stars. Draws its own panel and
/// frame as one mesh, so it needs no sprite asset.
///
/// It anchors to the hovered thing's position on the map rather than following
/// the pointer. That keeps it stable, avoids any input-backend dependency, and
/// reads as a callout attached to the object. Because the map pans and zooms
/// underneath, the anchor is stored in content space and re-projected every
/// frame in LateUpdate — after the focuser has moved the content.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StarMapTooltip : MaskableGraphic
{
    [SerializeField] Color _panelColor = new Color(0.03f, 0.09f, 0.13f, 0.85f);
    [SerializeField] Color _frameColor = new Color(0.5f, 0.9f, 1f, 0.8f);
    [SerializeField] float _lineWidth = 1f;
    [SerializeField] Vector2 _textPadding = new Vector2(5f, 3f);
    [SerializeField] float _maxWidth = 110f;
    [SerializeField] Vector2 _anchorOffset = new Vector2(9f, 9f);

    RectTransform _viewport;
    RectTransform _content;
    TextMeshProUGUI _text;
    Vector2 _anchorPoint;
    bool _showing;

    public void Configure(RectTransform viewport, RectTransform content, float fontSize)
    {
        _viewport = viewport;
        _content = content;
        raycastTarget = false;   // must never eat the hover it is reporting on

        EnsureText(fontSize);
        Hide();
    }

    void EnsureText(float fontSize)
    {
        if (_text == null)
        {
            var existing = transform.Find("Text");
            _text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        }

        if (_text == null)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(_textPadding.x, _textPadding.y);
            rt.offsetMax = new Vector2(-_textPadding.x, -_textPadding.y);

            _text = go.AddComponent<TextMeshProUGUI>();
            // Font left unset: TMP falls back to the project's default font asset.
            _text.raycastTarget = false;
            _text.alignment = TextAlignmentOptions.TopLeft;
        }

        _text.fontSize = fontSize;
        _text.color = _frameColor;
    }

    /// <param name="contentLocalPoint">Where on the map this tooltip belongs, in content space.</param>
    public void Show(string body, Vector2 contentLocalPoint)
    {
        if (_text == null) return;

        _anchorPoint = contentLocalPoint;
        _showing = true;

        _text.text = body;

        // Size the panel to the text rather than guessing a fixed box.
        Vector2 preferred = _text.GetPreferredValues(body, _maxWidth, 0f);
        preferred.x = Mathf.Min(preferred.x, _maxWidth);
        rectTransform.sizeDelta = preferred + _textPadding * 2f;

        gameObject.SetActive(true);
        SetVerticesDirty();
        Reposition();
    }

    public void Hide()
    {
        _showing = false;
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        // After the focuser has moved the content this frame.
        if (_showing) Reposition();
    }

    void Reposition()
    {
        if (_viewport == null || _content == null) return;

        // Content space -> world -> viewport space, so this survives any pan,
        // zoom or rotation applied to the content.
        Vector3 world = _content.TransformPoint(_anchorPoint);
        Vector2 local = _viewport.InverseTransformPoint(world);

        Vector2 size = rectTransform.rect.size;
        Vector2 target = local + _anchorOffset;

        // Flip to the other side of the anchor rather than clipping at an edge.
        Rect viewRect = _viewport.rect;
        if (target.x + size.x > viewRect.xMax) target.x = local.x - _anchorOffset.x - size.x;
        if (target.y + size.y > viewRect.yMax) target.y = local.y - _anchorOffset.y - size.y;

        target.x = Mathf.Clamp(target.x, viewRect.xMin, Mathf.Max(viewRect.xMin, viewRect.xMax - size.x));
        target.y = Mathf.Clamp(target.y, viewRect.yMin, Mathf.Max(viewRect.yMin, viewRect.yMax - size.y));

        rectTransform.anchoredPosition = target;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;

        // Panel
        var vert = UIVertex.simpleVert;
        vert.color = _panelColor;
        int baseIndex = vh.currentVertCount;

        vert.position = new Vector2(r.xMin, r.yMin); vh.AddVert(vert);
        vert.position = new Vector2(r.xMin, r.yMax); vh.AddVert(vert);
        vert.position = new Vector2(r.xMax, r.yMax); vh.AddVert(vert);
        vert.position = new Vector2(r.xMax, r.yMin); vh.AddVert(vert);

        vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
        vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);

        // Frame
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
