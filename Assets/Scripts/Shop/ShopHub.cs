using UnityEngine;

/// <summary>
/// Marker + sell logic for a shop "hub" object. When a hand ball is dragged
/// onto an object carrying this component during the shop screen, the ball is
/// sold for a fraction of its price and removed from the loadout.
/// Requires a Collider on this object (or a child) for raycast hit-testing.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopHub : MonoBehaviour
{
    [Tooltip("Fraction of the ball's price refunded when sold (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float sellFraction = 0.5f;

    [Tooltip("If true, the player cannot sell their last remaining ball.")]
    [SerializeField] private bool preventSellingLastBall = true;

    [Header("Tooltip")]
    [SerializeField] private string displayName = "Shop Hub";
    [TextArea]
    [SerializeField] private string description = "Sell your pinballs here";

    [Header("Hover Outline")]
    [SerializeField] private Color hoverOutlineColor = Color.white;
    [SerializeField] private float hoverOutlineWidth = 5f;

    private Outline _outline;

    public string DisplayName => displayName;
    public string Description => description;

    private void Awake()
    {
        EnsureOutline();
        SetHovered(false);
    }

    private void EnsureOutline()
    {
        if (_outline != null) return;

        _outline = GetComponentInChildren<Outline>(true);
        if (_outline == null)
        {
            Renderer rend = GetComponentInChildren<Renderer>(true);
            if (rend != null)
            {
                _outline = rend.gameObject.AddComponent<Outline>();
            }
        }

        if (_outline != null)
        {
            _outline.OutlineMode = Outline.Mode.OutlineAll;
            _outline.OutlineColor = hoverOutlineColor;
            _outline.OutlineWidth = hoverOutlineWidth;
        }
    }

    public void SetHovered(bool hovered)
    {
        EnsureOutline();
        if (_outline == null) return;

        _outline.OutlineColor = hoverOutlineColor;
        _outline.OutlineWidth = hoverOutlineWidth;
        _outline.enabled = hovered;
    }

    public int GetSellPrice(BallDefinition def)
    {
        if (def == null) return 0;
        return Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0, def.Price) * sellFraction));
    }

    /// <summary>
    /// Attempts to sell the hand ball at the given loadout slot. Returns true
    /// on success. Only works while the shop is active (the caller -- the
    /// UnifiedShopController -- already enforces this via its state machine).
    /// </summary>
    public bool TrySellBall(int slotIndex, out BallDefinition sold, out int refund, out string failReason)
    {
        sold = null;
        refund = 0;
        failReason = null;

        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null)
        {
            failReason = "No loadout available.";
            return false;
        }

        if (preventSellingLastBall && loadoutCtrl.BallLoadoutCount <= 1)
        {
            failReason = "Can't sell your last ball.";
            return false;
        }

        if (slotIndex < 0 || slotIndex >= loadoutCtrl.BallLoadoutCount)
        {
            failReason = "Invalid slot.";
            return false;
        }

        if (!loadoutCtrl.TryRemoveBallFromLoadoutAt(slotIndex, out BallDefinition removed) || removed == null)
        {
            failReason = "Could not remove ball.";
            return false;
        }

        sold = removed;
        refund = GetSellPrice(removed);
        return true;
    }
}
