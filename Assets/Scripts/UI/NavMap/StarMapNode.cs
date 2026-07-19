using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum StarMapNodeType
{
    Start,
    Standard,
    Elite,
    Shop,
    Boss,
}

/// <summary>
/// One clickable star on the nav map. Handles its own idle pulse and
/// hover/select feedback; the generator owns the graph wiring.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class StarMapNode : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int Index { get; private set; }
    public StarMapNodeType NodeType { get; private set; }
    public Vector2 MapPosition { get; private set; }
    public List<StarMapNode> Neighbours { get; private set; }

    /// <summary>Designation shown in the hover tooltip.</summary>
    public string DisplayName { get; set; }

    /// <summary>Playfield this star drops you into. Assigned by the generator.</summary>
    public BoardDefinition Board { get; set; }
    /// <summary>Mission run on that playfield. Null when the board has no missions authored.</summary>
    public ChallengeModeDefinition Mission { get; set; }

    public event Action<StarMapNode> Clicked;
    public event Action<StarMapNode, bool> HoverChanged;

    Image _core;
    Image _glow;
    Color _baseColor;
    RectTransform _rect;

    float _pulsePhase;
    float _pulseSpeed = 1.6f;
    float _hoverBlend;      // 0 = idle, 1 = fully hovered
    bool  _hovered;
    bool  _selected;

    public void Initialise(int index, StarMapNodeType type, Vector2 mapPosition,
                           Image core, Image glow, Color baseColor, float pulseSpeed)
    {
        Index       = index;
        NodeType    = type;
        MapPosition = mapPosition;
        Neighbours  = new List<StarMapNode>();

        _core       = core;
        _glow       = glow;
        _baseColor  = baseColor;
        _pulseSpeed = pulseSpeed;
        _rect       = (RectTransform)transform;

        // Desync the pulses so the map shimmers instead of blinking in unison.
        _pulsePhase = index * 0.7f;

        ApplyVisual(1f, 0f);
    }

    public bool Selected
    {
        get { return _selected; }
        set { _selected = value; }
    }

    void Update()
    {
        _pulsePhase += Time.unscaledDeltaTime * _pulseSpeed;

        // Ease toward the hover target so entering/leaving doesn't snap.
        float target = (_hovered || _selected) ? 1f : 0f;
        _hoverBlend = Mathf.MoveTowards(_hoverBlend, target, Time.unscaledDeltaTime * 6f);

        float pulse = 1f + Mathf.Sin(_pulsePhase) * 0.06f;
        ApplyVisual(pulse, _hoverBlend);
    }

    void ApplyVisual(float pulse, float hover)
    {
        float scale = pulse * Mathf.Lerp(1f, 1.35f, hover);
        _rect.localScale = new Vector3(scale, scale, 1f);

        if (_core != null)
        {
            Color c = Color.Lerp(_baseColor, Color.white, hover * 0.6f);
            c.a = Mathf.Lerp(0.85f, 1f, hover);
            _core.color = c;
        }

        if (_glow != null)
        {
            Color g = _baseColor;
            // Idle glow breathes with the pulse; hover pushes it much brighter.
            g.a = Mathf.Lerp(0.18f + (pulse - 1f) * 1.5f, 0.55f, hover);
            _glow.color = g;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var handler = Clicked;
        if (handler != null) handler(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
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
