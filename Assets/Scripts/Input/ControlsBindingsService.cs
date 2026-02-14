using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Centralized, persistent bindings for keyboard/mouse controls.
/// This is a minimal-refactor alternative to full InputAction maps.
/// </summary>
[DisallowMultipleComponent]
public sealed class ControlsBindingsService : MonoBehaviour
{
    private const string PlayerPrefsKey = "ControlsBindings_v1";

    public static ControlsBindingsService Instance { get; private set; }

    /// <summary>
    /// Fired whenever bindings are changed (rebind/reset/load).
    /// </summary>
    public static event Action BindingsChanged;

    private readonly Dictionary<ControlAction, ControlsBinding> _bindings = new Dictionary<ControlAction, ControlsBinding>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject(nameof(ControlsBindingsService));
        DontDestroyOnLoad(go);
        go.AddComponent<ControlsBindingsService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadOrDefaults();
    }

    public static ControlsBinding GetBinding(ControlAction action)
    {
        if (Instance == null)
            return GetDefaultBinding(action);

        if (Instance._bindings.TryGetValue(action, out var b))
            return b;

        return GetDefaultBinding(action);
    }

    public static void SetBinding(ControlAction action, ControlsBinding binding)
    {
        if (Instance == null) return;

        Instance._bindings[action] = binding;
        Instance.Save();
        BindingsChanged?.Invoke();
    }

    public static void ResetToDefaults()
    {
        if (Instance == null) return;
        Instance.SetDefaults();
        Instance.Save();
        BindingsChanged?.Invoke();
    }

    public static string GetDisplayString(ControlAction action)
    {
        return GetBinding(action).ToDisplayString();
    }

    public static bool IsHeld(ControlAction action)
    {
        return ReadBindingHeld(GetBinding(action));
    }

    public static bool WasPressedThisFrame(ControlAction action)
    {
        return ReadBindingPressedThisFrame(GetBinding(action));
    }

    public static bool WasReleasedThisFrame(ControlAction action)
    {
        return ReadBindingReleasedThisFrame(GetBinding(action));
    }

    private void LoadOrDefaults()
    {
        SetDefaults();

        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            return;

        string json = PlayerPrefs.GetString(PlayerPrefsKey, "");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            var save = JsonUtility.FromJson<ControlsBindingsSave>(json);
            if (save?.entries == null)
                return;

            for (int i = 0; i < save.entries.Count; i++)
            {
                var e = save.entries[i];
                _bindings[e.action] = e.binding;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{nameof(ControlsBindingsService)}: Failed to load bindings JSON. Using defaults. {ex.Message}", this);
            SetDefaults();
        }
    }

    private void Save()
    {
        var save = new ControlsBindingsSave
        {
            version = 1,
            entries = new List<ControlsBindingsEntry>()
        };

        foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
        {
            save.entries.Add(new ControlsBindingsEntry
            {
                action = action,
                binding = GetBinding(action),
            });
        }

        string json = JsonUtility.ToJson(save);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    private void SetDefaults()
    {
        _bindings.Clear();
        foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
        {
            _bindings[action] = GetDefaultBinding(action);
        }
    }

    private static ControlsBinding GetDefaultBinding(ControlAction action)
    {
        switch (action)
        {
            case ControlAction.LeftFlipper:
                return new ControlsBinding { key = "LeftArrow", mouseButton = -1 };
            case ControlAction.RightFlipper:
                return new ControlsBinding { key = "RightArrow", mouseButton = -1 };
            case ControlAction.Launch:
                return new ControlsBinding { key = "DownArrow", mouseButton = -1 };
            case ControlAction.ToggleDebugPanel:
                return new ControlsBinding { key = "Backquote", mouseButton = -1 };
            default:
                return ControlsBinding.None;
        }
    }

    private static bool ReadBindingHeld(ControlsBinding binding)
    {
        if (ReadKeyboardHeld(binding.key))
            return true;
        if (ReadMouseHeld(binding.mouseButton))
            return true;
        return false;
    }

    private static bool ReadBindingPressedThisFrame(ControlsBinding binding)
    {
        if (ReadKeyboardPressedThisFrame(binding.key))
            return true;
        if (ReadMousePressedThisFrame(binding.mouseButton))
            return true;
        return false;
    }

    private static bool ReadBindingReleasedThisFrame(ControlsBinding binding)
    {
        if (ReadKeyboardReleasedThisFrame(binding.key))
            return true;
        if (ReadMouseReleasedThisFrame(binding.mouseButton))
            return true;
        return false;
    }

    private static bool ReadMouseHeld(int mouseButton)
    {
        if (mouseButton < 0) return false;

#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        switch (mouseButton)
        {
            case 0: return m.leftButton.isPressed;
            case 1: return m.rightButton.isPressed;
            case 2: return m.middleButton.isPressed;
            default: return false;
        }
#else
        if (mouseButton < 3)
            return Input.GetMouseButton(mouseButton);
        return false;
#endif
    }

    private static bool ReadMousePressedThisFrame(int mouseButton)
    {
        if (mouseButton < 0) return false;

#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        switch (mouseButton)
        {
            case 0: return m.leftButton.wasPressedThisFrame;
            case 1: return m.rightButton.wasPressedThisFrame;
            case 2: return m.middleButton.wasPressedThisFrame;
            default: return false;
        }
#else
        if (mouseButton < 3)
            return Input.GetMouseButtonDown(mouseButton);
        return false;
#endif
    }

    private static bool ReadMouseReleasedThisFrame(int mouseButton)
    {
        if (mouseButton < 0) return false;

#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return false;
        switch (mouseButton)
        {
            case 0: return m.leftButton.wasReleasedThisFrame;
            case 1: return m.rightButton.wasReleasedThisFrame;
            case 2: return m.middleButton.wasReleasedThisFrame;
            default: return false;
        }
#else
        if (mouseButton < 3)
            return Input.GetMouseButtonUp(mouseButton);
        return false;
#endif
    }

    private static bool ReadKeyboardHeld(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return false;

#if ENABLE_INPUT_SYSTEM
        if (!TryParseInputSystemKey(keyName, out var k)) return false;
        var kb = Keyboard.current;
        return kb != null && kb[k].isPressed;
#else
        if (!TryParseLegacyKeyCode(keyName, out var kc)) return false;
        return Input.GetKey(kc);
#endif
    }

    private static bool ReadKeyboardPressedThisFrame(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return false;

#if ENABLE_INPUT_SYSTEM
        if (!TryParseInputSystemKey(keyName, out var k)) return false;
        var kb = Keyboard.current;
        return kb != null && kb[k].wasPressedThisFrame;
#else
        if (!TryParseLegacyKeyCode(keyName, out var kc)) return false;
        return Input.GetKeyDown(kc);
#endif
    }

    private static bool ReadKeyboardReleasedThisFrame(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return false;

#if ENABLE_INPUT_SYSTEM
        if (!TryParseInputSystemKey(keyName, out var k)) return false;
        var kb = Keyboard.current;
        return kb != null && kb[k].wasReleasedThisFrame;
#else
        if (!TryParseLegacyKeyCode(keyName, out var kc)) return false;
        return Input.GetKeyUp(kc);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryParseInputSystemKey(string keyName, out Key key)
    {
        // A few common alias differences between Input System Key and legacy KeyCode naming.
        if (string.Equals(keyName, "BackQuote", StringComparison.OrdinalIgnoreCase))
            keyName = "Backquote";
        if (string.Equals(keyName, "LeftControl", StringComparison.OrdinalIgnoreCase))
            keyName = "LeftCtrl";
        if (string.Equals(keyName, "RightControl", StringComparison.OrdinalIgnoreCase))
            keyName = "RightCtrl";

        return Enum.TryParse(keyName, out key);
    }
#else
    private static bool TryParseLegacyKeyCode(string keyName, out KeyCode keyCode)
    {
        // A few common alias differences between Input System Key and legacy KeyCode naming.
        if (string.Equals(keyName, "Backquote", StringComparison.OrdinalIgnoreCase))
            keyName = "BackQuote";
        if (string.Equals(keyName, "LeftCtrl", StringComparison.OrdinalIgnoreCase))
            keyName = "LeftControl";
        if (string.Equals(keyName, "RightCtrl", StringComparison.OrdinalIgnoreCase))
            keyName = "RightControl";

        return Enum.TryParse(keyName, out keyCode);
    }
#endif

    [Serializable]
    private sealed class ControlsBindingsSave
    {
        public int version;
        public List<ControlsBindingsEntry> entries;
    }

    [Serializable]
    private struct ControlsBindingsEntry
    {
        public ControlAction action;
        public ControlsBinding binding;
    }
}

