// Updated with Cursor (Composer) by assistant on 2026-03-31 (CoinController for economy; BallLoadoutController direct).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(ShopOfferShelfController))]
[RequireComponent(typeof(ShopHandInteractionController))]
[RequireComponent(typeof(ShopComponentPlacementController))]
public sealed class UnifiedShopController : MonoBehaviour
{
    public enum ShopState
    {
        Browsing,
        PlacingComponent,
        PlacingBall,
        SelectingBall
    }

    [Header("Refs")]
    [SerializeField] private GameRulesManager rulesManager;
    [SerializeField] private CoinController coinController;
    [SerializeField] private RunFlowController runFlowController;
    [SerializeField] private ShopTransitionController shopTransitionController;
    [SerializeField] private GameObject shopCanvasRoot;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private RenderTextureRaycaster renderTextureRaycaster;
    [SerializeField] private UIScript uiScript;
    [SerializeField] private ShopHub shopHub;

    [Header("UI")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button rerollButton;
    [SerializeField] private int rerollCost = 2;
    [SerializeField] private float movementThreshold;
    [SerializeField] private GameObject currentOfferObject;
    [SerializeField] private int currentOfferIndex;
    [SerializeField] private int currentComponentIndex;
    [SerializeField] private int currentBallIndex;
    [SerializeField] private bool keepMoving;
    [SerializeField] private int selectedBallIndex;

    [Header("Confirm Panel")]
    [SerializeField] private ShopConfirmPanel confirmPanel;

    public ShopState CurrentState { get; private set; } = ShopState.Browsing;

    public bool IsShopActive => isActiveAndEnabled
        && shopCanvasRoot != null && shopCanvasRoot.activeInHierarchy;

    public bool SelectingButtons;

    public event Action<ShopOffer> OfferSelected;
    public event Action PlacementCancelled;
    public event Action ShopClosed;

    // Sub-controllers
    private ShopOfferShelfController _shelf;
    private ShopHandInteractionController _hand;
    private ShopComponentPlacementController _placement;

    private ShopOffer _selectedOffer;
    private int _selectedOfferIndex = -1;
    private bool _closeRequested;

    private BoardComponent _targetComponent;
    private int _targetComponentIndex;
    private bool _dragBallHasRoom;
    private bool _isDragPreviewActive;

    private ShopShipController _spaceship;

    // Board-side world-space Reroll/Done buttons (additive board scene).
    // Resolved at shop-open time via ServiceLocator since they cannot be
    // serialized across scenes.
    private ShopRerollPanel _rerollPanel;

    private void Awake()
    {
        ServiceLocator.Register<UnifiedShopController>(this);

        _shelf = GetComponent<ShopOfferShelfController>();
        _hand = GetComponent<ShopHandInteractionController>();
        _placement = GetComponent<ShopComponentPlacementController>();

        ResolveReferences();
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<UnifiedShopController>();
    }

    private void OnEnable()
    {
        Debug.Log("[UnifiedShopController] OnEnable triggered. Resetting state.");
        _closeRequested = false;
        CurrentState = ShopState.Browsing;
        SelectingButtons = true;

        ResolveReferences();

        if (ballSpawner != null)
        {
            var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
            if (loadoutCtrl != null && loadoutCtrl.BallLoadoutCount > 0)
            {
                ballSpawner.BuildHandFromPrefabs(loadoutCtrl.GetBallLoadoutPrefabSnapshot());
            }
        }

        _shelf.Initialize();
        _hand.Initialize();
        _placement.Initialize();

        WireContinueButton();
        WireRerollButton();
        WireExternalRerollPanel();
        HookTransitionEvents();

        if (confirmPanel != null) confirmPanel.Hide();

        RefreshUI();

        _spaceship = ServiceLocator.Get<ShopShipController>();
        if (_spaceship == null) _spaceship = FindFirstObjectByType<ShopShipController>();

        if (_spaceship != null)
        {
            _spaceship.SpaceshipParked += OnSpaceshipParked;

            // If the ship has already parked (e.g., second shop visit, or the
            // transition fired before our OnEnable), build offers now instead
            // of waiting for a parked event that won't come.
            if (_spaceship.IsParked)
            {
                _shelf.RebuildOffers();
            }
        }
        else
        {
            _shelf.RebuildOffers();
        }
    }

    private void OnDisable()
    {
        if (_spaceship != null)
        {
            _spaceship.SpaceshipParked -= OnSpaceshipParked;
            _spaceship = null;
        }

        UnhookTransitionEvents();
        UnwireExternalRerollPanel();

        _shelf.Cleanup();
        _hand.Cleanup();
        _placement.Cleanup();

        _dragBallHasRoom = false;
    }

    private void OnSpaceshipParked()
    {
        _shelf.RebuildOffers();
    }

    // Localized lowercase words for enums embedded in shop prompts (fallback = English enum name).
    private static string TypeWord(BoardComponentType type) =>
        LocalizedUI.Get($"gameplay.componentType.{type}", type.ToString().ToLower());

    private static string RarityWord(BallRarity rarity) =>
        LocalizedUI.Get($"gameplay.rarity.{rarity}", rarity.ToString());

    #region Click API
    public void ConfirmComponentPlacement()
    {
        if (_selectedOffer == null || _targetComponent == null) return;

        BoardComponentDefinition def = _selectedOffer.ComponentDef;
        int price = _selectedOffer.Price;

        if (!_placement.ReplaceComponent(_targetComponent, def))
        {
            coinController?.AddCoinsUnscaled(price);
            SetPrompt(LocalizedUI.Format("gameplay.shop.couldNotPlace", "Could not place {0}. Coins refunded.", def.GetSafeDisplayName()));
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            ExitPlacementMode();
            RefreshUI();
            return;
        }

        PinballAnalytics.LogShopItemPurchased(_selectedOffer);

        _shelf.ConsumeOffer(_selectedOfferIndex);
        ExitPlacementMode();

        SetPrompt(LocalizedUI.Format("gameplay.shop.placed", "Placed {0}.", def.GetSafeDisplayName()));
        RefreshUI();
    }

    public void RerollOffers()
    {
        if (coinController == null || coinController.Coins < rerollCost)
        {
            SetPrompt(LocalizedUI.Format("gameplay.shop.notEnoughReroll", "Not enough coins to reroll (${0}).", rerollCost));
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            RefreshUI();
            return;
        }

        if (!coinController.TrySpendCoins(rerollCost)) return;

        SetPrompt(LocalizedUI.Get("gameplay.shop.rerolling", "Rerolling..."));
        ServiceLocator.Get<AudioManager>()?.PlayReroll();
        RefreshUI();

        _shelf.ClearOfferDisplays();

        if (_spaceship != null)
        {
            _spaceship.FlyOut(() => {
                _spaceship.FlyIn();
            });
        }
        else
        {
            _shelf.RebuildOffers();
        }
    }

    public void CloseAndContinue()
    {
        Debug.Log($"[UnifiedShopController] CloseAndContinue called. closeRequested={_closeRequested}, CurrentState={CurrentState}");
        if (_closeRequested) return;
        _closeRequested = true;

        if (confirmPanel != null) confirmPanel.Hide();

        if (CurrentState == ShopState.PlacingComponent && _selectedOffer != null)
        {
            coinController?.AddCoinsUnscaled(_selectedOffer.Price);
            Debug.Log($"[UnifiedShopController] Refunded in-flight placement for {_selectedOffer.DisplayName} on shop close.");
        }

        _shelf.ClearOfferDisplays();

        ExitPlacementMode();
        ShopClosed?.Invoke();

        if (runFlowController != null)
        {
            Debug.Log("[UnifiedShopController] Handing over Continue to RunFlowController");
            runFlowController.ContinueAfterShop();
            return;
        }

        if (shopTransitionController != null)
        {
            Debug.Log("[UnifiedShopController] Using ShopTransitionController to close");
            shopTransitionController.CloseShopThen(() => rulesManager?.OnShopClosed());
            return;
        }

        Debug.Log("[UnifiedShopController] Closing via GameRulesManager immediately");
        rulesManager?.OnShopClosed();

        if (shopCanvasRoot != null) shopCanvasRoot.SetActive(false);
    }

    #endregion

    #region Drag API

    public void TryDropOfferAfterDrag(int offerIndex, GameObject hitObject, Ray worldRay)
    {
        if (CurrentState != ShopState.Browsing) return;

        if (_spaceship != null && !_spaceship.IsParked)
        {
            SetPrompt(LocalizedUI.Get("gameplay.shop.merchantArriving", "The merchant is still arriving..."));
            return;
        }

        ShopOffer offer = _shelf.GetOffer(offerIndex);
        if (offer == null || !offer.IsValid) return;

        if (offer.Type == ShopOffer.OfferType.BoardComponent)
        {
            BoardComponent bc = hitObject != null ? hitObject.GetComponentInParent<BoardComponent>() : null;
            if (bc == null)
            {
                SetPrompt(LocalizedUI.Get("gameplay.shop.dropOnComponent", "Drop onto a bumper, target, or flipper on the board."));
                return;
            }

            if (!ShopComponentPlacementController.IsValidPlacementTarget(offer, bc))
            {
                SetPrompt(LocalizedUI.Format("gameplay.shop.dropOnMatchingType", "Drop onto a {0} on the board.", TypeWord(offer.ComponentDef.ComponentType)));
                return;
            }

            ShowDragDropBoardPurchaseConfirm(offerIndex, bc);
            return;
        }

        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null) return;

        int dropSlot = ResolveDropSlotIndex(hitObject, worldRay);
        if (dropSlot < 0)
        {
            SetPrompt(LocalizedUI.Get("gameplay.shop.dropToBuyBall", "Drop onto a hand slot to buy the ball."));
            return;
        }

        int currentCount = loadoutCtrl.BallLoadoutCount;
        int cap = Mathf.Max(1, loadoutCtrl.MaxBalls);

        if (currentCount < cap)
        {
            // Partial inventory: drop onto any slot inserts at that position
            // (or appends at end if the slot is past the filled range).
            int insertIdx = Mathf.Clamp(dropSlot, 0, currentCount);
            AutoBuyBallOffer(offerIndex, offer, insertIdx);
            return;
        }

        if (dropSlot >= loadoutCtrl.BallLoadoutCount) return;

        ShowDragDropBallReplaceConfirm(offerIndex, dropSlot);
    }

    private int ResolveDropSlotIndex(GameObject hitObject, Ray worldRay)
    {
        if (hitObject != null)
        {
            var slot = hitObject.GetComponentInParent<BallHandSlot>();
            if (slot != null && slot.SlotIndex >= 0) return slot.SlotIndex;

            var marker = hitObject.GetComponentInParent<BallHandSlotMarker>();
            if (marker != null && marker.SlotIndex >= 0) return marker.SlotIndex;
        }
        if (_hand.TryGetHandBallSlotFromRay(worldRay, out int slotIdx)) return slotIdx;
        return -1;
    }

    public void OnOfferDragStarted(ShopOffer offer)
    {
        if (offer == null || CurrentState != ShopState.Browsing) return;

        // If the merchant ship is mid-flight, the shelf may be cleared or
        // about to be rerolled — reject drags until it parks.
        if (_spaceship != null && !_spaceship.IsParked)
        {
            SetPrompt(LocalizedUI.Get("gameplay.shop.merchantArriving", "The merchant is still arriving..."));
            return;
        }

        _isDragPreviewActive = true;
        _hand.ClearSwapSelection();

        if (offer.Type == ShopOffer.OfferType.BoardComponent)
        {
            _placement.SetSelectionStateForPlacement(offer.ComponentDef.ComponentType);
        }
        else
        {
            var lc = ServiceLocator.Get<BallLoadoutController>();
            int currentCount = lc != null ? lc.BallLoadoutCount : 0;
            int cap = lc != null ? Mathf.Max(1, lc.MaxBalls) : 1;
            _dragBallHasRoom = currentCount < cap;

            if (_dragBallHasRoom && ballSpawner != null)
            {
                // Start with gap at the end, but OnOfferDragHover will update it
                ballSpawner.PreviewInsertGap(ballSpawner.HandCount);
            }
            else
            {
                _hand.HighlightAllHandBallsWaitColor();
            }
        }
    }

    public void OnOfferDragHover(ShopOffer offer, GameObject hitObject, Ray worldRay)
    {
        if (!_isDragPreviewActive || offer == null) return;

        if (offer.Type == ShopOffer.OfferType.BoardComponent)
        {
            BoardComponent bc = hitObject != null ? hitObject.GetComponentInParent<BoardComponent>() : null;
            _placement.UpdateDragHover(offer, bc);
        }
        else
        {
            int slot = ResolveDropSlotIndex(hitObject, worldRay);

            if (_dragBallHasRoom)
            {
                if (ballSpawner != null)
                {
                    // If we are hovering a specific slot, preview gap there. 
                    // Otherwise default to the end.
                    int gapIdx = (slot >= 0) ? slot : ballSpawner.HandCount;
                    ballSpawner.PreviewInsertGap(gapIdx);
                }
            }
            else
            {
                _hand.UpdateDragHover(slot);
            }
        }
    }

    public void OnOfferDragEnded()
    {
        if (!_isDragPreviewActive) return;

        _isDragPreviewActive = false;
        _placement.EndDragHover();
        _hand.EndDragHover();
        
        if (ballSpawner != null) ballSpawner.ClearInsertGapPreview();
        _dragBallHasRoom = false;
    }

    public void OnPlacementHover(GameObject hitObject, Ray worldRay)
    {
        if (CurrentState == ShopState.PlacingComponent)
        {
            BoardComponent bc = hitObject != null ? hitObject.GetComponentInParent<BoardComponent>() : null;
            _placement.UpdatePlacementHover(_selectedOffer, bc);
        }
    }

    public void OnHandBallDragStarted(int dragSlot)
    {
        if (CurrentState != ShopState.Browsing) return;
        ResolveReferences();
        _hand.ClearSwapSelection();
        _isDragPreviewActive = true;
        _hand.HighlightDropSources(dragSlot);
    }

    public void OnHandBallDragHover(int hoveredSlot)
    {
        if (!_isDragPreviewActive) return;
        _hand.UpdateDragHover(hoveredSlot);
    }

    public void OnHandBallDragEnded()
    {
        if (!_isDragPreviewActive) return;
        _isDragPreviewActive = false;
        _hand.EndDragHover();
    }

    public void OnHandBallDragSell(int slot, ShopHub hub)
    {
        if (!IsShopActive) return;
        if (!(CurrentState == ShopState.Browsing || CurrentState == ShopState.SelectingBall)) return;
        if (hub == null || slot < 0) return;

        _hand.ClearSwapSelection();

        if (!hub.TrySellBall(slot, out BallDefinition sold, out int refund, out string failReason))
        {
            SetPrompt(string.IsNullOrEmpty(failReason) ? LocalizedUI.Get("gameplay.shop.couldNotSell", "Could not sell ball.") : failReason);
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            return;
        }

        if (refund > 0) coinController?.AddCoinsUnscaled(refund);

        if (ballSpawner != null)
        {
            var lc = ServiceLocator.Get<BallLoadoutController>();
            if (lc != null)
            {
                ballSpawner.BuildHandFromPrefabs(lc.GetBallLoadoutPrefabSnapshot());
            }
        }

        ServiceLocator.Get<AudioManager>()?.PlayPurchase();
        SetPrompt(LocalizedUI.Format("gameplay.shop.sold", "Sold {0} for ${1}.", sold.GetSafeDisplayName(), refund));
        RefreshUI();
    }

    public void OnHandBallDragSwap(int fromSlot, int toSlot)
    {
        if (CurrentState != ShopState.Browsing) return;

        _hand.ClearSwapSelection();
        var lc = ServiceLocator.Get<BallLoadoutController>();
        if (fromSlot < 0 || toSlot < 0 || fromSlot == toSlot || lc == null) return;

        bool moved = lc.MoveBallInLoadout(fromSlot, toSlot);
        if (!moved)
        {
            SetPrompt(LocalizedUI.Get("gameplay.shop.couldNotMove", "Could not move that ball."));
            return;
        }

        if (ballSpawner != null) ballSpawner.MoveHandBallAnimated(fromSlot, toSlot);
        
        ServiceLocator.Get<AudioManager>()?.PlaySwapSlot();
        SetPrompt(LocalizedUI.Get("gameplay.shop.ballMoved", "Ball moved."));
    }

    #endregion

    #region Internal Logic

    private void HandleBallSwapClick(int slotIndex)
    {
        if (slotIndex < 0) return;

        int selected = _hand.GetSwapSelectedSlot();
        if (selected < 0)
        {
            _hand.SetSwapSelectedSlot(slotIndex);
            SetPrompt(LocalizedUI.Get("gameplay.shop.moveHint", "Click another slot to move the ball there, or the same ball to cancel."));
            return;
        }

        if (selected == slotIndex)
        {
            _hand.ClearSwapSelection();
            SetPrompt("");
            return;
        }

        int slotFrom = selected;
        int slotTo = slotIndex;
        _hand.ClearSwapSelection();

        var lc = ServiceLocator.Get<BallLoadoutController>();
        if (lc == null) return;

        bool moved = lc.MoveBallInLoadout(slotFrom, slotTo);
        if (!moved)
        {
            SetPrompt(LocalizedUI.Get("gameplay.shop.couldNotMove", "Could not move that ball."));
            return;
        }

        if (ballSpawner != null) ballSpawner.MoveHandBallAnimated(slotFrom, slotTo);

        ServiceLocator.Get<AudioManager>()?.PlaySwapSlot();
        SetPrompt(LocalizedUI.Get("gameplay.shop.ballMoved", "Ball moved."));
    }

    private void EnterComponentPlacementMode()
    {
        CurrentState = ShopState.PlacingComponent;
        _placement.SetSelectionStateForPlacement(_selectedOffer.ComponentDef.ComponentType);
        SetPrompt(LocalizedUI.Format("gameplay.shop.clickToReplace", "Click a {0} on the board to replace with {1}.", TypeWord(_selectedOffer.ComponentDef.ComponentType), _selectedOffer.DisplayName));
        RefreshUI();
    }

    private void ExitPlacementMode()
    {
        _placement.DeselectAll();
        _hand.UnhighlightAllHandBalls();
        _hand.ClearSwapSelection();
        _hand.ClearPlacementHover();

        if (ballSpawner != null) ballSpawner.ClearInsertGapPreview();

        CurrentState = ShopState.Browsing;
        _selectedOffer = null;
        _selectedOfferIndex = -1;
        _targetComponent = null;

        if (confirmPanel != null) confirmPanel.Hide();

        PlacementCancelled?.Invoke();
    }

    private void AutoBuyBallOffer(int offerIndex, ShopOffer offer, int insertSlot)
    {
        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null || offer == null) return;

        insertSlot = Mathf.Clamp(insertSlot, 0, loadoutCtrl.BallLoadoutCount);

        if (coinController == null || !coinController.TrySpendCoins(offer.Price))
        {
            SetPrompt(LocalizedUI.Format("gameplay.shop.notEnoughCoinsFor", "Not enough coins for {0}.", offer.DisplayName));
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            RefreshUI();
            return;
        }

        BallDefinition ballToGrant = offer.BallDef;
        bool wasMystery = ballToGrant is MysteryBallDefinition;

        if (wasMystery)
        {
            var mystery = (MysteryBallDefinition)ballToGrant;
            ballToGrant = _shelf.ResolveMysteryBall(mystery.TargetRarity);

            if (ballToGrant == null)
            {
                coinController?.AddCoinsUnscaled(offer.Price);
                SetPrompt(LocalizedUI.Format("gameplay.shop.noRarityBalls", "No {0} balls available -- purchase refunded.", RarityWord(mystery.TargetRarity)));
                ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
                RefreshUI();
                return;
            }
        }

        ServiceLocator.Get<AudioManager>()?.PlayPurchase();

        if (!loadoutCtrl.InsertBallIntoLoadout(insertSlot, ballToGrant))
        {
            coinController?.AddCoinsUnscaled(offer.Price);
            SetPrompt(LocalizedUI.Get("gameplay.shop.loadoutFull", "Loadout full -- could not add ball."));
            RefreshUI();
            return;
        }

        if (ballSpawner != null && ballToGrant.Prefab != null)
        {
            ballSpawner.AddBallAnimated(ballToGrant.Prefab, insertSlot);
        }

        PinballAnalytics.LogShopItemPurchased(offer, ballToGrant, offer.Price);

        _shelf.ConsumeOffer(offerIndex);

        if (wasMystery)
        {
            SetPrompt(LocalizedUI.Format("gameplay.shop.mysteryRevealed", "Mystery revealed: {0}!", ballToGrant.GetSafeDisplayName()));
        }
        else
        {
            SetPrompt(LocalizedUI.Format("gameplay.shop.addedToLoadout", "Added {0} to loadout.", offer.DisplayName));
        }

        RefreshUI();
    }

    private Vector2 GetConfirmAnchorScreenPoint()
    {
        if (TooltipManager.TryGetTooltipScreenRectCenter(out Vector2 center))
        {
            TooltipManager.Hide();
            return center;
        }

        var mouse = Mouse.current;
        if (mouse != null) return mouse.position.ReadValue();
        return Vector2.zero;
    }

    private void ShowDragDropBoardPurchaseConfirm(int offerIndex, BoardComponent target)
    {
        ShopOffer offer = _shelf.GetOffer(offerIndex);
        if (offer == null || !offer.IsValid || target == null) return;

        if (confirmPanel == null)
        {
            ConfirmDragDropBoardPurchase(offerIndex, target);
            return;
        }

        var link = target.GetComponent<BoardComponentDefinitionLink>();
        string targetName = "this component";
        if (link != null && link.TryGetDefinition(out var def)) targetName = def.GetSafeDisplayName();

        confirmPanel.Show(
            $"Buy {offer.DisplayName} for ${offer.Price} and replace {targetName}?",
            offer.DisplayName, offer.Description, offer.Icon,
            () => ConfirmDragDropBoardPurchase(offerIndex, target),
            () => { if (confirmPanel != null) confirmPanel.Hide(); },
            GetConfirmAnchorScreenPoint());
    }

    private void ConfirmDragDropBoardPurchase(int offerIndex, BoardComponent target)
    {
        ShopOffer offer = _shelf.GetOffer(offerIndex);
        if (target == null || offer == null || !offer.IsValid) return;

        if (coinController == null || !coinController.TrySpendCoins(offer.Price))
        {
            SetPrompt(LocalizedUI.Format("gameplay.shop.notEnoughCoinsFor", "Not enough coins for {0}.", offer.DisplayName));
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            CancelDragDropBoard();
            return;
        }

        ServiceLocator.Get<AudioManager>()?.PlayPurchase();
        if (confirmPanel != null) confirmPanel.Hide();

        _selectedOffer = offer;
        _selectedOfferIndex = offerIndex;
        _targetComponent = target;
        ConfirmComponentPlacement();
    }

    public void CancelDragDropBoard()
    {
        if (confirmPanel != null) confirmPanel.Hide();
        RefreshUI();
    }

    private void ShowDragDropBallReplaceConfirm(int offerIndex, int slotIndex)
    {
        ShopOffer offer = _shelf.GetOffer(offerIndex);
        if (offer == null || !offer.IsValid) return;

        if (confirmPanel == null)
        {
            ConfirmDragDropBallReplace(offerIndex, slotIndex);
            return;
        }

        var lc = ServiceLocator.Get<BallLoadoutController>();
        var loadout = lc != null ? lc.GetBallLoadoutSnapshot() : new List<BallDefinition>();
        BallDefinition old = (slotIndex >= 0 && slotIndex < loadout.Count) ? loadout[slotIndex] : null;
        string oldName = old != null ? old.GetSafeDisplayName() : "(Empty)";
        int sellPrice = old != null ? (Mathf.Max(0, old.Price) + 1) / 2 : 0;

        string msg = old != null
            ? $"Buy {offer.DisplayName} for ${offer.Price} and replace {oldName}?\nSell {oldName} for ${sellPrice}."
            : $"Buy {offer.DisplayName} for ${offer.Price} and add to slot {slotIndex + 1}?";

        confirmPanel.Show(
            msg, offer.DisplayName, offer.Description, offer.Icon,
            () => ConfirmDragDropBallReplace(offerIndex, slotIndex),
            () => { if (confirmPanel != null) confirmPanel.Hide(); },
            GetConfirmAnchorScreenPoint());
    }

    private void ConfirmDragDropBallReplace(int offerIndex, int slotIndex)
    {
        ShopOffer offer = _shelf.GetOffer(offerIndex);
        if (offer == null || !offer.IsValid) return;

        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null) return;

        var loadout = loadoutCtrl.GetBallLoadoutSnapshot();
        if (slotIndex < 0 || slotIndex >= loadout.Count) return;

        if (coinController == null || !coinController.TrySpendCoins(offer.Price))
        {
            SetPrompt(LocalizedUI.Format("gameplay.shop.notEnoughCoinsFor", "Not enough coins for {0}.", offer.DisplayName));
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            if (confirmPanel != null) confirmPanel.Hide();
            RefreshUI();
            return;
        }

        ServiceLocator.Get<AudioManager>()?.PlayPurchase();
        if (confirmPanel != null) confirmPanel.Hide();

        BallDefinition newDef = offer.BallDef;
        BallDefinition oldBall = loadout[slotIndex];
        if (oldBall != null)
        {
            int sellPrice = (Mathf.Max(0, oldBall.Price) + 1) / 2;
            coinController?.AddCoinsUnscaled(sellPrice);
        }
        loadoutCtrl.ReplaceBallInLoadout(slotIndex, newDef);

        PinballAnalytics.LogShopItemPurchased(offer);

        _shelf.ConsumeOffer(offerIndex);

        if (ballSpawner != null && newDef != null && newDef.Prefab != null)
        {
            ballSpawner.ReplaceBallAnimated(slotIndex, newDef.Prefab);
        }

        SetPrompt(LocalizedUI.Format("gameplay.shop.replacedWith", "Replaced with {0}.", newDef.GetSafeDisplayName()));
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (coinsText != null && coinController != null) coinsText.text = $"${coinController.Coins}";
    }

    private void SetPrompt(string msg)
    {
        if (promptText != null) promptText.text = msg ?? string.Empty;
    }

    private void ResolveReferences()
    {
        if (_hand == null) _hand = GetComponent<ShopHandInteractionController>();
        if (rulesManager == null) rulesManager = ServiceLocator.Get<GameRulesManager>();
        if (coinController == null) coinController = ServiceLocator.Get<CoinController>();
        if (runFlowController == null) runFlowController = ServiceLocator.Get<RunFlowController>();
        if (shopTransitionController == null) shopTransitionController = ServiceLocator.Get<ShopTransitionController>();
        if (shopCanvasRoot == null) shopCanvasRoot = gameObject;
        if (ballSpawner == null) ballSpawner = ServiceLocator.Get<BallSpawner>();
        if (renderTextureRaycaster == null) renderTextureRaycaster = ServiceLocator.Get<RenderTextureRaycaster>();
        if (uiScript == null) uiScript = ServiceLocator.Get<UIScript>();
        if (shopHub == null) shopHub = ServiceLocator.Get<ShopHub>();
    }

    private void WireContinueButton()
    {
        if (continueButton == null) return;
        continueButton.onClick.RemoveListener(CloseAndContinue);
        continueButton.onClick.AddListener(CloseAndContinue);
    }

    private void WireRerollButton()
    {
        if (rerollButton == null) return;
        rerollButton.onClick.RemoveListener(RerollOffers);
        rerollButton.onClick.AddListener(RerollOffers);
    }

    private void WireExternalRerollPanel()
    {
        _rerollPanel = ServiceLocator.Get<ShopRerollPanel>();
        if (_rerollPanel == null) return;

        if (_rerollPanel.RerollButton != null)
        {
            _rerollPanel.RerollButton.onClick.RemoveListener(RerollOffers);
            _rerollPanel.RerollButton.onClick.AddListener(RerollOffers);
        }

        if (_rerollPanel.DoneButton != null)
        {
            _rerollPanel.DoneButton.onClick.RemoveListener(CloseAndContinue);
            _rerollPanel.DoneButton.onClick.AddListener(CloseAndContinue);
        }

        _rerollPanel.Show();
    }

    private void UnwireExternalRerollPanel()
    {
        if (_rerollPanel == null) return;

        if (_rerollPanel.RerollButton != null)
            _rerollPanel.RerollButton.onClick.RemoveListener(RerollOffers);
        if (_rerollPanel.DoneButton != null)
            _rerollPanel.DoneButton.onClick.RemoveListener(CloseAndContinue);

        _rerollPanel.Hide();
        _rerollPanel = null;
    }

    private void HookTransitionEvents()
    {
        if (shopTransitionController == null) return;
        shopTransitionController.CameraPanFinished -= OnCameraPanFinished;
        shopTransitionController.CameraPanFinished += OnCameraPanFinished;
    }

    private void UnhookTransitionEvents()
    {
        if (shopTransitionController == null) return;
        shopTransitionController.CameraPanFinished -= OnCameraPanFinished;
    }

    private void OnCameraPanFinished()
    {
        RefreshUI();
    }

    #endregion

    #region Controller Integration

    public void OnUIMovement(InputValue context)
    {
        if (SelectingButtons) return;
        Vector2 moveVector = context.Get<Vector2>();
        if (CurrentState == ShopState.Browsing)
        {
            if (moveVector.y < -movementThreshold)
            {
                if (keepMoving)
                {
                    keepMoving = false;
                    currentOfferIndex++;
                    if (currentOfferIndex >= _shelf.OfferEntries.Count)
                    {
                        currentOfferIndex--;
                    }
                    renderTextureRaycaster.ClearHover();
                    currentOfferObject = _shelf.OfferEntries[currentOfferIndex].gameObject;
                }
            }
            else if (moveVector.y > movementThreshold)
            {
                if (keepMoving)
                {
                    keepMoving = false;
                    currentOfferIndex--;
                    renderTextureRaycaster.ClearHover();
                    if (currentOfferIndex < 0)
                    {
                        currentOfferIndex = 0;
                        currentOfferObject = null;
                        SelectingButtons = true;
                        uiScript.SelectButton();
                        TooltipManager.Hide();
                        return;
                    }
                    currentOfferObject = _shelf.OfferEntries[currentOfferIndex].gameObject;
                }

            }
            else if (moveVector.x < -movementThreshold)
            {
                if (keepMoving)
                {
                    SelectBalls();
                }

            }
            else
            {
                keepMoving = true;
            }
        }
        else if (CurrentState == ShopState.PlacingComponent)
        {
            if (moveVector.x < -movementThreshold)
            {
                if (keepMoving)
                {
                    keepMoving = false;
                    _targetComponentIndex--;
                    ShopOffer offer = currentOfferObject.GetComponent<ShopOffer3DEntry>().Offer;
                    SetTargetComponent(offer.ComponentDef.ComponentType);
                }
            }
            else if (moveVector.x > movementThreshold)
            {
                if (keepMoving)
                {
                    keepMoving = false;
                    _targetComponentIndex++;
                    ShopOffer offer = currentOfferObject.GetComponent<ShopOffer3DEntry>().Offer;
                    SetTargetComponent(offer.ComponentDef.ComponentType);
                }
            }
            else
            {
                keepMoving = true;
            }
        } else if (CurrentState == ShopState.PlacingBall)
        {
            if (moveVector.y < -movementThreshold)
            {
                if (keepMoving)
                {
                    _hand.SetSlotHoverHighlight(currentBallIndex, false);
                    currentBallIndex--;
                    if (currentBallIndex < 0)
                    {
                        currentBallIndex = ballSpawner.SlotCount - 1;
                    }
                }
            } else if (moveVector.y > movementThreshold)
            {
                if (keepMoving)
                {
                    _hand.SetSlotHoverHighlight(currentBallIndex, false);
                    currentBallIndex++;
                    if (currentBallIndex > ballSpawner.SlotCount - 1)
                    {
                        currentBallIndex = 0;
                    }
                }
            } else
            {
                keepMoving = true;
            }
        } else if (CurrentState == ShopState.SelectingBall)
        {
            if (moveVector.y < -movementThreshold)
            {
                if (keepMoving)
                {
                    _hand.SetSlotHoverHighlight(currentBallIndex, false);
                    shopHub.SetHovered(false);
                    currentBallIndex--;
                    if (currentBallIndex < 0)
                    {
                        currentBallIndex = ballSpawner.SlotCount;
                    }
                }
            }
            else if (moveVector.y > movementThreshold)
            {
                if (keepMoving)
                {
                    _hand.SetSlotHoverHighlight(currentBallIndex, false);
                    currentBallIndex++;
                    if (currentBallIndex == ballSpawner.SlotCount)
                    {
                        shopHub.SetHovered(true);
                    } else if (currentBallIndex > ballSpawner.SlotCount)
                    {
                        shopHub.SetHovered(false);
                        currentBallIndex = 0;
                    }
                }
            }
            else if (moveVector.x > movementThreshold)
            {
                if (keepMoving)
                {
                    SelectingButtons = true;
                    uiScript.SelectButton();
                    TooltipManager.Hide();
                    selectedBallIndex = -1;
                    _hand.ClearSwapSelection();
                }
            } else
            {
                keepMoving = true;
            }
        }
    }

    public void OnEnter()
    {
        if (currentOfferObject == null && CurrentState != ShopState.SelectingBall) return;
        
        if (CurrentState == ShopState.Browsing)
        {
            if (currentOfferObject.GetComponent<Ball>())
            {
                CurrentState = ShopState.PlacingBall;
                ShopOffer offer = currentOfferObject.GetComponent<ShopOffer3DEntry>().Offer;
                currentBallIndex = 0;
            } else
            {
                CurrentState = ShopState.PlacingComponent;
                ShopOffer offer = currentOfferObject.GetComponent<ShopOffer3DEntry>().Offer;
                _placement.SetSelectionStateForPlacement(offer.ComponentDef.ComponentType);
                _targetComponentIndex = 0;
                SetTargetComponent(offer.ComponentDef.ComponentType);
            }
        }
        else if (CurrentState == ShopState.PlacingComponent)
        {
            ShopOffer3DEntry offer = currentOfferObject.GetComponent<ShopOffer3DEntry>();
            ConfirmDragDropBoardPurchase(offer.OfferIndex, _targetComponent);
            CheckForEmptyShop();
        } else if (CurrentState == ShopState.PlacingBall)
        {
            ShopOffer3DEntry offer = currentOfferObject.GetComponent<ShopOffer3DEntry>();
            if (!ballSpawner.GetHandBallAtSlot(currentBallIndex))
            {
                AutoBuyBallOffer(offer.OfferIndex, offer.Offer, currentBallIndex);
                _hand.SetSlotHoverHighlight(currentBallIndex, false);
                CurrentState = ShopState.Browsing;
                CheckForEmptyShop();
            } else
            {
                ConfirmDragDropBallReplace(offer.OfferIndex, currentBallIndex);
                CurrentState = ShopState.Browsing;
                CheckForEmptyShop();
            }
            
        } else if (CurrentState == ShopState.SelectingBall)
        {
            if (selectedBallIndex != -1 && currentBallIndex != ballSpawner.SlotCount)
            {
                Debug.Log("Swap");
                HandleBallSwapClick(currentBallIndex);
                selectedBallIndex = -1;
            } else if (selectedBallIndex != -1 && currentBallIndex == ballSpawner.SlotCount)
            {
                OnHandBallDragSell(selectedBallIndex, shopHub);
                selectedBallIndex = -1;
            } else
            {
                selectedBallIndex = currentBallIndex;
                _hand.SetSwapSelectedSlot(selectedBallIndex);
            }
        }

    }

    private void SetTargetComponent(BoardComponentType typeOfComponent)
    {
        if (typeOfComponent == BoardComponentType.Bumper)
        {
            int index = _targetComponentIndex % _placement.Bumpers.Count;
            if (index < 0)
            {
                index = _placement.Bumpers.Count - 1;
                _targetComponentIndex = index;
            }
            _targetComponent = _placement.Bumpers[index];
        }
        else if (typeOfComponent == BoardComponentType.Target)
        {
            int index = _targetComponentIndex % _placement.Targets.Count;
            if (index < 0)
            {
                index = _placement.Targets.Count - 1;
                _targetComponentIndex = index;
            }
            _targetComponent = _placement.Targets[index];
        }
        else if (typeOfComponent == BoardComponentType.Flipper)
        {
            int index = _targetComponentIndex % _placement.Flippers.Count;
            if (index < 0)
            {
                index = _placement.Flippers.Count - 1;
                _targetComponentIndex = index;
            }
            _targetComponent = _placement.Flippers[index];
        }
    }

    public void OnBack()
    {
        if (CurrentState == ShopState.SelectingBall && selectedBallIndex != -1)
        {
            selectedBallIndex = -1;
            _hand.ClearSwapSelection();
            return;
        }

        CurrentState = ShopState.Browsing;
        _placement.Cleanup();
    }

    public void SelectShop()
    {
        if (_shelf.OfferEntries.Count == 0)
        {
            uiScript.SelectButton();
            return;
        }

        currentOfferIndex = 0;
        SelectingButtons = false;
        currentOfferObject = _shelf.OfferEntries[currentOfferIndex].gameObject;
        CurrentState = ShopState.Browsing;
        CheckForEmptyShop();
    }

    public void SelectBalls()
    {
        if (ballSpawner.HandCount == 0)
        {
            uiScript.SelectButton();
            return;
        }

        CurrentState = ShopState.SelectingBall;
        currentBallIndex = 0;
        selectedBallIndex = -1;
        SelectingButtons = false;
    }

    private void CheckForEmptyShop()
    {
        if (_shelf.OfferEntries.Count == 0)
        {
            currentOfferIndex = 0;
            currentOfferObject = null;
            SelectingButtons = true;
            uiScript.SelectButton();
            TooltipManager.Hide();
            return;
        }
    }

    private void Update()
    {
        if (SelectingButtons) return;

        if (Mouse.current.IsPressed())
        {
            SelectingButtons = true;
            uiScript.SelectButton();
            TooltipManager.Hide();
            selectedBallIndex = -1;
            _hand.ClearSwapSelection();
            return;
        }

        if (CurrentState == ShopState.Browsing && currentOfferObject)
        {
            renderTextureRaycaster.HandleControllerHighlight(currentOfferObject);
        }
        else if (CurrentState == ShopState.PlacingComponent)
        {
            renderTextureRaycaster.HandleControllerHighlight(_targetComponent.gameObject);
        } else if (CurrentState == ShopState.PlacingBall || CurrentState == ShopState.SelectingBall)
        {
            GameObject ball = ballSpawner.GetHandBallAtSlot(currentBallIndex);
            if (ball)
            {
                renderTextureRaycaster.HandleControllerHighlight(ball);
            } else if (currentBallIndex != ballSpawner.SlotCount)
            {
                _hand.SetSlotHoverHighlight(currentBallIndex, true);
            } else
            {
                shopHub.SetHovered(true);
                renderTextureRaycaster.HandleControllerHighlight(shopHub.gameObject);
            }
            
        }

    }

    #endregion

}