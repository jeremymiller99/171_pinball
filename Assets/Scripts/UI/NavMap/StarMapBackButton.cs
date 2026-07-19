using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Holographic "back" affordance shown while the map is drilled into a single
/// territory. Draws its own frame and chevron as one mesh, so it needs no
/// sprite or 9-slice asset, and matches the rest of the map's line art.
///
/// Lives on the viewport rather than the content, so it stays put while the map
/// pans and zooms underneath it.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StarMapBackButton : MaskableGraphic,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Color _idleColor = new Color(0.5f, 0.9f, 1f, 0.7f);
    [SerializeField] Color _hoverColor = new Color(0.85f, 1f, 1f, 1f);
    [SerializeField] float _lineWidth = 1.2f;
    [SerializeField] float _chevronSize = 5f;
    [SerializeField] string _label = "BACK";
    [SerializeField] float _labelSize = 9f;

    Action _onClick;
    TextMeshProUGUI _text;
    float _hoverBlend;
    bool _hovered;

    public void Configure(Action onClick, float labelSize)
    {
        _onClick = onClick;
        _labelSize = labelSize;
        raycastTarget = true;
        EnsureLabel();
        SetVerticesDirty();
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf == visible) return;
        gameObject.SetActive(visible);
        _hovered = false;
        _hoverBlend = 0f;
    }

    void EnsureLabel()
    {
        if (_text == null)
        {
            var existing = transform.Find("Label");
            _text = existing != null
                ? existing.GetComponent<TextMeshProUGUI>()
                : null;
        }

        if (_text == null)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(_chevronSize * 2.4f, 0f);   // clear the chevron
            rt.offsetMax = Vector2.zero;

            _text = go.AddComponent<TextMeshProUGUI>();
            // Font is left unset: TMP falls back to the project's default font
            // asset, so this needs no serialized reference.
            _text.alignment = TextAlignmentOptions.Left;
            _text.raycastTarget = false;
        }

        _text.text = _label;
        _text.fontSize = _labelSize;
        _text.color = CurrentColor();
    }

    Color CurrentColor()
    {
        return Color.Lerp(_idleColor, _hoverColor, _hoverBlend);
    }

    void Update()
    {
        float target = _hovered ? 1f : 0f;
        if (Mathf.Approximately(_hoverBlend, target)) return;

        _hoverBlend = Mathf.MoveTowards(_hoverBlend, target, Time.unscaledDeltaTime * 8f);
        if (_text != null) _text.color = CurrentColor();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        Color color = CurrentColor();

        // Frame
        var topLeft     = new Vector2(r.xMin, r.yMax);
        var topRight    = new Vector2(r.xMax, r.yMax);
        var bottomRight = new Vector2(r.xMax, r.yMin);
        var bottomLeft  = new Vector2(r.xMin, r.yMin);

        AddLine(vh, topLeft, topRight, color);
        AddLine(vh, topRight, bottomRight, color);
        AddLine(vh, bottomRight, bottomLeft, color);
        AddLine(vh, bottomLeft, topLeft, color);

        // Chevron pointing left, vertically centred just inside the frame.
        float x = r.xMin + _chevronSize * 1.6f;
        float half = _chevronSize * 0.5f;
        var tip = new Vector2(x - half, r.center.y);
        AddLine(vh, tip, new Vector2(x + half, r.center.y + _chevronSize * 0.8f), color);
        AddLine(vh, tip, new Vector2(x + half, r.center.y - _chevronSize * 0.8f), color);
    }

    void AddLine(VertexHelper vh, Vector2 a, Vector2 b, Color color)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.0001f) return;

        Vector2 normal = new Vector2(-delta.y, delta.x) / length * (_lineWidth * 0.5f);
        int baseIndex = vh.currentVertCount;

        var vert = UIVertex.simpleVert;
        vert.color = color;

        vert.position = a - normal; vh.AddVert(vert);
        vert.position = a + normal; vh.AddVert(vert);
        vert.position = b + normal; vh.AddVert(vert);
        vert.position = b - normal; vh.AddVert(vert);

        vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
        vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_onClick != null) _onClick();
    }

    public void OnPointerEnter(PointerEventData eventData) { _hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { _hovered = false; }
}
