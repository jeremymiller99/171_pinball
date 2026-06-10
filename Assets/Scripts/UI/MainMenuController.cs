// Created with Claude Code (Opus 4.8) by JJ on 2026-06-03: text-based main menu navigation.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Drives the new text-based main menu (Play / Settings / Progression).
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
        Progression = 2
    }

    // Which camera point we're currently at. The three main-menu buttons are
    // only interactive at Main (camera point 1); each other point owns its
    // own canvas/controls.
    private enum MenuLocation
    {
        Main = 0,        // Camera point 1 — monitor 1 canvas (these buttons).
        Play = 1,        // Camera point 2 — monitor 2 canvas.
        Progression = 2, // Camera point 3 — progression canvas / panel.
        Settings = 3     // Camera point 4 — settings canvas / panel.
    }

    [Header("Menu options (assign the TMP_Text objects)")]
    [Tooltip("The 'Play' text object.")]
    [SerializeField] private TMP_Text playText;

    [Tooltip("The 'Settings' text object.")]
    [SerializeField] private TMP_Text settingsText;

    [Tooltip("The 'Progression' text object.")]
    [FormerlySerializedAs("profileText")]
    [SerializeField] private TMP_Text progressionText;

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

    [Header("Monitor 2")]
    [Tooltip("Controller for the monitor-2 canvases (playfield / mission / ship / start). " +
             "Activated once the camera arrives at the Play point.")]
    [SerializeField] private Monitor2Controller monitor2Controller;

    [Header("Settings")]
    [Tooltip("Settings panel to show while the camera is at the fourth point. " +
             "Drag the panel instance from the scene here.")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Progression")]
    [Tooltip("Progression panel to show while the camera is at the third point. " +
             "Drag the panel instance from the scene here.")]
    [SerializeField] private GameObject progressionPanel;

    [Header("Quit")]
    [Tooltip("Optional 'Quit' text object on the monitor-1 canvas. Clicking it exits " +
             "the game. Auto-found by the GameObject name \"Quit\" if left blank.")]
    [SerializeField] private TMP_Text quitText;

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

    private void Start()
    {
        // Start the looping screen-hum ambience for the menu. Done in Start (not
        // Awake/OnEnable) so the AudioManager has finished registering itself.
        ServiceLocator.Get<AudioManager>()?.StartHummingSound();
    }

    private void OnDestroy()
    {
        // Stop the hum when leaving the menu scene.
        ServiceLocator.Get<AudioManager>()?.StopHummingSound();
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _texts = new[] { playText, settingsText, progressionText };
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

        WireQuitItem();

        _initialized = true;
        ApplySelectionVisuals();
    }

    // The Quit text isn't one of the navigable options (it's not in _texts), so it
    // gets its own pointer proxy and click handling. Assigned in the inspector or,
    // failing that, located by the GameObject name "Quit" in the scene.
    private void WireQuitItem()
    {
        if (quitText == null)
        {
            quitText = FindQuitText();
        }

        if (quitText == null)
        {
            return;
        }

        // Match the other items: clickable only while at the main camera point.
        quitText.raycastTarget = _location == MenuLocation.Main;

        MainMenuQuitPointerProxy proxy = quitText.GetComponent<MainMenuQuitPointerProxy>();
        if (proxy == null)
        {
            proxy = quitText.gameObject.AddComponent<MainMenuQuitPointerProxy>();
        }
        proxy.Bind(this);
    }

    private static TMP_Text FindQuitText()
    {
        TMP_Text[] all = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            TMP_Text text = all[i];
            if (text != null && text.gameObject.name == "Quit")
            {
                return text;
            }
        }
        return null;
    }

    private void Update()
    {
        // Dev/reset shortcut: R wipes the active profile (banked points, unlocks,
        // stats, tutorial flags) back to a fresh profile and re-saves over the
        // slot file. Honored anywhere in the main-menu scene, regardless of which
        // camera point we're at or which option is selected.
        if (WasResetPressed())
        {
            ResetActiveProfile();
            return;
        }

        // Dev shortcut: the tilde/backquote key jumps back to the legacy
        // (original) main-menu scene. Honored anywhere in this scene.
        if (WasReturnToLegacyMenuPressed())
        {
            ReturnToLegacyMenu();
            return;
        }

        // Away from the main camera point, the three buttons are inert; only
        // a back/cancel press (to return to Main) is honored here. While monitor 2
        // is active it owns Escape (and calls ReturnToMain itself), so don't also
        // handle cancel here or we'd return twice.
        if (_location != MenuLocation.Main)
        {
            bool monitor2OwnsInput = _location == MenuLocation.Play
                && monitor2Controller != null
                && monitor2Controller.IsActive;

            if (!monitor2OwnsInput && WasCancelPressed())
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

        // Selection changed (hover or keyboard nav) — play the UI hover sound.
        ServiceLocator.Get<AudioManager>()?.PlayButtonHover();

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
            case MenuOption.Progression:
                GoToProgression();
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

    // Fired on pointer-down for immediate click feedback. OnPointerClick only
    // fires on release, which makes the sound trail the press; play it here so
    // the click sound lands the instant the button is pressed.
    internal void OnItemPressed(int index)
    {
        if (_location != MenuLocation.Main || index < 0 || index >= _texts.Length)
        {
            return;
        }

        // These menu items are TMP_Text objects (not Unity Buttons), so the
        // AudioManager's automatic button-audio wiring doesn't reach them; play
        // the UI click sound ourselves.
        ServiceLocator.Get<AudioManager>()?.PlayButtonClick();
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

    // Pointer-down on Quit: play the click sound immediately (see OnItemPressed).
    internal void OnQuitPressed()
    {
        if (_location != MenuLocation.Main)
        {
            return;
        }

        ServiceLocator.Get<AudioManager>()?.PlayButtonClick();
    }

    internal void OnQuitClicked()
    {
        if (_location != MenuLocation.Main)
        {
            return;
        }

        QuitGame();
    }

    // ---- Option actions (stubbed for now) ------------------------------

    private void QuitGame()
    {
        Debug.Log("[MainMenu] Quit selected — exiting game.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void GoToPlay()
    {
        Debug.Log("[MainMenu] Play selected.");

        // Leave Main: lock the buttons; monitor 2's canvas owns input there.
        _location = MenuLocation.Play;
        SetMainInteractable(false);

        if (cameraLerp != null)
        {
            cameraLerp.GoToSecond();
            if (monitor2Controller != null)
            {
                StartCoroutine(ActivateMonitor2WhenCameraArrives());
            }
        }
        else
        {
            Debug.LogWarning($"{nameof(MainMenuController)}: no {nameof(CameraLerpBetweenPoints)} assigned; cannot move camera.", this);
            if (monitor2Controller != null)
            {
                monitor2Controller.Activate();
            }
        }
    }

    private System.Collections.IEnumerator ActivateMonitor2WhenCameraArrives()
    {
        // Let the transition kick off, then wait for it to finish before handing
        // input to monitor 2.
        yield return null;
        while (cameraLerp.IsMoving)
        {
            yield return null;
        }

        // The user may have already backed out during the move.
        if (_location == MenuLocation.Play && monitor2Controller != null)
        {
            monitor2Controller.Activate();
        }
    }

    private void GoToSettings()
    {
        Debug.Log("[MainMenu] Settings selected.");

        // Leave Main: lock the buttons; the settings canvas owns input there.
        _location = MenuLocation.Settings;
        SetMainInteractable(false);

        // Keep the panel hidden until the camera arrives at point 4.
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (cameraLerp != null)
        {
            cameraLerp.GoToFourth();
            if (settingsPanel != null)
            {
                StartCoroutine(ShowPanelWhenCameraArrives(settingsPanel, MenuLocation.Settings));
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

    private void GoToProgression()
    {
        Debug.Log("[MainMenu] Progression selected.");

        // Leave Main: lock the buttons; the progression canvas owns input there.
        _location = MenuLocation.Progression;
        SetMainInteractable(false);

        // Keep the panel hidden until the camera arrives at point 3.
        if (progressionPanel != null)
        {
            progressionPanel.SetActive(false);
        }

        if (cameraLerp != null)
        {
            cameraLerp.GoToThird();
            if (progressionPanel != null)
            {
                StartCoroutine(ShowPanelWhenCameraArrives(progressionPanel, MenuLocation.Progression));
            }
        }
        else
        {
            Debug.LogWarning($"{nameof(MainMenuController)}: no {nameof(CameraLerpBetweenPoints)} assigned; cannot move camera.", this);
            if (progressionPanel != null)
            {
                progressionPanel.SetActive(true);
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

        // Hand input back from monitor 2 (no-op if it wasn't active).
        if (monitor2Controller != null)
        {
            monitor2Controller.Deactivate();
        }

        // Hide the settings / progression panels immediately so neither is
        // visible during the move back to point 1.
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        if (progressionPanel != null)
        {
            progressionPanel.SetActive(false);
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

        if (quitText != null)
        {
            quitText.raycastTarget = interactable;
        }
    }

    private System.Collections.IEnumerator ShowPanelWhenCameraArrives(GameObject panel, MenuLocation forLocation)
    {
        // Let the transition kick off, then wait for it to finish.
        yield return null;
        while (cameraLerp.IsMoving)
        {
            yield return null;
        }

        // The user may have backed out (or switched options) during the move;
        // only reveal the panel if we're still headed for the same location.
        if (panel != null && _location == forLocation)
        {
            panel.SetActive(true);
        }
    }

    // Wipe the active profile slot back to a brand-new profile and persist it,
    // effectively deleting the existing save and starting fresh.
    private void ResetActiveProfile()
    {
        ProfileSlotId slot = ProfileService.GetActiveSlot();
        ProfileService.ResetSlot(slot);
        Debug.Log($"[MainMenu] Reset profile slot {slot} — save wiped, starting fresh.");
    }

    // Load the legacy main-menu scene (the original "MainMenu" scene), fading
    // out first to match the rest of the scene transitions.
    private void ReturnToLegacyMenu()
    {
        Debug.Log("[MainMenu] Returning to legacy main menu.");
        SceneFader.Instance.FadeAndLoadScene("MainMenu");
    }

    // ---- Input helpers -------------------------------------------------

    private static bool WasReturnToLegacyMenuPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && kb.backquoteKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.BackQuote);
#endif
    }

    private static bool WasResetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && kb.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

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
    IPointerEnterHandler, IPointerDownHandler, IPointerClickHandler
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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnItemPressed(_index);
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

/// <summary>
/// Pointer helper for the menu's "Quit" text. Unlike the navigable options it
/// has no selection index — a click just exits the game via the owning
/// <see cref="MainMenuController"/>. Added automatically at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuQuitPointerProxy : MonoBehaviour,
    IPointerDownHandler, IPointerClickHandler
{
    private MainMenuController _owner;

    internal void Bind(MainMenuController owner)
    {
        _owner = owner;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnQuitPressed();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_owner != null)
        {
            _owner.OnQuitClicked();
        }
    }
}
