using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runtime-built nested Controls menu under the MainMenu Settings panel.
/// Lets the player rebind keyboard/mouse bindings and persists via ControlsBindingsService.
/// </summary>
[DisallowMultipleComponent]
public sealed class ControlsMenuController : MonoBehaviour
{
    [Header("Injected")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Runtime (debug)")]
    [SerializeField] private bool built;
    [SerializeField] private bool showingControls;
    [SerializeField] private string listeningActionName;

    private GameObject _homeRoot;
    private GameObject _controlsRoot;
    private TMP_Text _listeningHintText;
    private readonly Dictionary<ControlAction, TMP_Text> _actionButtonTexts = new Dictionary<ControlAction, TMP_Text>();

    private ControlAction? _listeningFor;
    private int _listenStartFrame;

    public bool IsListening => _listeningFor.HasValue;

    public static bool IsRebindingInProgress()
    {
#if UNITY_2022_2_OR_NEWER
        var c = FindFirstObjectByType<ControlsMenuController>(FindObjectsInactive.Include);
#else
        var c = FindObjectOfType<ControlsMenuController>(includeInactive: true);
#endif
        return c != null && c.IsListening;
    }

    public static ControlsMenuController InstallInto(GameObject settingsPanel)
    {
        if (settingsPanel == null) return null;

        var existing = settingsPanel.GetComponentInChildren<ControlsMenuController>(includeInactive: true);
        if (existing != null)
        {
            existing.settingsPanel = settingsPanel;
            existing.BuildIfNeeded();
            return existing;
        }

        var go = new GameObject("ControlsMenuController");
        go.transform.SetParent(settingsPanel.transform, false);
        var controller = go.AddComponent<ControlsMenuController>();
        controller.settingsPanel = settingsPanel;
        controller.BuildIfNeeded();
        return controller;
    }

    private void OnEnable()
    {
        ControlsBindingsService.BindingsChanged += RefreshBindingTexts;
    }

    private void OnDisable()
    {
        ControlsBindingsService.BindingsChanged -= RefreshBindingTexts;
    }

    private void Update()
    {
        if (!_listeningFor.HasValue)
            return;

        listeningActionName = _listeningFor.Value.ToString();

        // Avoid immediately binding the click that activated the Rebind button.
        if (Time.frameCount <= _listenStartFrame)
            return;

        if (WasEscapePressedThisFrame())
        {
            StopListening(cancelled: true);
            return;
        }

        // Mouse takes priority so players can bind to mouse buttons easily.
        if (TryGetMousePressedThisFrame(out int mouseBtn))
        {
            ApplyBinding(_listeningFor.Value, new ControlsBinding { key = "", mouseButton = mouseBtn });
            return;
        }

        if (TryGetKeyPressedThisFrame(out string keyName))
        {
            // Don't allow binding Escape in v1 (kept for menu back).
            if (!string.Equals(keyName, "Escape", StringComparison.OrdinalIgnoreCase))
            {
                ApplyBinding(_listeningFor.Value, new ControlsBinding { key = keyName, mouseButton = -1 });
                return;
            }
        }
    }

    private void BuildIfNeeded()
    {
        if (built) return;
        built = true;

        if (settingsPanel == null)
        {
            Debug.LogWarning($"{nameof(ControlsMenuController)}: Missing Settings panel.", this);
            return;
        }

        HidePlaceholderComingSoonText();

        // Simple + effective: show keybinding list by default inside Settings.
        // (No extra "Controls" submenu click.)
        _homeRoot = CreatePanelRoot(settingsPanel.transform, "SettingsHomeRoot");
        _controlsRoot = CreatePanelRoot(settingsPanel.transform, "ControlsRoot");
        if (_homeRoot != null) _homeRoot.SetActive(false);
        if (_controlsRoot != null) _controlsRoot.SetActive(true);

        // Controls panel content
        CreateBodyText(_controlsRoot.transform, "Controls_Hint", "Click an action to rebind, then press a key or mouse button (Esc cancels).", 22, TextAlignmentOptions.Center);
        _listeningHintText = CreateBodyText(_controlsRoot.transform, "Controls_ListeningHint", "", 20, TextAlignmentOptions.Center);
        CreateSpacer(_controlsRoot.transform, 8f);

        var listRoot = CreateListRoot(_controlsRoot.transform, "Controls_List");

        // Simple + effective: one big button per action.
        _actionButtonTexts.Clear();
        foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
        {
            CreateActionRebindButton(listRoot, action);
        }

        CreateSpacer(_controlsRoot.transform, 16f);

        // Footer buttons
        var footer = CreateFooterRoot(_controlsRoot.transform, "Controls_Footer");
        CreateButton(footer.transform, "Controls_Reset", "Reset Defaults", new Color(0.65f, 0.2f, 0.2f, 1f), ControlsBindingsService.ResetToDefaults);

        RefreshBindingTexts();
    }

    private void ShowControls(bool show)
    {
        showingControls = show;
        if (_homeRoot != null) _homeRoot.SetActive(!show);
        if (_controlsRoot != null) _controlsRoot.SetActive(show);

        if (!show)
            StopListening(cancelled: true);
    }

    private void CreateActionRebindButton(Transform parent, ControlAction action)
    {
        var btn = CreateWideButton(parent, $"Rebind_{action}", "", new Color(0.2f, 0.55f, 0.9f, 1f), () => StartListening(action));
        var txt = btn.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (txt != null)
        {
            _actionButtonTexts[action] = txt;
        }
    }

    private void StartListening(ControlAction action)
    {
        _listeningFor = action;
        _listenStartFrame = Time.frameCount;
        listeningActionName = action.ToString();

        if (_listeningHintText != null)
            _listeningHintText.text = $"Press a key or mouse button for {PrettyActionName(action)} (Esc to cancel)";

        // Update button text to show it's active.
        if (_actionButtonTexts.TryGetValue(action, out var txt) && txt != null)
            txt.text = $"{PrettyActionName(action)}: [press a key]";
    }

    private void StopListening(bool cancelled)
    {
        _listeningFor = null;
        listeningActionName = "";
        if (_listeningHintText != null)
            _listeningHintText.text = "";
    }

    private void ApplyBinding(ControlAction action, ControlsBinding binding)
    {
        ControlsBindingsService.SetBinding(action, binding);
        StopListening(cancelled: false);
        RefreshBindingTexts();
    }

    private void RefreshBindingTexts()
    {
        foreach (var kvp in _actionButtonTexts)
        {
            if (kvp.Value == null) continue;
            kvp.Value.text = $"{PrettyActionName(kvp.Key)}: {ControlsBindingsService.GetDisplayString(kvp.Key)}";
        }
    }

    private void HidePlaceholderComingSoonText()
    {
        // Settings panel currently has placeholder text "Coming soon". Hide it if present.
        var texts = settingsPanel.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t != null && string.Equals(t.text?.Trim(), "Coming soon", StringComparison.OrdinalIgnoreCase))
            {
                t.gameObject.SetActive(false);
            }
        }
    }

    private static string PrettyActionName(ControlAction action)
    {
        switch (action)
        {
            case ControlAction.LeftFlipper: return "Left Flipper";
            case ControlAction.RightFlipper: return "Right Flipper";
            case ControlAction.ToggleDebugPanel: return "Toggle Debug Panel";
            case ControlAction.Launch: return "Launch";
            default: return action.ToString();
        }
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool TryGetMousePressedThisFrame(out int mouseBtn)
    {
        mouseBtn = -1;
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        if (m.leftButton.wasPressedThisFrame) { mouseBtn = 0; return true; }
        if (m.rightButton.wasPressedThisFrame) { mouseBtn = 1; return true; }
        if (m.middleButton.wasPressedThisFrame) { mouseBtn = 2; return true; }
        return false;
#else
        if (Input.GetMouseButtonDown(0)) { mouseBtn = 0; return true; }
        if (Input.GetMouseButtonDown(1)) { mouseBtn = 1; return true; }
        if (Input.GetMouseButtonDown(2)) { mouseBtn = 2; return true; }
        return false;
#endif
    }

    private static bool TryGetKeyPressedThisFrame(out string keyName)
    {
        keyName = null;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;

        // Iterate all keys and pick the first pressed this frame.
        // (If multiple keys are pressed, this is deterministic enough for UI.)
        var keys = kb.allKeys;
        for (int i = 0; i < keys.Count; i++)
        {
            var kc = keys[i];
            if (kc != null && kc.wasPressedThisFrame)
            {
                keyName = kc.keyCode.ToString();
                return true;
            }
        }
        return false;
#else
        if (!Input.anyKeyDown) return false;
        foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(kc))
            {
                keyName = kc.ToString();
                return true;
            }
        }
        return false;
#endif
    }

    private static GameObject CreatePanelRoot(Transform parent, string name)
    {
        // Fill most of the Settings panel so it doesn't collapse to a tiny centered list.
        var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(140f, 130f);
        rt.offsetMax = new Vector2(-140f, -130f);

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 12f;
        vlg.padding = new RectOffset(40, 40, 30, 30);

        return go;
    }

    private static Transform CreateListRoot(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 10f;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go.transform;
    }

    private static GameObject CreateFooterRoot(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);

        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 30f;

        return go;
    }

    private static void CreateSpacer(Transform parent, float minHeight)
    {
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().minHeight = minHeight;
    }

    private static TMP_Text CreateHeaderText(Transform parent, string name, string text)
    {
        return CreateBodyText(parent, name, text, 48, TextAlignmentOptions.Center, FontStyles.Bold);
    }

    private static TMP_Text CreateBodyText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = align;
        t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        // Non-interactive text should never block button clicks.
        t.raycastTarget = false;

        return t;
    }

    private static Button CreateButton(Transform parent, string name, string label, Color bgColor, Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220f, 60f);

        var img = go.GetComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txt = CreateBodyText(go.transform, "Label", label, 26, TextAlignmentOptions.Center, FontStyles.Bold);
        var txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        return btn;
    }

    private static Button CreateWideButton(Transform parent, string name, string label, Color bgColor, Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 64f;
        le.preferredHeight = 70f;

        var img = go.GetComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txt = CreateBodyText(go.transform, "Label", label, 26, TextAlignmentOptions.Left, FontStyles.Bold);
        var txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(18f, 8f);
        txtRt.offsetMax = new Vector2(-18f, -8f);

        return btn;
    }
}

