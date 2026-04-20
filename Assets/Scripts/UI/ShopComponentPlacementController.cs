using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnifiedShopController))]
public sealed class ShopComponentPlacementController : MonoBehaviour
{
    public readonly List<BoardComponent> Bumpers = new List<BoardComponent>();
    public readonly List<BoardComponent> Targets = new List<BoardComponent>();
    public readonly List<BoardComponent> Flippers = new List<BoardComponent>();

    private BoardComponent _dragHoveredComponent;
    private BoardComponent _placementHoveredComponent;
    private ShopTransitionController _transitionController;

    public void Initialize()
    {
        _transitionController = FindAnyObjectByType<ShopTransitionController>();
        DiscoverBoardComponents();
    }

    public void Cleanup()
    {
        DeselectAll();
        _dragHoveredComponent = null;
        _placementHoveredComponent = null;
    }

    private void DiscoverBoardComponents()
    {
        Bumpers.Clear();
        Targets.Clear();
        Flippers.Clear();

        BoardComponent[] all = Object.FindObjectsByType<BoardComponent>(FindObjectsSortMode.None);
        foreach (BoardComponent bc in all)
        {
            if (bc == null || bc.gameObject == null) continue;

            if (bc.componentType == BoardComponentType.Bumper && !Bumpers.Contains(bc))
                Bumpers.Add(bc);
            else if (bc.componentType == BoardComponentType.Target && !Targets.Contains(bc))
                Targets.Add(bc);
            else if (bc.componentType == BoardComponentType.Flipper && !Flippers.Contains(bc))
                Flippers.Add(bc);
        }

        Bumpers.Sort();
        Targets.Sort();
        Flippers.Sort();

        PrewarmOutlines();
    }

    private void PrewarmOutlines()
    {
        foreach (BoardComponent bc in Bumpers) if (bc != null) bc.PrewarmSelectionOutline();
        foreach (BoardComponent bc in Targets) if (bc != null) bc.PrewarmSelectionOutline();
        foreach (BoardComponent bc in Flippers) if (bc != null) bc.PrewarmSelectionOutline();
    }

    public void SetSelectionStateForPlacement(BoardComponentType type)
    {
        if (type == BoardComponentType.Bumper)
        {
            foreach (BoardComponent bc in Bumpers) if (bc != null) bc.Select();
        }
        else if (type == BoardComponentType.Target)
        {
            foreach (BoardComponent bc in Targets) if (bc != null) bc.Select();
        }
        else if (type == BoardComponentType.Flipper)
        {
            foreach (BoardComponent bc in Flippers) if (bc != null) bc.Select();
        }
    }

    public void DeselectAll()
    {
        foreach (BoardComponent bc in Bumpers) if (bc != null) bc.DeSelect();
        foreach (BoardComponent bc in Targets) if (bc != null) bc.DeSelect();
        foreach (BoardComponent bc in Flippers) if (bc != null) bc.DeSelect();
    }

    /// <summary>
    /// True when the offer is a board component and its type matches the hit component.
    /// Null inputs are invalid.
    /// </summary>
    public static bool IsValidPlacementTarget(ShopOffer offer, BoardComponent hitComponent)
    {
        if (offer == null || hitComponent == null) return false;
        if (offer.Type != ShopOffer.OfferType.BoardComponent) return false;
        if (offer.ComponentDef == null) return false;
        return offer.ComponentDef.ComponentType == hitComponent.componentType;
    }

    public void UpdateDragHover(ShopOffer offer, BoardComponent hitComponent)
    {
        BoardComponent bc = IsValidPlacementTarget(offer, hitComponent) ? hitComponent : null;

        if (bc != _dragHoveredComponent)
        {
            if (_dragHoveredComponent != null) _dragHoveredComponent.UnhighlightDragTarget();
            _dragHoveredComponent = bc;
            if (bc != null) bc.HighlightDragTarget();
        }
    }

    public void EndDragHover()
    {
        DeselectAll();
        _dragHoveredComponent = null;
    }

    public void UpdatePlacementHover(ShopOffer offer, BoardComponent hitComponent)
    {
        BoardComponent bc = IsValidPlacementTarget(offer, hitComponent) ? hitComponent : null;

        if (bc != _placementHoveredComponent)
        {
            if (_placementHoveredComponent != null) _placementHoveredComponent.UnhighlightDragTarget();
            _placementHoveredComponent = bc;
            if (bc != null) bc.HighlightDragTarget();
        }
    }

    public bool ReplaceComponent(BoardComponent targetComponent, BoardComponentDefinition newDef)
    {
        if (targetComponent == null || newDef == null)
        {
            Debug.LogWarning("[ShopComponentPlacement] ReplaceComponent: null target or definition.");
            return false;
        }

        if (newDef.Prefab == null)
        {
            Debug.LogError($"[ShopComponentPlacement] ReplaceComponent: '{newDef.GetSafeDisplayName()}' has no prefab assigned.");
            return false;
        }

        GameObject newGo = Instantiate(newDef.Prefab, targetComponent.transform.parent);
        newGo.transform.position = targetComponent.transform.position;
        newGo.transform.rotation = targetComponent.transform.rotation;
        newGo.transform.localScale = targetComponent.startingSize;

        BoardComponent newComp = newGo.GetComponent<BoardComponent>();
        if (newComp == null)
        {
            Debug.LogError($"[ShopComponentPlacement] ReplaceComponent: prefab '{newDef.GetSafeDisplayName()}' has no BoardComponent script.");
            Destroy(newGo);
            return false;
        }

        newComp.startingSize = targetComponent.startingSize;

        if (newDef.ComponentType == BoardComponentType.Flipper)
        {
            PinballFlipper newFlipper = newComp.GetComponent<PinballFlipper>();
            PinballFlipper oldFlipper = targetComponent.GetComponent<PinballFlipper>();
            if (newFlipper == null || oldFlipper == null)
            {
                Debug.LogError("[ShopComponentPlacement] ReplaceComponent: flipper replacement missing PinballFlipper component (new=" +
                               (newFlipper != null) + ", old=" + (oldFlipper != null) + ").");
                Destroy(newGo);
                return false;
            }
            newFlipper.CopyFlipperProperties(oldFlipper);
            //Disable the new flipper's ability to flip until the player leaves the shop.
            _transitionController.TrackAndDisable(newFlipper);
        }

        if (newDef.ComponentType == BoardComponentType.Bumper)
        {
            Bumpers.Remove(targetComponent);
            Bumpers.Add(newComp);
            Bumpers.Sort();
        }
        else if (newDef.ComponentType == BoardComponentType.Target)
        {
            Targets.Remove(targetComponent);
            Targets.Add(newComp);
            Targets.Sort();
        }
        else if (newDef.ComponentType == BoardComponentType.Flipper)
        {
            Flippers.Remove(targetComponent);
            Flippers.Add(newComp);
            Flippers.Sort();
        }

        Destroy(targetComponent.gameObject);
        return true;
    }
}
