using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 3-tab controller for the Shop panel.
/// - Main (default), Balls, BoardComponents tab buttons
/// - Three content roots (enable one, disable the others)
/// - Optional "selected" visuals per tab
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopTabsController : MonoBehaviour
{
    public enum Tab
    {
        Main = 0,
        Balls = 1,
        BoardComponents = 2
    }

    [Header("UI")]
    [SerializeField] private Button mainTabButton;
    [SerializeField] private Button ballsTabButton;
    [SerializeField] private Button boardComponentsTabButton;

    [Header("Content roots")]
    [SerializeField] private GameObject mainTabRoot;
    [SerializeField] private GameObject ballsTabRoot;
    [SerializeField] private GameObject boardComponentsTabRoot;

    [Header("Optional selected visuals")]
    [SerializeField] private GameObject mainSelectedVisual;
    [SerializeField] private GameObject ballsSelectedVisual;
    [SerializeField] private GameObject boardComponentsSelectedVisual;

    public Tab CurrentTab { get; private set; } = Tab.Main;

    public event Action<Tab> TabChanged;

    private bool _wired;
    private bool _mainContentDeferred;

    /// <summary>
    /// Hides the Main tab content until <see cref="RevealMainTabContent"/> is called.
    /// Used to defer showing the Main panel until the shop transition animation completes.
    /// </summary>
    public void DeferMainTabContent()
    {
        _mainContentDeferred = true;
        if (mainTabRoot != null)
            mainTabRoot.SetActive(false);
    }

    /// <summary>
    /// Shows the Main tab content (call after shop transition is complete).
    /// </summary>
    public void RevealMainTabContent()
    {
        _mainContentDeferred = false;
        ApplyTab(CurrentTab, notify: false);
    }

    private void Awake()
    {
        WireButtonsIfNeeded();
        ApplyTab(CurrentTab, notify: false);
    }

    private void OnEnable()
    {
        WireButtonsIfNeeded();
        ApplyTab(CurrentTab, notify: false);
    }

    private void OnDisable()
    {
        UnwireButtons();
    }

    public void SelectMainTab()
    {
        SetTab(Tab.Main);
    }

    public void SelectBallsTab()
    {
        SetTab(Tab.Balls);
    }

    public void SelectBoardComponentsTab()
    {
        SetTab(Tab.BoardComponents);
    }

    public void SetTab(Tab tab, bool notify = true)
    {
        if (CurrentTab == tab)
        {
            ApplyTab(tab, notify: false);
            return;
        }

        AudioManager.Instance.PlayTabSwitch();

        CurrentTab = tab;
        ApplyTab(tab, notify);
    }

    private void ApplyTab(Tab tab, bool notify)
    {
        bool showMain = tab == Tab.Main;
        bool showBalls = tab == Tab.Balls;
        bool showBoardComponents = tab == Tab.BoardComponents;

        if (mainTabRoot != null)
            mainTabRoot.SetActive(showMain && !_mainContentDeferred);
        if (ballsTabRoot != null) ballsTabRoot.SetActive(showBalls);
        if (boardComponentsTabRoot != null) boardComponentsTabRoot.SetActive(showBoardComponents);

        if (mainTabButton != null) mainTabButton.interactable = !showMain;
        if (ballsTabButton != null) ballsTabButton.interactable = !showBalls;
        if (boardComponentsTabButton != null) boardComponentsTabButton.interactable = !showBoardComponents;

        if (mainSelectedVisual != null) mainSelectedVisual.SetActive(showMain);
        if (ballsSelectedVisual != null) ballsSelectedVisual.SetActive(showBalls);
        if (boardComponentsSelectedVisual != null) boardComponentsSelectedVisual.SetActive(showBoardComponents);

        if (notify)
        {
            TabChanged?.Invoke(tab);
        }
    }

    private void WireButtonsIfNeeded()
    {
        if (_wired) return;

        if (mainTabButton != null)
        {
            mainTabButton.onClick.RemoveListener(SelectMainTab);
            mainTabButton.onClick.AddListener(SelectMainTab);
        }

        if (ballsTabButton != null)
        {
            ballsTabButton.onClick.RemoveListener(SelectBallsTab);
            ballsTabButton.onClick.AddListener(SelectBallsTab);
        }

        if (boardComponentsTabButton != null)
        {
            boardComponentsTabButton.onClick.RemoveListener(SelectBoardComponentsTab);
            boardComponentsTabButton.onClick.AddListener(SelectBoardComponentsTab);
        }

        _wired = true;
    }

    private void UnwireButtons()
    {
        if (!_wired)
            return;

        if (mainTabButton != null)
            mainTabButton.onClick.RemoveListener(SelectMainTab);

        if (ballsTabButton != null)
            ballsTabButton.onClick.RemoveListener(SelectBallsTab);

        if (boardComponentsTabButton != null)
            boardComponentsTabButton.onClick.RemoveListener(SelectBoardComponentsTab);

        _wired = false;
    }
}

