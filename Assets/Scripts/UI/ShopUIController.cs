using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Shop overlay UI.
/// - Open/Close the shop canvas root
/// - Handles buying a new ball prefab.
///   If the player's "hand" (loadout) is full, they must replace an existing slot.
/// </summary>
public sealed class ShopUIController : MonoBehaviour
{
    [Serializable]
    public class BallShopItem
    {
        public string displayName = "Ball";
        public GameObject ballPrefab;
        public int cost = 10;
    }

    [Header("Refs")]
    [SerializeField] private GameRulesManager rulesManager;
    [Tooltip("Optional: root object for the shop canvas. If omitted, this component's GameObject will be toggled.")]
    [SerializeField] private GameObject shopCanvasRoot;

    [Header("UI (optional)")]
    [Tooltip("Shown when a replacement slot must be chosen.")]
    [SerializeField] private GameObject replacePanelRoot;
    [Tooltip("Parent transform that will receive one instantiated entry per ball in hand.")]
    [SerializeField] private Transform replaceSlotsContainer;
    [Tooltip("Prefab with BallReplaceSlotEntryUI on it (Button + TMP + optional Image).")]
    [SerializeField] private BallReplaceSlotEntryUI replaceSlotEntryPrefab;

    [Header("Shop offers (dynamic)")]
    [Tooltip("Parent transform that will receive 3 instantiated offer entries each shop visit.")]
    [SerializeField] private Transform offersContainer;
    [Tooltip("Prefab with ShopBallOfferEntryUI on it.")]
    [SerializeField] private ShopBallOfferEntryUI offerEntryPrefab;
    [SerializeField] private int offersPerShop = 3;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text promptText;

    [Header("Catalog (all possible balls)")]
    [SerializeField] private List<BallShopItem> allBallItems = new List<BallShopItem>();

    private BallShopItem _pendingItem;
    private readonly List<BallShopItem> _currentOffers = new List<BallShopItem>(capacity: 3);

    private void Awake()
    {
        if (rulesManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            rulesManager = FindFirstObjectByType<GameRulesManager>();
#else
            rulesManager = FindObjectOfType<GameRulesManager>();
#endif
        }

        if (shopCanvasRoot == null)
        {
            shopCanvasRoot = gameObject;
        }
    }

    private void OnEnable()
    {
        // Canvas enabled by GameRulesManager; treat that as "open".
        SetReplacePanelOpen(false);
        _pendingItem = null;
        RebuildReplaceSlots();
        RebuildOffers();
        RefreshUI();
    }

    public void Open()
    {
        if (shopCanvasRoot != null)
        {
            shopCanvasRoot.SetActive(true);
        }
        RebuildReplaceSlots();
        RebuildOffers();
        RefreshUI();
    }

    /// <summary>
    /// Close the shop and continue to the next round.
    /// Hook this to your "Close" / "Continue" button.
    /// </summary>
    public void CloseAndContinue()
    {
        SetReplacePanelOpen(false);
        _pendingItem = null;
        ClearReplaceSlots();
        ClearOffers();

        // Let the rules manager drive the round transition.
        if (rulesManager != null)
        {
            rulesManager.OnShopClosed();
        }
        else
        {
            // Fallback: just hide the canvas.
            if (shopCanvasRoot != null)
            {
                shopCanvasRoot.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Begins the "buy ball" flow for an offer item at index (0..offersPerShop-1).
    /// If the hand/loadout is full, shows the replacement panel and waits for ChooseReplaceSlot().
    /// Hook this to each dynamic offer entry button.
    /// </summary>
    public void BuyOfferByIndex(int offerIndex)
    {
        if (rulesManager == null)
        {
            return;
        }

        if (offerIndex < 0 || offerIndex >= _currentOffers.Count)
        {
            return;
        }

        var item = _currentOffers[offerIndex];
        if (item == null || item.ballPrefab == null)
        {
            return;
        }

        // Check money first (do NOT spend until we know whether we're replacing or adding).
        if (rulesManager.Coins < item.cost)
        {
            SetPrompt($"Not enough coins for {GetItemLabel(item)}.");
            RefreshUI();
            return;
        }

        int currentCount = rulesManager.BallLoadoutCount;
        int cap = Mathf.Max(1, rulesManager.MaxBalls);

        // If we have an open slot, buy immediately by adding to inventory.
        if (currentCount < cap)
        {
            if (!rulesManager.TrySpendCoins(item.cost))
            {
                SetPrompt($"Not enough coins for {GetItemLabel(item)}.");
                RefreshUI();
                return;
            }

            bool added = rulesManager.AddBallToLoadout(item.ballPrefab);
            if (!added)
            {
                // Shouldn't happen if currentCount < cap, but keep safe.
                SetPrompt($"Cannot add {GetItemLabel(item)} (hand is full).");
                RefreshUI();
                return;
            }

            SetPrompt($"Purchased {GetItemLabel(item)}.");
            SetReplacePanelOpen(false);
            _pendingItem = null;
            RebuildReplaceSlots();
            RebuildOffers();
            RefreshUI();
            return;
        }

        // Otherwise, must replace an existing slot.
        _pendingItem = item;
        RebuildReplaceSlots();
        SetReplacePanelOpen(true);
        SetPrompt($"Choose a ball to replace with {GetItemLabel(item)} (cost {item.cost}).");
        RefreshUI();
    }

    /// <summary>
    /// Confirms which existing ball slot gets replaced.
    /// Hook this up to your replacement-slot buttons (0..handSize-1).
    /// </summary>
    public void ChooseReplaceSlot(int slotIndex)
    {
        if (_pendingItem == null || rulesManager == null)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= rulesManager.BallLoadoutCount)
        {
            return;
        }

        // Spend only at confirm-time.
        if (!rulesManager.TrySpendCoins(_pendingItem.cost))
        {
            SetPrompt($"Not enough coins for {GetItemLabel(_pendingItem)}.");
            RefreshUI();
            return;
        }

        rulesManager.ReplaceBallInLoadout(slotIndex, _pendingItem.ballPrefab);

        SetPrompt($"Purchased {GetItemLabel(_pendingItem)}.");
        _pendingItem = null;
        SetReplacePanelOpen(false);
        RebuildReplaceSlots();
        RebuildOffers();
        RefreshUI();
    }

    public void CancelPendingPurchase()
    {
        _pendingItem = null;
        SetReplacePanelOpen(false);
        SetPrompt(string.Empty);
        RebuildReplaceSlots();
        RebuildOffers();
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (coinsText != null && rulesManager != null)
        {
            coinsText.text = rulesManager.Coins.ToString();
        }
    }

    private void RebuildReplaceSlots()
    {
        if (replaceSlotsContainer == null || replaceSlotEntryPrefab == null || rulesManager == null)
        {
            return;
        }

        ClearReplaceSlots();

        List<GameObject> loadout = rulesManager.GetBallLoadoutSnapshot();
        for (int i = 0; i < loadout.Count; i++)
        {
            GameObject prefab = loadout[i];
            string ballName = prefab != null ? prefab.name : "(None)";
            Sprite icon = TryGetSpriteIcon(prefab);

            BallReplaceSlotEntryUI entry = Instantiate(replaceSlotEntryPrefab, replaceSlotsContainer);
            entry.Init(this, i, $"Slot {i + 1}: {ballName}", icon);
        }
    }

    private void ClearReplaceSlots()
    {
        if (replaceSlotsContainer == null)
        {
            return;
        }

        for (int i = replaceSlotsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(replaceSlotsContainer.GetChild(i).gameObject);
        }
    }

    private void RebuildOffers()
    {
        if (offersContainer == null || offerEntryPrefab == null)
        {
            return;
        }

        ClearOffers();
        RollNewOffers();

        for (int i = 0; i < _currentOffers.Count; i++)
        {
            BallShopItem item = _currentOffers[i];
            if (item == null || item.ballPrefab == null) continue;

            Sprite icon = TryGetSpriteIcon(item.ballPrefab);
            ShopBallOfferEntryUI entry = Instantiate(offerEntryPrefab, offersContainer);
            entry.Init(this, i, GetItemLabel(item), item.cost, icon);
        }
    }

    private void ClearOffers()
    {
        _currentOffers.Clear();

        if (offersContainer == null) return;
        for (int i = offersContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(offersContainer.GetChild(i).gameObject);
        }
    }

    private void RollNewOffers()
    {
        _currentOffers.Clear();

        if (allBallItems == null || allBallItems.Count == 0)
        {
            return;
        }

        int target = Mathf.Max(0, offersPerShop);
        if (target == 0) return;

        // Build list of valid indices (non-null item + prefab).
        List<int> valid = new List<int>(allBallItems.Count);
        for (int i = 0; i < allBallItems.Count; i++)
        {
            var it = allBallItems[i];
            if (it != null && it.ballPrefab != null)
            {
                valid.Add(i);
            }
        }

        if (valid.Count == 0)
        {
            return;
        }

        // Partial shuffle to pick up to target unique items.
        int picks = Mathf.Min(target, valid.Count);
        for (int i = 0; i < picks; i++)
        {
            int j = UnityEngine.Random.Range(i, valid.Count);
            (valid[i], valid[j]) = (valid[j], valid[i]);
            _currentOffers.Add(allBallItems[valid[i]]);
        }
    }

    private static Sprite TryGetSpriteIcon(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        // Common case if your balls are 2D sprites.
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
        if (sr != null)
        {
            return sr.sprite;
        }

        return null;
    }

    private void SetReplacePanelOpen(bool open)
    {
        if (replacePanelRoot != null)
        {
            replacePanelRoot.SetActive(open);
        }
    }

    private void SetPrompt(string msg)
    {
        if (promptText != null)
        {
            promptText.text = msg ?? string.Empty;
        }
    }

    private static string GetItemLabel(BallShopItem item)
    {
        if (item == null)
        {
            return "Ball";
        }
        return string.IsNullOrWhiteSpace(item.displayName) ? "Ball" : item.displayName;
    }
}

