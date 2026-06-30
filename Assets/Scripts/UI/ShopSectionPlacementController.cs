using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Section/group counterpart to <see cref="ShopComponentPlacementController"/>.
/// Discovers the <see cref="BoardSection"/>s in the loaded board, validates that
/// a group offer's category matches a section, drives the section highlight
/// state during placement, and installs a purchased group into a section.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnifiedShopController))]
public sealed class ShopSectionPlacementController : MonoBehaviour
{
    public readonly List<BoardSection> Sections = new List<BoardSection>();

    private BoardSection _dragHoveredSection;
    private BoardSection _placementHoveredSection;
    private ShopTransitionController _transitionController;

    public void Initialize()
    {
        _transitionController = FindAnyObjectByType<ShopTransitionController>();
        DiscoverSections();
    }

    public void Cleanup()
    {
        DeselectAll();
        _dragHoveredSection = null;
        _placementHoveredSection = null;
    }

    private void DiscoverSections()
    {
        Sections.Clear();

        BoardSection[] all = Object.FindObjectsByType<BoardSection>(FindObjectsSortMode.None);
        foreach (BoardSection s in all)
        {
            if (s == null || s.gameObject == null) continue;
            if (!Sections.Contains(s)) Sections.Add(s);
        }

        foreach (BoardSection s in Sections)
            if (s != null) s.PrewarmSelectionOutline();
    }

    /// <summary>
    /// True when the offer is a component group and its category matches the
    /// section. Null inputs are invalid.
    /// </summary>
    public static bool IsValidSectionTarget(ShopOffer offer, BoardSection section)
    {
        if (offer == null || section == null) return false;
        if (offer.Type != ShopOffer.OfferType.ComponentGroup) return false;
        if (offer.GroupDef == null) return false;
        return offer.GroupDef.Category == section.Category;
    }

    public void SetSelectionStateForPlacement(BoardSectionCategory category)
    {
        foreach (BoardSection s in Sections)
            if (s != null && s.Category == category) s.Select();
    }

    public void DeselectAll()
    {
        foreach (BoardSection s in Sections)
            if (s != null) s.DeSelect();
    }

    public void UpdateDragHover(ShopOffer offer, BoardSection hitSection)
    {
        BoardSection s = IsValidSectionTarget(offer, hitSection) ? hitSection : null;

        if (s != _dragHoveredSection)
        {
            if (_dragHoveredSection != null) _dragHoveredSection.UnhighlightDragTarget();
            _dragHoveredSection = s;
            if (s != null) s.HighlightDragTarget();
        }
    }

    public void EndDragHover()
    {
        DeselectAll();
        _dragHoveredSection = null;
    }

    public void UpdatePlacementHover(ShopOffer offer, BoardSection hitSection)
    {
        BoardSection s = IsValidSectionTarget(offer, hitSection) ? hitSection : null;

        if (s != _placementHoveredSection)
        {
            if (_placementHoveredSection != null) _placementHoveredSection.UnhighlightDragTarget();
            _placementHoveredSection = s;
            if (s != null) s.HighlightDragTarget();
        }
    }

    /// <summary>
    /// Installs the group into the section, then disables any flippers in the
    /// freshly-installed group until the shop closes (mirrors the flipper safety
    /// in <see cref="ShopComponentPlacementController.ReplaceComponent"/>).
    /// </summary>
    public bool InstallGroup(BoardSection target, ComponentGroupDefinition def)
    {
        if (target == null || def == null)
        {
            Debug.LogWarning("[ShopSectionPlacement] InstallGroup: null target or definition.");
            return false;
        }

        if (!target.InstallGroup(def)) return false;

        if (_transitionController != null && target.CurrentGroupInstance != null)
        {
            PinballFlipper[] flippers =
                target.CurrentGroupInstance.GetComponentsInChildren<PinballFlipper>(true);
            foreach (PinballFlipper f in flippers)
                if (f != null) _transitionController.TrackAndDisable(f);
        }

        return true;
    }
}
