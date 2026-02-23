using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Minimal 2-tab controller for the Shop panel.
/// - Two tab buttons
/// - Two content roots (enable one, disable the other)
/// - Optional "selected" visuals per tab
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopTabsController : MonoBehaviour
{
    public enum Tab
    {
        Balls = 0,
        BoardComponents = 1
    }

    [Header("UI")]
    [SerializeField] private Button ballsTabButton;
    [SerializeField] private Button boardComponentsTabButton;

    [Header("Content roots")]
    [SerializeField] private GameObject ballsTabRoot;
    [SerializeField] private GameObject boardComponentsTabRoot;

    [Header("Optional selected visuals")]
    [SerializeField] private GameObject ballsSelectedVisual;
    [SerializeField] private GameObject boardComponentsSelectedVisual;

    public Tab CurrentTab { get; private set; } = Tab.Balls;

    public event Action<Tab> TabChanged;

    private bool _wired;

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

        CurrentTab = tab;
        ApplyTab(tab, notify);
    }

    private void ApplyTab(Tab tab, bool notify)
    {
        bool showBalls = tab == Tab.Balls;

        if (ballsTabRoot != null) ballsTabRoot.SetActive(showBalls);
        if (boardComponentsTabRoot != null) boardComponentsTabRoot.SetActive(!showBalls);

        if (ballsTabButton != null) ballsTabButton.interactable = !showBalls;
        if (boardComponentsTabButton != null) boardComponentsTabButton.interactable = showBalls;

        if (ballsSelectedVisual != null) ballsSelectedVisual.SetActive(showBalls);
        if (boardComponentsSelectedVisual != null) boardComponentsSelectedVisual.SetActive(!showBalls);

        if (notify)
        {
            TabChanged?.Invoke(tab);
        }
    }

    private void WireButtonsIfNeeded()
    {
        ButtonSound globalSound = AudioManager.Instance.GetComponent<ButtonSound>();
        if (globalSound == null)
        {
            Debug.LogWarning("No ButtonSound attached to the AudioManager!");
            return;
        }
        if (_wired)
            return;

        if (ballsTabButton != null)
        {
            ballsTabButton.onClick.RemoveListener(SelectBallsTab);
            ballsTabButton.onClick.AddListener(SelectBallsTab);
            ballsTabButton.onClick.RemoveListener(globalSound.PlaySound);
            ballsTabButton.onClick.AddListener(globalSound.PlaySound);

            EventTrigger trigger = ballsTabButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = ballsTabButton.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerEnter);

            EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
            hoverEntry.eventID = EventTriggerType.PointerEnter;
            hoverEntry.callback.AddListener((data) => { globalSound.PlayHoverSound(); });
            
            trigger.triggers.Add(hoverEntry);
        }

        if (boardComponentsTabButton != null)
        {
            boardComponentsTabButton.onClick.RemoveListener(SelectBoardComponentsTab);
            boardComponentsTabButton.onClick.AddListener(SelectBoardComponentsTab);
            boardComponentsTabButton.onClick.RemoveListener(globalSound.PlaySound);
            boardComponentsTabButton.onClick.AddListener(globalSound.PlaySound);


            EventTrigger trigger = boardComponentsTabButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = boardComponentsTabButton.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerEnter);

            EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
            hoverEntry.eventID = EventTriggerType.PointerEnter;
            hoverEntry.callback.AddListener((data) => { globalSound.PlayHoverSound(); });
            
            trigger.triggers.Add(hoverEntry);
        }

        _wired = true;
    }

    private void UnwireButtons()
    {
        if (!_wired)
            return;

        if (ballsTabButton != null)
            ballsTabButton.onClick.RemoveListener(SelectBallsTab);

        if (boardComponentsTabButton != null)
            boardComponentsTabButton.onClick.RemoveListener(SelectBoardComponentsTab);

        _wired = false;
    }
}

