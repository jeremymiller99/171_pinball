// Generated with Antigravity by jjmil on 2026-03-29.
// Extracted from BoardComponent to decouple Portal knowledge
// from the base class.
using UnityEngine;

/// <summary>
/// Manages the paired-exit outline for a Portal's
/// BoardComponent. Attach to the same GameObject that has
/// both a <see cref="Portal"/> and a
/// <see cref="BoardComponent"/>.
///
/// Implements <see cref="IBoardComponentSelectionListener"/>
/// so BoardComponent automatically notifies this extension
/// when selection state changes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Portal))]
[RequireComponent(typeof(BoardComponent))]
public sealed class PortalOutlineExtension
    : MonoBehaviour, IBoardComponentSelectionListener
{
    private const int outlineRenderQueueOffset = 100;
    private const int outlineStencilRef = 2;

    private BoardComponent _boardComponent;
    private Portal _portal;
    private Outline _exitOutline;
    private Transform _cachedPortalExit;

    private void Awake()
    {
        _boardComponent = GetComponent<BoardComponent>();
        _portal = GetComponent<Portal>();
    }

    private void OnEnable()
    {
        ApplyExitOutline(false);
    }

    public void OnBoardComponentSelected()
    {
        ApplyExitOutline(true);
    }

    public void OnBoardComponentDeselected()
    {
        ApplyExitOutline(false);
    }

    public void OnBoardComponentPrewarmed()
    {
        ApplyExitOutline(false);
    }

    private void ApplyExitOutline(bool isHighlighted)
    {
        if (_portal == null || _portal.portalExit == null)
        {
            if (_exitOutline != null)
            {
                _exitOutline.enabled = false;
            }

            _exitOutline = null;
            _cachedPortalExit = null;
            return;
        }

        if (_cachedPortalExit != _portal.portalExit)
        {
            _cachedPortalExit = _portal.portalExit;
            _exitOutline = null;
        }

        if (_exitOutline == null)
        {
            _exitOutline =
                _cachedPortalExit.GetComponent<Outline>();

            if (_exitOutline == null)
            {
                _exitOutline = _cachedPortalExit.gameObject
                    .AddComponent<Outline>();
            }
        }

        if (_boardComponent == null)
        {
            _boardComponent = GetComponent<BoardComponent>();
        }

        if (isHighlighted)
        {
            ApplyOutlineSettings(
                _exitOutline,
                _boardComponent.ConfirmOutlineColor);
        }
        else
        {
            ApplyOutlineSettings(
                _exitOutline,
                _boardComponent.DefaultOutlineColor);
        }

        _exitOutline.enabled = true;
    }

    private void ApplyOutlineSettings(
        Outline outline, Color color)
    {
        if (outline == null || _boardComponent == null)
        {
            return;
        }

        outline.RenderQueueOffset = outlineRenderQueueOffset;
        outline.StencilRef = outlineStencilRef;
        outline.OutlineMode =
            _boardComponent.SelectionOutlineMode;
        outline.OutlineColor = color;
        outline.OutlineWidth = _boardComponent.OutlineWidth;
    }
}
