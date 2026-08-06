// Created with Claude Code (Opus 5) by JJ on 2026-08-05: swallow the click that
// restores focus to the player window.
using UnityEngine;

/// <summary>
/// Tracks when the player window last regained focus.
///
/// On Windows, the click that re-activates a background window is also
/// delivered to that window, so alt-tabbing (or clicking) back into the build
/// lands a real pointer down/up on whatever UI sits under the cursor. In the
/// main menu that can be the "Quit" item, which exits the game the instant the
/// player tabs back in. UI whose action is destructive checks
/// <see cref="IsSettlingAfterFocus"/> and ignores input for a short grace
/// period after focus returns.
///
/// Installs itself automatically before the first scene loads; nothing needs to
/// be placed in a scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class ApplicationFocusGuard : MonoBehaviour
{
    /// <summary>How long input is ignored after the window regains focus.</summary>
    public const float defaultGraceSeconds = 0.35f;

    private static ApplicationFocusGuard instance;

    // Unscaled time of the last focus gain. Starts far in the past so the guard
    // is inert until the app actually loses and regains focus.
    private static float focusRegainedAt = float.NegativeInfinity;

    /// <summary>
    /// True for a short window after the player window regained focus, i.e.
    /// while a pointer click may just be the click that refocused the game.
    /// </summary>
    public static bool IsSettlingAfterFocus(float graceSeconds = defaultGraceSeconds)
    {
#if !UNITY_EDITOR
        // A press can also be delivered just before Windows hands the window
        // focus, in which case the grace period hasn't started yet. Player only:
        // in the editor this tracks the editor's own focus, which would swallow
        // the first click back into the Game view.
        if (!Application.isFocused)
        {
            return true;
        }
#endif

        return Time.unscaledTime - focusRegainedAt < graceSeconds;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Reset for domain-reload-free play mode, where statics survive.
        focusRegainedAt = float.NegativeInfinity;

        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject(nameof(ApplicationFocusGuard));
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ApplicationFocusGuard>();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            focusRegainedAt = Time.unscaledTime;
        }

        Debug.Log($"[FocusGuard] Window focus {(hasFocus ? "gained" : "lost")}.");
    }
}
