using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnifiedShopController))]
public sealed class ShopHandInteractionController : MonoBehaviour
{
    [Header("Drag-drop")]
    [Tooltip("Ray distance to hand balls for ball-offer drops when colliders are off.")]
    [SerializeField] private float handBallDropRayRadius = 0.25f;

    [Header("Drag Preview Visuals")]
    [SerializeField] private Color handBallSelectColor = Color.white;
    [SerializeField] private Color handBallDragTargetColor = new Color(0.3f, 1f, 0.3f);
    private const float HandBallOutlineWidth = 5f;

    [Tooltip("Scale multiplier applied to a hand ball when hovered or clicked.")]
    [SerializeField] private float ballHoverScaleFactor = 1.5f;

    private BallSpawner _ballSpawner;
    private GameRulesManager _rulesManager;
    private UnifiedShopController _shop;

    private int _dragHoveredBallSlot = -1;
    private int _placementHoveredBallSlot = -1;
    private int _swapSelectedSlot = -1;

    private void Awake()
    {
        _shop = GetComponent<UnifiedShopController>();
    }

    public void Initialize()
    {
        _ballSpawner = ServiceLocator.Get<BallSpawner>();
        _rulesManager = ServiceLocator.Get<GameRulesManager>();
        
        ClearState();
    }

    public void Cleanup()
    {
        UnhighlightAllHandBalls();
        ClearSwapSelection();
        ClearState();
        if (_ballSpawner != null) _ballSpawner.ClearInsertGapPreview();
    }

    private void ClearState()
    {
        _dragHoveredBallSlot = -1;
        _placementHoveredBallSlot = -1;
        _swapSelectedSlot = -1;
    }

    public bool TryGetHandBallSlotFromRay(Ray ray, out int slotIndex)
    {
        slotIndex = -1;
        if (_ballSpawner == null) return false;

        var handBalls = _ballSpawner.HandBalls;
        if (handBalls == null || handBalls.Count == 0) return false;

        float radiusSq = handBallDropRayRadius * handBallDropRayRadius;
        float bestDistSq = float.MaxValue;
        GameObject bestBall = null;

        for (int i = 0; i < handBalls.Count; i++)
        {
            GameObject b = handBalls[i];
            if (b == null) continue;

            Vector3 toCenter = b.transform.position - ray.origin;
            float dot = Vector3.Dot(toCenter, ray.direction);
            if (dot < 0f) continue;

            Vector3 closest = ray.origin + ray.direction * dot;
            float distSq = (b.transform.position - closest).sqrMagnitude;

            if (distSq <= radiusSq && distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestBall = b;
            }
        }

        if (bestBall == null) return false;

        var marker = bestBall.GetComponentInParent<BallHandSlotMarker>();
        if (marker != null && marker.SlotIndex >= 0)
        {
            slotIndex = marker.SlotIndex;
            return true;
        }

        return false;
    }

    public int GetSwapSelectedSlot() => _swapSelectedSlot;

    public void SetSwapSelectedSlot(int slot)
    {
        _swapSelectedSlot = slot;
        if (slot >= 0)
        {
            SetHandBallOutlineColor(slot, handBallDragTargetColor);
            SetBallHoverScale(slot, true);
        }
    }

    public void ClearSwapSelection()
    {
        if (_swapSelectedSlot >= 0)
        {
            RestoreHandBallDefaultOutline(_swapSelectedSlot);
            SetBallHoverScale(_swapSelectedSlot, false);
            _swapSelectedSlot = -1;
        }
    }

    public void HighlightAllHandBallsWaitColor()
    {
        HighlightAllHandBalls(handBallSelectColor);
    }

    public void HighlightAllHandBalls(Color color)
    {
        if (_ballSpawner == null) return;
        var handBalls = _ballSpawner.HandBalls;
        for (int i = 0; i < handBalls.Count; i++)
        {
            if (handBalls[i] != null)
            {
                EnsureBallOutline(handBalls[i], color);
                EnsureBallPulse(handBalls[i], true);
            }
        }
    }

    public void UnhighlightAllHandBalls()
    {
        if (_ballSpawner == null) return;
        var handBalls = _ballSpawner.HandBalls;
        for (int i = 0; i < handBalls.Count; i++)
        {
            if (handBalls[i] != null)
            {
                RestoreBallDefaultOutline(handBalls[i]);
                EnsureBallPulse(handBalls[i], false);
            }
        }
    }

    public void HighlightDropSources(int ignoreSlot)
    {
        if (_ballSpawner == null) return;
        var handBalls = _ballSpawner.HandBalls;
        for (int i = 0; i < handBalls.Count; i++)
        {
            if (handBalls[i] == null) continue;
            var marker = handBalls[i].GetComponent<BallHandSlotMarker>();
            if (marker != null && marker.SlotIndex != ignoreSlot)
            {
                EnsureBallOutline(handBalls[i], handBallSelectColor);
            }
        }
    }

    public void UpdateDragHover(int hoveredSlot)
    {
        if (hoveredSlot != _dragHoveredBallSlot)
        {
            if (_dragHoveredBallSlot >= 0)
            {
                SetHandBallOutlineColor(_dragHoveredBallSlot, handBallSelectColor);
                SetBallHoverScale(_dragHoveredBallSlot, false);
            }

            _dragHoveredBallSlot = hoveredSlot;

            if (hoveredSlot >= 0)
            {
                SetHandBallOutlineColor(hoveredSlot, handBallDragTargetColor);
                SetBallHoverScale(hoveredSlot, true);
            }
        }
    }

    public void EndDragHover()
    {
        UnhighlightAllHandBalls();
        _dragHoveredBallSlot = -1;
    }

    public void UpdatePlacementHover(int slot)
    {
        if (slot != _placementHoveredBallSlot)
        {
            if (_placementHoveredBallSlot >= 0)
            {
                SetHandBallOutlineColor(_placementHoveredBallSlot, handBallSelectColor);
                SetBallHoverScale(_placementHoveredBallSlot, false);
            }

            _placementHoveredBallSlot = slot;

            if (slot >= 0)
            {
                SetHandBallOutlineColor(slot, handBallDragTargetColor);
                SetBallHoverScale(slot, true);
            }
        }
    }

    public void ClearPlacementHover()
    {
        if (_placementHoveredBallSlot >= 0)
        {
            SetHandBallOutlineColor(_placementHoveredBallSlot, handBallSelectColor);
            SetBallHoverScale(_placementHoveredBallSlot, false);
            _placementHoveredBallSlot = -1;
        }
    }

    private void SetHandBallOutlineColor(int loadoutSlotIndex, Color color)
    {
        if (_ballSpawner == null) return;
        var handBalls = _ballSpawner.HandBalls;
        for (int i = 0; i < handBalls.Count; i++)
        {
            if (handBalls[i] == null) continue;
            var marker = handBalls[i].GetComponent<BallHandSlotMarker>();
            if (marker != null && marker.SlotIndex == loadoutSlotIndex)
            {
                EnsureBallOutline(handBalls[i], color);
                return;
            }
        }
    }

    private static void EnsureBallOutline(GameObject ball, Color color)
    {
        Outline outline = ball.GetComponentInChildren<Outline>();
        if (outline == null)
        {
            Renderer rend = ball.GetComponentInChildren<Renderer>();
            if (rend != null) outline = rend.gameObject.AddComponent<Outline>();
        }

        if (outline != null)
        {
            outline.OutlineMode = Outline.Mode.OutlineAll;
            outline.OutlineColor = color;
            outline.OutlineWidth = HandBallOutlineWidth;
            outline.enabled = true;
        }
    }

    private static void EnsureBallPulse(GameObject ball, bool active)
    {
        if (ball == null) return;
        BallPulse pulse = ball.GetComponent<BallPulse>();

        if (active)
        {
            if (pulse == null) pulse = ball.AddComponent<BallPulse>();
            pulse.StartPulse();
        }
        else if (pulse != null)
        {
            pulse.ResetAll();
        }
    }

    private void SetBallHoverScale(int loadoutSlotIndex, bool scaleUp)
    {
        if (_ballSpawner == null) return;
        var handBalls = _ballSpawner.HandBalls;
        for (int i = 0; i < handBalls.Count; i++)
        {
            if (handBalls[i] == null) continue;
            var marker = handBalls[i].GetComponent<BallHandSlotMarker>();
            if (marker == null || marker.SlotIndex != loadoutSlotIndex) continue;

            BallPulse pulse = handBalls[i].GetComponent<BallPulse>();
            if (pulse == null) pulse = handBalls[i].AddComponent<BallPulse>();
            
            pulse.SetHoverMultiplier(scaleUp ? ballHoverScaleFactor : 1f);
            return;
        }
    }

    private static void RestoreBallDefaultOutline(GameObject ball)
    {
        Outline outline = ball.GetComponentInChildren<Outline>();
        if (outline != null)
        {
            outline.OutlineMode = Outline.Mode.OutlineAll;
            outline.OutlineColor = Color.black;
            outline.OutlineWidth = 6f;
            outline.enabled = true;
        }
    }

    private void RestoreHandBallDefaultOutline(int loadoutSlotIndex)
    {
        if (_ballSpawner == null) return;
        var handBalls = _ballSpawner.HandBalls;
        for (int i = 0; i < handBalls.Count; i++)
        {
            if (handBalls[i] == null) continue;
            var marker = handBalls[i].GetComponent<BallHandSlotMarker>();
            if (marker != null && marker.SlotIndex == loadoutSlotIndex)
            {
                RestoreBallDefaultOutline(handBalls[i]);
                return;
            }
        }
    }
}
