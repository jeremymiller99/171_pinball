// Created with Claude Code (Opus 4.8) by JJ on 2026-06-08: monitor-3 progression
// panel — two clickable text tabs (Ships / Items); the selected one turns green.
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drives the two text tabs on the monitor-3 progression canvas (reached when
/// the player chooses "Progression" on monitor 1). The tabs are plain
/// <see cref="TMP_Text"/> objects — "Ships" and "Items" — that work like the
/// monitor-1 menu buttons: click a tab to select it, or cycle between them with
/// the arrow keys and commit with Enter. (Selection toggles on click rather than
/// hover.) The selected tab is tinted green; the other keeps its authored color.
///
/// Optionally assign a content GameObject per tab; the matching content is shown
/// for the selected tab and the other is hidden. Leave them unassigned to change
/// only the tab color.
/// </summary>
[DisallowMultipleComponent]
public sealed class Monitor3Controller : MonoBehaviour
{
    private enum Tab
    {
        Ships = 0,
        Items = 1
    }

    [Header("Tabs (assign the TMP_Text objects)")]
    [Tooltip("The 'Ships' tab text object.")]
    [SerializeField] private TMP_Text shipsTab;

    [Tooltip("The 'Items' tab text object.")]
    [SerializeField] private TMP_Text itemsTab;

    [Header("Tab content (optional)")]
    [Tooltip("Shown while the Ships tab is selected. Leave null to change only the tab color.")]
    [SerializeField] private GameObject shipsContent;

    [Tooltip("Shown while the Items tab is selected. Leave null to change only the tab color.")]
    [SerializeField] private GameObject itemsContent;

    [Header("Selection styling")]
    [Tooltip("Color applied to the currently selected tab.")]
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.9f, 0.4f, 1f);

    [Tooltip("Prefix added in front of the selected tab's label (e.g. \"> \"). Optional.")]
    [SerializeField] private string selectedPrefix = "";

    [Tooltip("Suffix added after the selected tab's label (e.g. \" <\"). Optional.")]
    [SerializeField] private string selectedSuffix = "";

    [Header("Runtime (debug)")]
    [SerializeField] private Tab currentTab = Tab.Ships;

    // Per-tab cached data so we can restore the unselected look.
    private TMP_Text[] _tabs;
    private GameObject[] _contents;
    private string[] _baseLabels;
    private Color[] _baseColors;

    private bool _initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        // Re-apply visuals whenever the panel is shown.
        if (_initialized)
        {
            ApplyTabVisuals();
        }
    }

    private void Update()
    {
        // Update only runs while this component's GameObject is active — i.e.
        // while the progression panel is shown — so keyboard input is naturally
        // gated to when this canvas is on screen. (MainMenuController ignores the
        // arrow/Enter keys while the camera is away from monitor 1, so there's no
        // double-handling.)
        if (!_initialized)
        {
            return;
        }

        if (WasNavigatePrevPressed())
        {
            MoveSelection(-1);
        }
        else if (WasNavigateNextPressed())
        {
            MoveSelection(1);
        }

        if (WasSubmitPressed())
        {
            ConfirmSelection();
        }
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _tabs = new[] { shipsTab, itemsTab };
        _contents = new[] { shipsContent, itemsContent };
        _baseLabels = new string[_tabs.Length];
        _baseColors = new Color[_tabs.Length];

        for (int i = 0; i < _tabs.Length; i++)
        {
            TMP_Text tab = _tabs[i];
            if (tab == null)
            {
                Debug.LogWarning($"{nameof(Monitor3Controller)}: tab {(Tab)i} has no TMP_Text assigned.", this);
                continue;
            }

            _baseLabels[i] = tab.text;
            _baseColors[i] = tab.color;

            // Ensure the text can receive pointer events.
            tab.raycastTarget = true;

            // Attach a lightweight proxy that forwards hover/click to us.
            Monitor3TabProxy proxy = tab.GetComponent<Monitor3TabProxy>();
            if (proxy == null)
            {
                proxy = tab.gameObject.AddComponent<Monitor3TabProxy>();
            }
            proxy.Bind(this, i);
        }

        _initialized = true;
        ApplyTabVisuals();
    }

    // ---- Selection handling --------------------------------------------

    private void SelectTab(Tab tab)
    {
        if (currentTab == tab && _initialized)
        {
            return;
        }

        currentTab = tab;

        // Tab changed — play the UI click sound (these TMP_Text objects aren't
        // Unity Buttons, so the AudioManager's auto-wiring doesn't reach them).
        ServiceLocator.Get<AudioManager>()?.PlayButtonClick();

        ApplyTabVisuals();
    }

    // Arrow-key cycling: moves the selection to the next/previous tab (wrapping),
    // turning it green immediately with a soft hover chirp.
    private void MoveSelection(int direction)
    {
        int count = _tabs.Length;
        int next = ((int)currentTab + direction) % count;
        if (next < 0)
        {
            next += count;
        }

        if ((Tab)next == currentTab)
        {
            return;
        }

        currentTab = (Tab)next;
        ServiceLocator.Get<AudioManager>()?.PlayButtonHover();
        ApplyTabVisuals();
    }

    // Enter: commits the currently focused tab (plays the click sound and
    // re-asserts the visuals / content for that tab).
    private void ConfirmSelection()
    {
        ServiceLocator.Get<AudioManager>()?.PlayButtonClick();
        ApplyTabVisuals();
    }

    private void ApplyTabVisuals()
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool isSelected = i == (int)currentTab;

            TMP_Text tab = _tabs[i];
            if (tab != null)
            {
                tab.text = isSelected
                    ? $"{selectedPrefix}{_baseLabels[i]}{selectedSuffix}"
                    : _baseLabels[i];
                tab.color = isSelected ? selectedColor : _baseColors[i];
            }

            GameObject content = _contents[i];
            if (content != null)
            {
                content.SetActive(isSelected);
            }
        }
    }

    // ---- Pointer callbacks (from Monitor3TabProxy) ---------------------

    internal void OnTabHovered(int index)
    {
        if (index < 0 || index >= _tabs.Length)
        {
            return;
        }

        // Tabs switch on click, not hover, so hovering only chirps for feedback.
        ServiceLocator.Get<AudioManager>()?.PlayButtonHover();
    }

    internal void OnTabClicked(int index)
    {
        if (index < 0 || index >= _tabs.Length)
        {
            return;
        }

        SelectTab((Tab)index);
    }

    // ---- Input helpers (mirror MainMenuController) ---------------------

    // Previous tab: left/up (or A/W).
    private static bool WasNavigatePrevPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame
            || kb.aKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.W);
#endif
    }

    // Next tab: right/down (or D/S).
    private static bool WasNavigateNextPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame
            || kb.dKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.S);
#endif
    }

    private static bool WasSubmitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }
}

/// <summary>
/// Tiny per-tab helper that forwards pointer hover/click events from a tab text
/// object back to the owning <see cref="Monitor3Controller"/>. Added
/// automatically at runtime; you do not need to attach it manually.
/// </summary>
[DisallowMultipleComponent]
public sealed class Monitor3TabProxy : MonoBehaviour,
    IPointerEnterHandler, IPointerClickHandler
{
    private Monitor3Controller _owner;
    private int _index;

    internal void Bind(Monitor3Controller owner, int index)
    {
        _owner = owner;
        _index = index;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnTabHovered(_index);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnTabClicked(_index);
        }
    }
}
