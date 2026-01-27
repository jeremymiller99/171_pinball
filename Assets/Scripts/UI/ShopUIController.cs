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
    [SerializeField] private RunFlowController runFlowController;
    [SerializeField] private ShopTransitionController shopTransitionController;
    [Tooltip("Optional: root object for the shop canvas. If omitted, this component's GameObject will be toggled.")]
    [SerializeField] private GameObject shopCanvasRoot;
    [Tooltip("Optional: tab controller used to switch between Balls and Board Components screens.")]
    [SerializeField] private ShopTabsController tabsController;

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
    private bool _closeRequested;

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

        if (runFlowController == null)
        {
#if UNITY_2022_2_OR_NEWER
            runFlowController = FindFirstObjectByType<RunFlowController>();
#else
            runFlowController = FindObjectOfType<RunFlowController>();
#endif
        }

        if (shopTransitionController == null)
        {
#if UNITY_2022_2_OR_NEWER
            shopTransitionController = FindFirstObjectByType<ShopTransitionController>();
#else
            shopTransitionController = FindObjectOfType<ShopTransitionController>();
#endif
        }

        if (shopCanvasRoot == null)
        {
            shopCanvasRoot = gameObject;
        }

        if (tabsController == null && shopCanvasRoot != null)
        {
            tabsController = shopCanvasRoot.GetComponentInChildren<ShopTabsController>(includeInactive: true);
        }
    }

    private void OnEnable()
    {
        _closeRequested = false;

        // Canvas enabled by GameRulesManager; treat that as "open".
        HookTabs();
        if (tabsController != null)
        {
            // Default to balls screen every time the shop is opened.
            tabsController.SetTab(ShopTabsController.Tab.Balls, notify: false);
        }

        SetReplacePanelOpen(false);
        _pendingItem = null;
        RebuildReplaceSlots();
        RebuildOffers();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnhookTabs();
    }

    public void Open()
    {
        if (shopCanvasRoot != null)
        {
            shopCanvasRoot.SetActive(true);
        }

        if (tabsController != null)
        {
            // If another system opened us without OnEnable (rare), ensure we start on Balls.
            tabsController.SetTab(ShopTabsController.Tab.Balls, notify: false);
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
        if (_closeRequested)
            return;
        _closeRequested = true;

        SetReplacePanelOpen(false);
        _pendingItem = null;

        // Prefer animated close transition, then hand off to RunFlowController.
        if (shopTransitionController != null)
        {
            shopTransitionController.CloseShopThen(() =>
            {
                // Let the RunFlowController drive the transition (it may swap boards before starting the next round).
                if (runFlowController != null)
                {
                    runFlowController.ContinueAfterShop();
                    return;
                }

                // Fallback: old behavior.
                if (rulesManager != null)
                {
                    rulesManager.OnShopClosed();
                    return;
                }

                // Fallback: just hide the canvas.
                if (shopCanvasRoot != null)
                {
                    shopCanvasRoot.SetActive(false);
                }
            });
            return;
        }

        // No transition controller: immediate continue as before.
        // Let the RunFlowController drive the transition (it may swap boards before starting the next round).
        if (runFlowController != null)
        {
            runFlowController.ContinueAfterShop();
            return;
        }

        // Fallback: old behavior.
        if (rulesManager != null)
        {
            rulesManager.OnShopClosed();
            return;
        }

        // Fallback: just hide the canvas.
        if (shopCanvasRoot != null)
        {
            shopCanvasRoot.SetActive(false);
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
        FMODUnity.RuntimeManager.PlayOneShot("event:/button_click");
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
        FMODUnity.RuntimeManager.PlayOneShot("event:/button_click");
        _pendingItem = null;
        SetReplacePanelOpen(false);
        RebuildReplaceSlots();
        RebuildOffers();
        RefreshUI();
    }

    public void CancelPendingPurchase()
    {
        _pendingItem = null;
        FMODUnity.RuntimeManager.PlayOneShot("event:/button_click");
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

    private void HookTabs()
    {
        if (tabsController == null)
            return;

        tabsController.TabChanged -= OnTabChanged;
        tabsController.TabChanged += OnTabChanged;
    }

    private void UnhookTabs()
    {
        if (tabsController == null)
            return;

        tabsController.TabChanged -= OnTabChanged;
    }

    private void OnTabChanged(ShopTabsController.Tab tab)
    {
        // If the player is in the middle of a "choose slot to replace" flow, leaving the Balls tab
        // should safely abort it (otherwise they'd come back to a stale pending purchase).
        if (tab != ShopTabsController.Tab.Balls)
        {
            ClearPendingReplaceFlow();
        }
        else
        {
            // Coming back to Balls: ensure coin display is current.
            RefreshUI();
        }
    }

    private void ClearPendingReplaceFlow()
    {
        _pendingItem = null;
        SetReplacePanelOpen(false);
        SetPrompt(string.Empty);
        RefreshUI();
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

