using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Invisible full-viewport click catcher sitting behind the map. Clicking empty
/// space while drilled into a territory backs out to the region overview.
///
/// It is the lowest sibling in the viewport, so stars and territory cells always
/// win the raycast — only genuinely empty space reaches this.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class StarMapBackdrop : MonoBehaviour, IPointerClickHandler
{
    Action _onClick;
    Image _surface;

    public void Configure(Action onClick)
    {
        _onClick = onClick;

        if (_surface == null)
        {
            _surface = GetComponent<Image>();
            if (_surface == null) _surface = gameObject.AddComponent<Image>();
        }

        // Fully transparent, but raycastTarget still receives pointers.
        _surface.color = new Color(0f, 0f, 0f, 0f);
        _surface.raycastTarget = true;
    }

    /// <summary>Only listen while there is something to back out of.</summary>
    public void SetActive(bool active)
    {
        if (_surface != null) _surface.raycastTarget = active;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_surface == null || !_surface.raycastTarget) return;
        if (_onClick != null) _onClick();
    }
}
