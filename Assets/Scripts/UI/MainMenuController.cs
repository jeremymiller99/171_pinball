// Created with Claude Code (Opus 4.8) by JJ on 2026-06-03: text-based main menu navigation.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drives the new text-based main menu (Play / Settings / Profile).
/// The selected option is rendered as "> Label <" in green; the others
/// use their default label/color. Selection changes on hover or arrow
/// keys; clicking (or Enter/Submit) confirms the current selection.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    private enum MenuOption
    {
        Play = 0,
        Settings = 1,
        Profile = 2
    }

    // Which camera point we're currently at. The three main-menu buttons are
    // only interactive at Main (camera point 1); each other point owns its
    // own canvas/controls.
    private enum MenuLocation
    {
        Main = 0,     // Camera point 1 — monitor 1 canvas (these buttons).
        Play = 1,     // Camera point 2 — monitor 2 canvas (not yet made).
        Settings = 2  // Camera point 3 — settings canvas / panel.
    }

    [Header("Menu options (assign the TMP_Text objects)")]
    [Tooltip("The 'Play' text object.")]
    [SerializeField] private TMP_Text playText;

    [Tooltip("The 'Settings' text object.")]
    [SerializeField] private TMP_Text settingsText;

    [Tooltip("The 'Profile' text object.")]
    [SerializeField] private TMP_Text profileText;

    [Header("Selection styling")]
    [Tooltip("Color applied to the currently selected option.")]
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.9f, 0.4f, 1f);

    [Tooltip("Prefix added in front of the selected option's label.")]
    [SerializeField] private string selectedPrefix = "> ";

    [Tooltip("Suffix added after the selected option's label.")]
    [SerializeField] private string selectedSuffix = " <";

    [Header("Camera")]
    [Tooltip("Camera rig that lerps to the second point when Play is selected.")]
    [SerializeField] private CameraLerpBetweenPoints cameraLerp;

    [Header("Settings")]
    [Tooltip("Settings panel to show while the camera is at the third point. " +
             "Drag the panel instance from the scene here.")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Runtime (debug)")]
    [SerializeField] private MenuOption currentOption = MenuOption.Play;

    // Where the camera currently is. Starts at Main (point 1).
    private MenuLocation _location = MenuLocation.Main;

    // Per-option cached data so we can restore the unselected look.
    private TMP_Text[] _texts;
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
            ApplySelectionVisuals();
        }
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _texts = new[] { playText, settingsText, profileText };
        _baseLabels = new string[_texts.Length];
        _baseColors = new Color[_texts.Length];

        for (int i = 0; i < _texts.Length; i++)
        {
            TMP_Text text = _texts[i];
            if (text == null)
            {
                Debug.LogWarning($"{nameof(MainMenuController)}: menu option {(MenuOption)i} has no TMP_Text assigned.", this);
                continue;
            }

            _baseLabels[i] = text.text;
            _baseColors[i] = text.color;

            // Ensure the text can receive pointer events.
            text.raycastTarget = true;

            // Attach a lightweight proxy that forwards hover/click to us.
            MainMenuItemPointerProxy proxy = text.GetComponent<MainMenuItemPointerProxy>();
            if (proxy == null)
            {
                proxy = text.gameObject.AddComponent<MainMenuItemPointerProxy>();
            }
            proxy.Bind(this, i);
        }

        _initialized = true;
        ApplySelectionVisuals();
    }

    private void Update()
    {
        // Away from the main camera point, the three buttons are inert; only
        // a back/cancel press (to return to Main) is honored here.
        if (_location != MenuLocation.Main)
        {
            if (WasCancelPressed())
            {
                ReturnToMain();
            }
            return;
        }

        if (WasNavigateUpPressed())
        {
            MoveSelection(-1);
        }
        else if (WasNavigateDownPressed())
        {
            MoveSelection(1);
        }

        if (WasSubmitPressed())
        {
            ConfirmSelection();
        }
    }

    // ---- Selection handling --------------------------------------------

    private void MoveSelection(int direction)
    {
        int count = _texts.Length;
        int next = ((int)currentOption + direction) % count;
        if (next < 0)
        {
            next += count;
        }

        SetSelection((MenuOption)next);
    }

    private void SetSelection(MenuOption option)
    {
        if (currentOption == option && _initialized)
        {
            return;
        }

        currentOption = option;
        ApplySelectionVisuals();
    }

    private void ApplySelectionVisuals()
    {
        for (int i = 0; i < _texts.Length; i++)
        {
            TMP_Text text = _texts[i];
            if (text == null)
            {
                continue;
            }

            bool isSelected = i == (int)currentOption;

            if (isSelected)
            {
                text.text = $"{selectedPrefix}{_baseLabels[i]}{selectedSuffix}";
                text.color = selectedColor;
            }
            else
            {
                text.text = _baseLabels[i];
                text.color = _baseColors[i];
            }
        }
    }

    private void ConfirmSelection()
    {
        switch (currentOption)
        {
            case MenuOption.Play:
                GoToPlay();
                break;
            case MenuOption.Settings:
                GoToSettings();
                break;
            case MenuOption.Profile:
                GoToProfile();
                break;
        }
    }

    // ---- Pointer callbacks (from MainMenuItemPointerProxy) --------------

    internal void OnItemHovered(int index)
    {
        if (_location != MenuLocation.Main || index < 0 || index >= _texts.Length)
        {
            return;
        }

        SetSelection((MenuOption)index);
    }

    internal void OnItemClicked(int index)
    {
        if (_location != MenuLocation.Main || index < 0 || index >= _texts.Length)
        {
            return;
        }

        SetSelection((MenuOption)index);
        ConfirmSelection();
    }

    // ---- Option actions (stubbed for now) ------------------------------

    private void GoToPlay()
    {
        Debug.Log("[MainMenu] Play selected.");

        // Leave Main: lock the buttons; monitor 2's canvas owns input there.
        _location = MenuLocation.Play;
        SetMainInteractable(false);

        if (cameraLerp != null)
        {
            cameraLerp.GoToSecond();
        }
        else
        {
            Debug.LogWarning($"{nameof(MainMenuController)}: no {nameof(CameraLerpBetweenPoints)} assigned; cannot move camera.", this);
        }
    }

    private void GoToSettings()
    {
        Debug.Log("[MainMenu] Settings selected.");

        // Leave Main: lock the buttons; the settings canvas owns input there.
        _location = MenuLocation.Settings;
        SetMainInteractable(false);

        // Keep the panel hidden until the camera arrives at point 3.
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (cameraLerp != null)
        {
            cameraLerp.GoToThird();
            if (settingsPanel != null)
            {
                StartCoroutine(ShowPanelWhenCameraArrives());
            }
        }
        else
        {
            Debug.LogWarning($"{nameof(MainMenuController)}: no {nameof(CameraLerpBetweenPoints)} assigned; cannot move camera.", this);
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Return to camera point 1 and re-enable the main menu buttons. Hook this
    /// to a "Back" button on the Play / Settings canvases (or it fires on the
    /// cancel key, e.g. Escape).
    /// </summary>
    public void ReturnToMain()
    {
        if (_location == MenuLocation.Main)
        {
            return;
        }

        // Hide the settings panel immediately so it isn't visible during the
        // move back to point 1.
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (cameraLerp != null)
        {
            cameraLerp.GoToDefault();
        }

        _location = MenuLocation.Main;
        SetMainInteractable(true);
        ApplySelectionVisuals();
    }

    // Enables/disables pointer raycasting on the three menu texts so they can
    // only be clicked while we're at the main camera point.
    private void SetMainInteractable(bool interactable)
    {
        if (_texts == null)
        {
            return;
        }

        foreach (TMP_Text text in _texts)
        {
            if (text != null)
            {
                text.raycastTarget = interactable;
            }
        }
    }

    private System.Collections.IEnumerator ShowPanelWhenCameraArrives()
    {
        // Let the transition kick off, then wait for it to finish.
        yield return null;
        while (cameraLerp.IsMoving)
        {
            yield return null;
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    private void GoToProfile()
    {
        Debug.Log("[MainMenu] Profile selected.");
    }

    // ---- Input helpers -------------------------------------------------

    private static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool WasNavigateUpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame))
        {
            return true;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
#endif
    }

    private static bool WasNavigateDownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame))
        {
            return true;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
#endif
    }

    private static bool WasSubmitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            return true;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }
}

/// <summary>
/// Tiny per-item helper that forwards pointer hover/click events from a
/// menu text object back to the owning <see cref="MainMenuController"/>.
/// Added automatically at runtime; you do not need to attach it manually.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuItemPointerProxy : MonoBehaviour,
    IPointerEnterHandler, IPointerClickHandler
{
    private MainMenuController _owner;
    private int _index;

    internal void Bind(MainMenuController owner, int index)
    {
        _owner = owner;
        _index = index;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnItemHovered(_index);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnItemClicked(_index);
        }
    }
}
