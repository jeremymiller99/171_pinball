using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Debug/testing only: press F8 to force the shop open on the current board,
// bypassing the normal round-progression gate. Self-installs at runtime via a
// DontDestroyOnLoad object, so no scene setup is required. The whole type is
// compiled out of release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DisallowMultipleComponent]
public sealed class DebugForceShopHotkey : MonoBehaviour
{
    private static bool _installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (_installed) return;
        _installed = true;

        var go = new GameObject(nameof(DebugForceShopHotkey));
        DontDestroyOnLoad(go);
        go.AddComponent<DebugForceShopHotkey>();
    }

    private void Update()
    {
        if (!ForceShopKeyPressed()) return;

        // Get<T>() falls back to FindAnyObjectByType, so this resolves even before
        // GameRulesManager has registered itself, or on additive board loads.
        var rules = ServiceLocator.Get<GameRulesManager>();
        if (rules != null)
        {
            rules.DebugForceOpenShop();
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(DebugForceShopHotkey)}: no GameRulesManager found — load a board first.");
        }
    }

    private static bool ForceShopKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && kb[Key.F8].wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F8);
#endif
    }
}
#endif
