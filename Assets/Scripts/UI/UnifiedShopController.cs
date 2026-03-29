using System;
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
            var loadout = rulesManager != null ? rulesManager.GetBallLoadoutSnapshot() : new System.Collections.Generic.List<BallDefinition>();
            var prefabs = new System.Collections.Generic.List<GameObject>(loadout.Count);
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
    }

    private void OnDisable()
    {
        UnhookTransitionEvents();

        _shelf.Cleanup();
        _hand.Cleanup();
        _placement.Cleanup();

        _dragBallHasRoom = false;
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
        if (_selectedOffer == null || rulesManager == null) return;

        if (!rulesManager.TrySpendCoins(_selectedOffer.Price))
        {
            SetPrompt($"Not enough coins for {_selectedOffer.DisplayName}.");
            AudioManager.Instance?.PlayFailedPurchase();
            if (confirmPanel != null) confirmPanel.Hide();
            RefreshUI();
            return;
        }

        AudioManager.Instance?.PlayPurchase();

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

        bool isBumper = _selectedOffer.ComponentDef.ComponentType == BoardComponentType.Bumper;
        if (isBumper && component.componentType != BoardComponentType.Bumper) return;
        if (!isBumper && component.componentType != BoardComponentType.Target) return;

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
        if (_selectedOffer == null || _targetComponent == null || rulesManager == null) return;

        _placement.ReplaceComponent(_targetComponent, _selectedOffer.ComponentDef);
        _shelf.ConsumeOffer(_selectedOfferIndex);
        
        BoardComponentDefinition def = _selectedOffer.ComponentDef;
        ExitPlacementMode();

        SetPrompt($"Placed {def.GetSafeDisplayName()}.");
        RefreshUI();
    }

    public void ConfirmBallPlacement()
    {
        if (_selectedOffer == null || rulesManager == null) return;

        BallDefinition def = _selectedOffer.BallDef;
        int slotToReplace = _targetBallSlotIndex;
        var loadout = rulesManager.GetBallLoadoutSnapshot();

        if (slotToReplace >= 0 && slotToReplace < loadout.Count)
        {
            BallDefinition oldBall = loadout[slotToReplace];
            if (oldBall != null)
            {
                int sellPrice = (Mathf.Max(0, oldBall.Price) + 1) / 2;
                rulesManager.AddCoinsUnscaled(sellPrice);
            }
            rulesManager.ReplaceBallInLoadout(slotToReplace, def);
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
        if (_selectedOffer != null && rulesManager != null)
        {
            rulesManager.AddCoinsUnscaled(_selectedOffer.Price);
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
        if (rulesManager == null) return;

        if (rulesManager.Coins < rerollCost)
        {
            SetPrompt($"Not enough coins to reroll (${rerollCost}).");
            AudioManager.Instance?.PlayFailedPurchase();
            RefreshUI();
            return;
        }

        if (!rulesManager.TrySpendCoins(rerollCost)) return;

        _shelf.RebuildOffers();
        SetPrompt("Rerolled shop.");
        AudioManager.Instance?.PlayReroll();
        RefreshUI();
    }

    public void CloseAndContinue()
    {
        if (_closeRequested) return;
        _closeRequested = true;

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
            if (!offerIsBumper && bc.componentType != BoardComponentType.Target)
            {
                SetPrompt("Drop targets onto targets.");
                return;
            }

            ShowDragDropBoardPurchaseConfirm(offerIndex, bc);
            return;
        }

        if (rulesManager == null) return;

        int currentCount = rulesManager.BallLoadoutCount;
        int cap = Mathf.Max(1, rulesManager.MaxBalls);

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

        var loadout = rulesManager.GetBallLoadoutSnapshot();
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
            int currentCount = rulesManager != null ? rulesManager.BallLoadoutCount : 0;
            int cap = rulesManager != null ? Mathf.Max(1, rulesManager.MaxBalls) : 1;
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
        if (fromSlot < 0 || toSlot < 0 || fromSlot == toSlot || rulesManager == null) return;

        bool swapped = rulesManager.SwapBallLoadoutSlots(fromSlot, toSlot);
        if (!swapped)
        {
            SetPrompt("Could not swap those balls.");
            return;
        }

        if (ballSpawner != null) ballSpawner.SwapHandBallsAnimated(fromSlot, toSlot);
        
        AudioManager.Instance?.PlaySwapSlot();
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

        if (rulesManager == null) return;

        bool swapped = rulesManager.SwapBallLoadoutSlots(slotA, slotB);
        if (!swapped)
        {
            SetPrompt("Could not swap those balls.");
            return;
        }

        if (ballSpawner != null) ballSpawner.SwapHandBallsAnimated(slotA, slotB);

        AudioManager.Instance?.PlaySwapSlot();
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

        int currentCount = rulesManager.BallLoadoutCount;
        int cap = Mathf.Max(1, rulesManager.MaxBalls);

        if (currentCount < cap)
        {
            int addSlot = GetFirstEmptySlotIndex();
            bool added = rulesManager.AddBallToLoadout(_selectedOffer.BallDef);
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
        if (rulesManager == null || offer == null) return;

        if (!rulesManager.TrySpendCoins(offer.Price))
        {
            SetPrompt($"Not enough coins for {offer.DisplayName}.");
            AudioManager.Instance?.PlayFailedPurchase();
            RefreshUI();
            return;
        }

        AudioManager.Instance?.PlayPurchase();

        int addSlot = GetFirstEmptySlotIndex();
        bool added = rulesManager.AddBallToLoadout(offer.BallDef);

        if (!added)
        {
            rulesManager.AddCoinsUnscaled(offer.Price);
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
        if (rulesManager == null) return -1;
        var loadout = rulesManager.GetBallLoadoutSnapshot();
        return loadout.Count;
    }

    private Vector2 GetConfirmAnchorScreenPoint()
    {
        if (TooltipManager.TryGetTooltipScreenRectCenter(out Vector2 center))
        {
            TooltipManager.Hide();
            return center;
        }

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null) return mouse.position.ReadValue();
        return Vector2.zero;
#else
        return Input.mousePosition;
#endif
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

        var loadout = rulesManager.GetBallLoadoutSnapshot();
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
        if (rulesManager == null || target == null || offer == null || !offer.IsValid) return;

        if (!rulesManager.TrySpendCoins(offer.Price))
        {
            SetPrompt($"Not enough coins for {offer.DisplayName}.");
            AudioManager.Instance?.PlayFailedPurchase();
            if (confirmPanel != null) confirmPanel.Hide();
            RefreshUI();
            return;
        }

        AudioManager.Instance?.PlayPurchase();
        if (confirmPanel != null) confirmPanel.Hide();

        _selectedOffer = offer;
        _selectedOfferIndex = offerIndex;
        _targetComponent = target;
        ConfirmComponentPlacement();
    }

    private void ShowDragDropBallReplaceConfirm(int offerIndex, int slotIndex)
    {
        ShopOffer offer = _shelf.GetOffer(offerIndex);
        if (offer == null || !offer.IsValid || rulesManager == null) return;

        if (confirmPanel == null)
        {
            ConfirmDragDropBallReplace(offerIndex, slotIndex);
            return;
        }

        var loadout = rulesManager.GetBallLoadoutSnapshot();
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
        if (rulesManager == null || offer == null || !offer.IsValid) return;

        if (!rulesManager.TrySpendCoins(offer.Price))
        {
            SetPrompt($"Not enough coins for {offer.DisplayName}.");
            AudioManager.Instance?.PlayFailedPurchase();
            if (confirmPanel != null) confirmPanel.Hide();
            RefreshUI();
            return;
        }

        AudioManager.Instance?.PlayPurchase();
        if (confirmPanel != null) confirmPanel.Hide();

        _selectedOffer = offer;
        _selectedOfferIndex = offerIndex;
        _targetBallSlotIndex = slotIndex;
        ConfirmBallPlacement();
    }

    public void RefreshUI()
    {
        if (coinsText != null && rulesManager != null) coinsText.text = $"${rulesManager.Coins}";
    }

    private void SetPrompt(string msg)
    {
        if (promptText != null) promptText.text = msg ?? string.Empty;
    }

    private void ResolveReferences()
    {
        if (rulesManager == null) rulesManager = ServiceLocator.Get<GameRulesManager>();
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
