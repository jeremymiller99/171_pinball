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
        PlacingBall
    }

    [Header("Refs")]
    [SerializeField] private GameRulesManager rulesManager;
    [SerializeField] private CoinController coinController;
    [SerializeField] private RunFlowController runFlowController;
    [SerializeField] private ShopTransitionController shopTransitionController;
    [SerializeField] private GameObject shopCanvasRoot;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("UI")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button rerollButton;
    [SerializeField] private int rerollCost = 2;

    [Header("Confirm Panel")]
    [SerializeField] private ShopConfirmPanel confirmPanel;

    public ShopState CurrentState { get; private set; } = ShopState.Browsing;

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
    private int _targetBallSlotIndex = -1;
    private bool _dragBallHasRoom;
    private bool _isDragPreviewActive;

    private ShopShipController _spaceship;

    private void Awake()
    {
        _shelf = GetComponent<ShopOfferShelfController>();
        _hand = GetComponent<ShopHandInteractionController>();
        _placement = GetComponent<ShopComponentPlacementController>();
        
        ResolveReferences();
    }

    private void OnEnable()
    {
        _closeRequested = false;
        CurrentState = ShopState.Browsing;

        ResolveReferences();

        if (ballSpawner != null)
        {
            var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
            var loadout = loadoutCtrl != null ? loadoutCtrl.GetBallLoadoutSnapshot() : new List<BallDefinition>();
            var prefabs = new List<GameObject>(loadout.Count);
            for (int i = 0; i < loadout.Count; i++) prefabs.Add(loadout[i]?.Prefab);
            ballSpawner.BuildHandFromPrefabs(prefabs);
        }

        _shelf.Initialize();
        _hand.Initialize();
        _placement.Initialize();

        WireContinueButton();
        WireRerollButton();
        HookTransitionEvents();

        if (confirmPanel != null) confirmPanel.Hide();

        RefreshUI();

        _spaceship = ServiceLocator.Get<ShopShipController>();
        if (_spaceship == null) _spaceship = FindFirstObjectByType<ShopShipController>();

        if (_spaceship != null)
        {
            _spaceship.SpaceshipParked += OnSpaceshipParked;
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

        _shelf.Cleanup();
        _hand.Cleanup();
        _placement.Cleanup();

        _dragBallHasRoom = false;
    }

    private void OnSpaceshipParked()
    {
        _shelf.RebuildOffers();
    }

    #region Click API

    public void OnOfferClicked(int offerIndex)
    {
        if (CurrentState != ShopState.Browsing) return;

        ShopOffer offer = _shelf.GetOffer(offerIndex);
        if (offer == null || !offer.IsValid) return;

        _hand.ClearSwapSelection();

        _selectedOffer = offer;
        _selectedOfferIndex = offerIndex;
        OfferSelected?.Invoke(offer);

        ShowPurchaseConfirmation(offer);
    }

    public void ConfirmPurchase()
    {
        if (_selectedOffer == null) return;

        if (coinController == null || !coinController.TrySpendCoins(_selectedOffer.Price))
        {
            SetPrompt($"Not enough coins for {_selectedOffer.DisplayName}.");
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            if (confirmPanel != null) confirmPanel.Hide();
            RefreshUI();
            return;
        }

        ServiceLocator.Get<AudioManager>()?.PlayPurchase();

        if (confirmPanel != null) confirmPanel.Hide();

        if (_selectedOffer.Type == ShopOffer.OfferType.BoardComponent)
            EnterComponentPlacementMode();
        else
            EnterBallPlacementMode();
    }

    public void OnBoardComponentClicked(BoardComponent component)
    {
        if (CurrentState != ShopState.PlacingComponent || _selectedOffer == null) return;
        if (component == null) return;

        BoardComponentType typeOfDef = _selectedOffer.ComponentDef.ComponentType;
        if (typeOfDef != component.componentType) return;

        _targetComponent = component;
        ShowComponentReplaceConfirmation(component);
    }

    public void OnBallSlotClicked(int slotIndex)
    {
        if (CurrentState == ShopState.Browsing)
        {
            HandleBallSwapClick(slotIndex);
            return;
        }

        if (CurrentState != ShopState.PlacingBall || _selectedOffer == null) return;

        _targetBallSlotIndex = slotIndex;
        ShowBallReplaceConfirmation(slotIndex);
    }

    public void ConfirmComponentPlacement()
    {
        if (_selectedOffer == null || _targetComponent == null) return;

        _placement.ReplaceComponent(_targetComponent, _selectedOffer.ComponentDef);
        _shelf.ConsumeOffer(_selectedOfferIndex);
        
        BoardComponentDefinition def = _selectedOffer.ComponentDef;
        ExitPlacementMode();

        SetPrompt($"Placed {def.GetSafeDisplayName()}.");
        RefreshUI();
    }

    public void ConfirmBallPlacement()
    {
        if (_selectedOffer == null) return;

        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null) return;

        BallDefinition def = _selectedOffer.BallDef;
        int slotToReplace = _targetBallSlotIndex;
        var loadout = loadoutCtrl.GetBallLoadoutSnapshot();

        if (slotToReplace >= 0 && slotToReplace < loadout.Count)
        {
            BallDefinition oldBall = loadout[slotToReplace];
            if (oldBall != null)
            {
                int sellPrice = (Mathf.Max(0, oldBall.Price) + 1) / 2;
                coinController?.AddCoinsUnscaled(sellPrice);
            }
            loadoutCtrl.ReplaceBallInLoadout(slotToReplace, def);
        }

        _shelf.ConsumeOffer(_selectedOfferIndex);
        ExitPlacementMode();

        if (ballSpawner != null && def != null && def.Prefab != null && slotToReplace >= 0)
        {
            ballSpawner.ReplaceBallAnimated(slotToReplace, def.Prefab);
        }

        SetPrompt($"Replaced with {def.GetSafeDisplayName()}.");
        RefreshUI();
    }

    public void CancelPlacement()
    {
        if (_selectedOffer != null)
        {
            coinController?.AddCoinsUnscaled(_selectedOffer.Price);
            SetPrompt("Purchase cancelled. Coins refunded.");
        }

        ExitPlacementMode();
        RefreshUI();
    }

    public void CancelConfirmation()
    {
        if (confirmPanel != null) confirmPanel.Hide();

        if (CurrentState == ShopState.Browsing)
        {
            _selectedOffer = null;
            _selectedOfferIndex = -1;
        }
    }

    public void RerollOffers()
    {
        if (coinController == null || coinController.Coins < rerollCost)
        {
            SetPrompt($"Not enough coins to reroll (${rerollCost}).");
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            RefreshUI();
            return;
        }

        if (!coinController.TrySpendCoins(rerollCost)) return;

        SetPrompt("Rerolling...");
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
        if (_closeRequested) return;
        _closeRequested = true;

        _shelf.ClearOfferDisplays();

        ExitPlacementMode();
        ShopClosed?.Invoke();

        if (runFlowController != null)
        {
            runFlowController.ContinueAfterShop();
            return;
        }

        if (shopTransitionController != null)
        {
            shopTransitionController.CloseShopThen(() => rulesManager?.OnShopClosed());
            return;
        }

        rulesManager?.OnShopClosed();

        if (shopCanvasRoot != null) shopCanvasRoot.SetActive(false);
    }

    #endregion

    #region Drag API

    public void TryDropOfferAfterDrag(int offerIndex, GameObject hitObject, Ray worldRay)
    {
        if (CurrentState != ShopState.Browsing) return;

        ShopOffer offer = _shelf.GetOffer(offerIndex);
        if (offer == null || !offer.IsValid) return;

        if (offer.Type == ShopOffer.OfferType.BoardComponent)
        {
            BoardComponent bc = hitObject != null ? hitObject.GetComponentInParent<BoardComponent>() : null;
            if (bc == null)
            {
                SetPrompt("Drop onto a bumper or target on the board.");
                return;
            }

            bool offerIsBumper = offer.ComponentDef.ComponentType == BoardComponentType.Bumper;
            if (offerIsBumper && bc.componentType != BoardComponentType.Bumper)
            {
                SetPrompt("Drop bumpers onto bumpers.");
                return;
            }

            bool offerIsTarget = offer.ComponentDef.ComponentType == BoardComponentType.Target;
            if (offerIsTarget && bc.componentType != BoardComponentType.Target)
            {
                SetPrompt("Drop targets onto targets.");
                return;
            }

            bool offerIsFlipper = offer.ComponentDef.ComponentType == BoardComponentType.Flipper;
            if (offerIsFlipper && bc.componentType != BoardComponentType.Flipper)
            {
                SetPrompt("Drop flippers onto flippers.");
            }

            ShowDragDropBoardPurchaseConfirm(offerIndex, bc);
            return;
        }

        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null) return;

        int currentCount = loadoutCtrl.BallLoadoutCount;
        int cap = Mathf.Max(1, loadoutCtrl.MaxBalls);

        if (currentCount < cap)
        {
            AutoBuyBallOffer(offerIndex, offer);
            return;
        }

        int slotIndex = -1;
        if (hitObject != null)
        {
            BallHandSlotMarker marker = hitObject.GetComponentInParent<BallHandSlotMarker>();
            if (marker != null && marker.SlotIndex >= 0) slotIndex = marker.SlotIndex;
        }

        if (slotIndex < 0) _hand.TryGetHandBallSlotFromRay(worldRay, out slotIndex);

        if (slotIndex < 0)
        {
            SetPrompt("Drop onto a ball in your hand to replace.");
            return;
        }

        var loadout = loadoutCtrl.GetBallLoadoutSnapshot();
        if (slotIndex >= loadout.Count) return;

        ShowDragDropBallReplaceConfirm(offerIndex, slotIndex);
    }

    public void OnOfferDragStarted(ShopOffer offer)
    {
        if (offer == null || CurrentState != ShopState.Browsing) return;

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
            if (_dragBallHasRoom) return;

            int slot = -1;
            if (hitObject != null)
            {
                var marker = hitObject.GetComponentInParent<BallHandSlotMarker>();
                if (marker != null) slot = marker.SlotIndex;
            }

            if (slot < 0) _hand.TryGetHandBallSlotFromRay(worldRay, out slot);
            
            _hand.UpdateDragHover(slot);
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
        else if (CurrentState == ShopState.PlacingBall)
        {
            int slot = -1;
            if (hitObject != null)
            {
                var marker = hitObject.GetComponentInParent<BallHandSlotMarker>();
                if (marker != null) slot = marker.SlotIndex;
            }

            if (slot < 0) _hand.TryGetHandBallSlotFromRay(worldRay, out slot);

            _hand.UpdatePlacementHover(slot);
        }
    }

    public void OnHandBallDragStarted(int dragSlot)
    {
        if (CurrentState != ShopState.Browsing) return;
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

    public void OnHandBallDragSwap(int fromSlot, int toSlot)
    {
        if (CurrentState != ShopState.Browsing) return;

        _hand.ClearSwapSelection();
        var lc = ServiceLocator.Get<BallLoadoutController>();
        if (fromSlot < 0 || toSlot < 0 || fromSlot == toSlot || lc == null) return;

        bool swapped = lc.SwapBallLoadoutSlots(fromSlot, toSlot);
        if (!swapped)
        {
            SetPrompt("Could not swap those balls.");
            return;
        }

        if (ballSpawner != null) ballSpawner.SwapHandBallsAnimated(fromSlot, toSlot);
        
        ServiceLocator.Get<AudioManager>()?.PlaySwapSlot();
        SetPrompt("Balls swapped.");
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
            SetPrompt("Click another ball to swap, or the same ball to cancel.");
            return;
        }

        if (selected == slotIndex)
        {
            _hand.ClearSwapSelection();
            SetPrompt("");
            return;
        }

        int slotA = selected;
        int slotB = slotIndex;
        _hand.ClearSwapSelection();

        var lc = ServiceLocator.Get<BallLoadoutController>();
        if (lc == null) return;

        bool swapped = lc.SwapBallLoadoutSlots(slotA, slotB);
        if (!swapped)
        {
            SetPrompt("Could not swap those balls.");
            return;
        }

        if (ballSpawner != null) ballSpawner.SwapHandBallsAnimated(slotA, slotB);

        ServiceLocator.Get<AudioManager>()?.PlaySwapSlot();
        SetPrompt("Balls swapped.");
    }

    private void EnterComponentPlacementMode()
    {
        CurrentState = ShopState.PlacingComponent;
        _placement.SetSelectionStateForPlacement(_selectedOffer.ComponentDef.ComponentType);
        SetPrompt($"Click a {_selectedOffer.ComponentDef.ComponentType} on the board to replace with {_selectedOffer.DisplayName}.");
        RefreshUI();
    }

    private void EnterBallPlacementMode()
    {
        CurrentState = ShopState.PlacingBall;

        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null)
        {
            SetPrompt("Ball loadout unavailable.");
            RefreshUI();
            return;
        }

        int currentCount = loadoutCtrl.BallLoadoutCount;
        int cap = Mathf.Max(1, loadoutCtrl.MaxBalls);

        if (currentCount < cap)
        {
            int addSlot = GetFirstEmptySlotIndex();
            bool added = loadoutCtrl.AddBallToLoadout(_selectedOffer.BallDef);
            if (added)
            {
                _shelf.ConsumeOffer(_selectedOfferIndex);
                if (ballSpawner != null && _selectedOffer.BallDef?.Prefab != null && addSlot >= 0)
                {
                    ballSpawner.AddBallAnimated(_selectedOffer.BallDef.Prefab, addSlot);
                }

                SetPrompt($"Added {_selectedOffer.DisplayName} to loadout.");
                ExitPlacementMode();
                RefreshUI();
                return;
            }
        }

        _hand.HighlightAllHandBallsWaitColor();
        SetPrompt($"Click a ball in your hand to replace with {_selectedOffer.DisplayName}.");
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
        _targetBallSlotIndex = -1;

        if (confirmPanel != null) confirmPanel.Hide();

        PlacementCancelled?.Invoke();
    }

    private void AutoBuyBallOffer(int offerIndex, ShopOffer offer)
    {
        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null || offer == null) return;

        if (coinController == null || !coinController.TrySpendCoins(offer.Price))
        {
            SetPrompt($"Not enough coins for {offer.DisplayName}.");
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            RefreshUI();
            return;
        }

        ServiceLocator.Get<AudioManager>()?.PlayPurchase();

        int addSlot = GetFirstEmptySlotIndex();
        bool added = loadoutCtrl.AddBallToLoadout(offer.BallDef);

        if (!added)
        {
            coinController?.AddCoinsUnscaled(offer.Price);
            SetPrompt("Loadout full -- could not add ball.");
            RefreshUI();
            return;
        }

        if (ballSpawner != null && offer.BallDef?.Prefab != null && addSlot >= 0)
        {
            ballSpawner.AddBallAnimated(offer.BallDef.Prefab, addSlot);
        }

        _shelf.ConsumeOffer(offerIndex);
        SetPrompt($"Added {offer.DisplayName} to loadout.");
        RefreshUI();
    }

    private int GetFirstEmptySlotIndex()
    {
        var loadoutCtrl = ServiceLocator.Get<BallLoadoutController>();
        if (loadoutCtrl == null) return -1;
        var loadout = loadoutCtrl.GetBallLoadoutSnapshot();
        return loadout.Count;
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

    private void ShowPurchaseConfirmation(ShopOffer offer)
    {
        if (confirmPanel == null) return;

        confirmPanel.Show(
            $"Buy {offer.DisplayName} for ${offer.Price}?",
            offer.DisplayName, offer.Description, offer.Icon,
            ConfirmPurchase, CancelConfirmation, GetConfirmAnchorScreenPoint());
    }

    private void ShowComponentReplaceConfirmation(BoardComponent target)
    {
        if (confirmPanel == null)
        {
            ConfirmComponentPlacement();
            return;
        }

        var link = target.GetComponent<BoardComponentDefinitionLink>();
        string targetName = "this component";
        if (link != null && link.TryGetDefinition(out var def)) targetName = def.GetSafeDisplayName();

        confirmPanel.Show(
            $"Replace {targetName} with {_selectedOffer.DisplayName}?",
            _selectedOffer.DisplayName, _selectedOffer.Description, _selectedOffer.Icon,
            ConfirmComponentPlacement, () => { if (confirmPanel != null) confirmPanel.Hide(); },
            GetConfirmAnchorScreenPoint());
    }

    private void ShowBallReplaceConfirmation(int slotIndex)
    {
        if (confirmPanel == null)
        {
            ConfirmBallPlacement();
            return;
        }

        var lc = ServiceLocator.Get<BallLoadoutController>();
        var loadout = lc != null ? lc.GetBallLoadoutSnapshot() : new List<BallDefinition>();
        BallDefinition old = (slotIndex >= 0 && slotIndex < loadout.Count) ? loadout[slotIndex] : null;
        string oldName = old != null ? old.GetSafeDisplayName() : "(Empty)";
        int sellPrice = old != null ? (Mathf.Max(0, old.Price) + 1) / 2 : 0;

        string msg = old != null
            ? $"Replace {oldName} with {_selectedOffer.DisplayName}?\nSell {oldName} for ${sellPrice}."
            : $"Add {_selectedOffer.DisplayName} to Slot {slotIndex + 1}?";

        confirmPanel.Show(
            msg, _selectedOffer.DisplayName, _selectedOffer.Description, _selectedOffer.Icon,
            ConfirmBallPlacement, () => { if (confirmPanel != null) confirmPanel.Hide(); },
            GetConfirmAnchorScreenPoint());
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
            SetPrompt($"Not enough coins for {offer.DisplayName}.");
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            if (confirmPanel != null) confirmPanel.Hide();
            RefreshUI();
            return;
        }

        ServiceLocator.Get<AudioManager>()?.PlayPurchase();
        if (confirmPanel != null) confirmPanel.Hide();

        _selectedOffer = offer;
        _selectedOfferIndex = offerIndex;
        _targetComponent = target;
        ConfirmComponentPlacement();
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

        if (coinController == null || !coinController.TrySpendCoins(offer.Price))
        {
            SetPrompt($"Not enough coins for {offer.DisplayName}.");
            ServiceLocator.Get<AudioManager>()?.PlayFailedPurchase();
            if (confirmPanel != null) confirmPanel.Hide();
            RefreshUI();
            return;
        }

        ServiceLocator.Get<AudioManager>()?.PlayPurchase();
        if (confirmPanel != null) confirmPanel.Hide();

        _selectedOffer = offer;
        _selectedOfferIndex = offerIndex;
        _targetBallSlotIndex = slotIndex;
        ConfirmBallPlacement();
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
        if (rulesManager == null) rulesManager = ServiceLocator.Get<GameRulesManager>();
        if (coinController == null) coinController = ServiceLocator.Get<CoinController>();
        if (runFlowController == null) runFlowController = ServiceLocator.Get<RunFlowController>();
        if (shopTransitionController == null) shopTransitionController = ServiceLocator.Get<ShopTransitionController>();
        if (shopCanvasRoot == null) shopCanvasRoot = gameObject;
        if (ballSpawner == null) ballSpawner = ServiceLocator.Get<BallSpawner>();
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
}
