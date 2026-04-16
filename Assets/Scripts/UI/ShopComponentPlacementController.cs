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

    public void UpdateDragHover(ShopOffer offer, BoardComponent hitComponent)
    {
        BoardComponent bc = hitComponent;

        if (bc != null && offer != null && offer.ComponentDef != null)
        {
            BoardComponentType typeOfOffer = offer.ComponentDef.ComponentType;
            if (typeOfOffer != bc.componentType)
            {
                bc = null;
            }
        }

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
        BoardComponent bc = hitComponent;

        if (bc != null && offer != null && offer.ComponentDef != null)
        {
            BoardComponentType typeOfOffer = offer.ComponentDef.ComponentType;
            if (typeOfOffer != bc.componentType)
            {
                bc = null;
            }
        }

        if (bc != _placementHoveredComponent)
        {
            if (_placementHoveredComponent != null) _placementHoveredComponent.UnhighlightDragTarget();
            _placementHoveredComponent = bc;
            if (bc != null) bc.HighlightDragTarget();
        }
    }

    public void ReplaceComponent(BoardComponent targetComponent, BoardComponentDefinition newDef)
    {
        if (targetComponent == null || newDef == null) return;

        GameObject newGo = Instantiate(newDef.Prefab, targetComponent.transform.parent);
        newGo.transform.position = targetComponent.transform.position;
        newGo.transform.rotation = targetComponent.transform.rotation;
        newGo.transform.localScale = targetComponent.startingSize;

        BoardComponent newComp = newGo.GetComponent<BoardComponent>();
        if (newComp != null)
        {
            newComp.startingSize = targetComponent.startingSize;
        }

        if (newDef.ComponentType == BoardComponentType.Bumper)
        {
            Bumpers.Add(newComp);
            Bumpers.Remove(targetComponent);
            Bumpers.Sort();
        }
        else if (newDef.ComponentType == BoardComponentType.Target)
        {
            Targets.Add(newComp);
            Targets.Remove(targetComponent);
            Targets.Sort();
        } else if (newDef.ComponentType == BoardComponentType.Flipper)
        {
            Debug.Log("new:" + newComp.gameObject);
            Debug.Log("old:" + targetComponent.gameObject);
            Flippers.Add(newComp);
            Flippers.Remove(targetComponent);
            Flippers.Sort();
            newComp.GetComponent<PinballFlipper>().CopyFlipperProperties(targetComponent.GetComponent<PinballFlipper>());
        }

        Destroy(targetComponent.gameObject);
    }
}
