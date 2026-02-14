using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Toggle a debug panel with the tilde/backquote key (` / ~).
/// Put this on an always-active object (NOT the panel itself), then assign the panel root.
/// </summary>
[DisallowMultipleComponent]
public sealed class DebugPanelToggleHotkey : MonoBehaviour
{
    [SerializeField] private GameObject debugPanelRoot;

    private bool _warnedMissingPanel;

    private void Awake()
    {
        // Convenience auto-hook if the panel is currently active in the scene.
        if (debugPanelRoot == null)
            debugPanelRoot = GameObject.Find("Debug Panel");
    }

    private void Update()
    {
        if (!WasTogglePressed())
            return;

        if (debugPanelRoot == null)
        {
            if (!_warnedMissingPanel)
            {
                _warnedMissingPanel = true;
                Debug.LogWarning($"{nameof(DebugPanelToggleHotkey)}: Assign Debug Panel root (or name it \"Debug Panel\").");
            }
            return;
        }

        debugPanelRoot.SetActive(!debugPanelRoot.activeSelf);
    }

    private static bool WasTogglePressed()
    {
        // Centralized binding (default: backquote/tilde).
        return ControlsBindingsService.WasPressedThisFrame(ControlAction.ToggleDebugPanel);
    }
}

