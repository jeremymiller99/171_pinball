// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (lift modal panels above floating text).
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lifts a full-screen panel above everything else drawn by its canvas.
///
/// Score, mult and coin popups are instantiated as children of the gameplay canvas, so they land
/// as the last sibling and — since draw order inside one canvas is hierarchy order — paint over
/// any panel that was authored earlier in the hierarchy. Rather than fight sibling indices every
/// time a popup spawns, the panel gets its own nested canvas with <c>overrideSorting</c>, which
/// sorts it against the rest of the canvas by number instead of by position.
///
/// The nested canvas needs its own raycaster: a <see cref="Graphic"/> registers with the nearest
/// enabled canvas above it, and a <see cref="GraphicRaycaster"/> only looks at its own canvas, so
/// without one the panel's buttons would render on top but stop taking clicks.
/// </summary>
public static class ModalUiLayer
{
    /// <summary>
    /// Level-up modifier card and the module choice cards. Below the pause menu so pausing over
    /// a card still puts the pause menu on top.
    /// </summary>
    public const int CardPopupSortingOrder = 8900;

    /// <summary>Pause menu and the settings panel it opens.</summary>
    public const int PauseSortingOrder = 9000;

    /// <summary>Round failed. Above the pause menu: if both are somehow up, the run is over.</summary>
    public const int RoundFailedSortingOrder = 9100;

    /// <summary>
    /// Puts <paramref name="panel"/> on its own sorting layer at <paramref name="sortingOrder"/>.
    /// Idempotent — re-showing a panel reuses the components it already has.
    /// </summary>
    public static void Elevate(GameObject panel, int sortingOrder)
    {
        if (panel == null) return;

        // A panel outside any canvas has nothing to sort against, and adding a lone Canvas to it
        // would change how it renders rather than just where in the order.
        if (panel.GetComponentInParent<Canvas>(includeInactive: true) == null) return;

        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = panel.AddComponent<Canvas>();
        }

        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        if (panel.GetComponent<GraphicRaycaster>() == null)
        {
            panel.AddComponent<GraphicRaycaster>();
        }
    }
}
