using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnifiedShopController))]
public sealed class ShopComponentPlacementController : MonoBehaviour
{
    private readonly List<BoardComponent> _bumpers = new List<BoardComponent>();
    private readonly List<BoardComponent> _targets = new List<BoardComponent>();
    private readonly List<BoardComponent> _flippers = new List<BoardComponent>();

    private BoardComponent _dragHoveredComponent;
    private BoardComponent _placementHoveredComponent;

    public void Initialize()
    {
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
        _bumpers.Clear();
        _targets.Clear();
        _flippers.Clear();

        BoardComponent[] all = Object.FindObjectsByType<BoardComponent>(FindObjectsSortMode.None);
        foreach (BoardComponent bc in all)
        {
            if (bc == null || bc.gameObject == null) continue;

            if (bc.componentType == BoardComponentType.Bumper && !_bumpers.Contains(bc))
                _bumpers.Add(bc);
            else if (bc.componentType == BoardComponentType.Target && !_targets.Contains(bc))
                _targets.Add(bc);
            else if (bc.componentType == BoardComponentType.Flipper && !_flippers.Contains(bc))
                _flippers.Add(bc);
        }

        _bumpers.Sort();
        _targets.Sort();
        _flippers.Sort();

        PrewarmOutlines();
    }

    private void PrewarmOutlines()
    {
        foreach (BoardComponent bc in _bumpers) if (bc != null) bc.PrewarmSelectionOutline();
        foreach (BoardComponent bc in _targets) if (bc != null) bc.PrewarmSelectionOutline();
        foreach (BoardComponent bc in _flippers) if (bc != null) bc.PrewarmSelectionOutline();
    }

    public void SetSelectionStateForPlacement(BoardComponentType type)
    {
        if (type == BoardComponentType.Bumper)
        {
            foreach (BoardComponent bc in _bumpers) if (bc != null) bc.Select();
        }
        else if (type == BoardComponentType.Target)
        {
            foreach (BoardComponent bc in _targets) if (bc != null) bc.Select();
        }
        else if (type == BoardComponentType.Flipper)
        {
            foreach (BoardComponent bc in _flippers) if (bc != null) bc.Select();
        }
    }

    public void DeselectAll()
    {
        foreach (BoardComponent bc in _bumpers) if (bc != null) bc.DeSelect();
        foreach (BoardComponent bc in _targets) if (bc != null) bc.DeSelect();
        foreach (BoardComponent bc in _flippers) if (bc != null) bc.DeSelect();
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
        }

        if (newDef.ComponentType == BoardComponentType.Bumper)
        {
            _bumpers.Remove(targetComponent);
            _bumpers.Add(newComp);
            _bumpers.Sort();
        }
        else if (newDef.ComponentType == BoardComponentType.Target)
        {
            _targets.Remove(targetComponent);
            _targets.Add(newComp);
            _targets.Sort();
        }
        else if (newDef.ComponentType == BoardComponentType.Flipper)
        {
            _flippers.Remove(targetComponent);
            _flippers.Add(newComp);
            _flippers.Sort();
        }

        Destroy(targetComponent.gameObject);
        return true;
    }
}
