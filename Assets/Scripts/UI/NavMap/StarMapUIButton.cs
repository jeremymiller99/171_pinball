using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reusable holographic button: a framed label that draws itself as one mesh,
/// with selected / disabled / hover states. Used for the mission panel's ship
/// chips and its Launch and Close actions.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StarMapUIButton : MaskableGraphic,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Color _lineColor = new Color(0.5f, 0.9f, 1f, 0.75f);
    [SerializeField] Color _selectedFill = new Color(0.4f, 0.85f, 1f, 0.22f);
    [SerializeField] Color _disabledColor = new Color(0.4f, 0.5f, 0.55f, 0.35f);
    [SerializeField] float _lineWidth = 1f;

    public event Action<StarMapUIButton> Clicked;

    /// <summary>Arbitrary data the owner wants back on click (e.g. which ship this chip is).</summary>
    public object Payload { get; set; }

    public bool IsSelected { get { return _selected; } }
    public bool IsEnabled { get { return _enabled; } }

    TextMeshProUGUI _text;
    bool _selected;
    bool _enabled = true;
    bool _hovered;
    float _hoverBlend;

    public void Configure(string label, float fontSize)
    {
        raycastTarget = true;
        EnsureText(fontSize);
        _text.text = label;
        ApplyColors();
        SetVerticesDirty();
    }

    void EnsureText(float fontSize)
    {
        if (_text == null)
        {
            var existing = transform.Find("Label");
            _text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        }

        if (_text == null)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _text = go.AddComponent<TextMeshProUGUI>();
            // Font left unset: TMP falls back to the project's default font asset.
            _text.raycastTarget = false;
            _text.alignment = TextAlignmentOptions.Center;
        }

        _text.fontSize = fontSize;
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;
        ApplyColors();
        SetVerticesDirty();
    }

    /// <summary>Disabled buttons still draw, dimmed, but stop accepting clicks.</summary>
    public void SetEnabled(bool value)
    {
        if (_enabled == value) return;
        _enabled = value;
        raycastTarget = value;
        if (!value) { _hovered = false; _hoverBlend = 0f; }
        ApplyColors();
        SetVerticesDirty();
    }

    Color CurrentLineColor()
    {
        if (!_enabled) return _disabledColor;
        return Color.Lerp(_lineColor, Color.white, _hoverBlend * 0.6f);
    }

    void ApplyColors()
    {
        if (_text != null) _text.color = CurrentLineColor();
    }

    void Update()
    {
        float target = (_hovered && _enabled) ? 1f : 0f;
        if (Mathf.Approximately(_hoverBlend, target)) return;

        _hoverBlend = Mathf.MoveTowards(_hoverBlend, target, Time.unscaledDeltaTime * 8f);
        ApplyColors();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        Color line = CurrentLineColor();

        if (_selected && _enabled)
        {
            var vert = UIVertex.simpleVert;
            vert.color = _selectedFill;
            int baseIndex = vh.currentVertCount;

            vert.position = new Vector2(r.xMin, r.yMin); vh.AddVert(vert);
            vert.position = new Vector2(r.xMin, r.yMax); vh.AddVert(vert);
            vert.position = new Vector2(r.xMax, r.yMax); vh.AddVert(vert);
            vert.position = new Vector2(r.xMax, r.yMin); vh.AddVert(vert);

            vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);
        }

        var topLeft     = new Vector2(r.xMin, r.yMax);
        var topRight    = new Vector2(r.xMax, r.yMax);
        var bottomRight = new Vector2(r.xMax, r.yMin);
        var bottomLeft  = new Vector2(r.xMin, r.yMin);

        AddLine(vh, topLeft, topRight, line);
        AddLine(vh, topRight, bottomRight, line);
        AddLine(vh, bottomRight, bottomLeft, line);
        AddLine(vh, bottomLeft, topLeft, line);
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
        if (!_enabled) return;
        var handler = Clicked;
        if (handler != null) handler(this);
    }

    public void OnPointerEnter(PointerEventData eventData) { _hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { _hovered = false; }
}
