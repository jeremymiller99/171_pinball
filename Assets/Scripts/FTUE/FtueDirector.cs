// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-07 (FTUE director shell).
using UnityEngine;

/// <summary>
/// Runs the tutorial. One of these lives in the Board_FTUE scene and nowhere else — its presence
/// is what raises <see cref="FtueState.Active"/>, and its destruction when the board unloads is
/// what lowers it again.
///
/// Shell only at this stage: it owns the lifecycle and nothing else. The authored step list,
/// dialogue, camera moves and board-state groups arrive in later tickets, all hanging off this
/// component so the whole tutorial stays in the board scene and dies with it.
/// </summary>
[DisallowMultipleComponent]
public sealed class FtueDirector : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Logs when the tutorial takes and releases ownership. Useful while wiring the "
        + "board; harmless to leave on.")]
    [SerializeField] private bool logStateChanges = true;

    // OnEnable/OnDisable rather than Awake/OnDestroy: toggling the component off is a reasonable
    // way to disable the tutorial while working in the scene, and it should lower the flag too.
    private void OnEnable()
    {
        FtueState.Activate(this);

        if (logStateChanges)
        {
            Debug.Log($"[FtueDirector] Tutorial active on '{gameObject.scene.name}'.", this);
        }
    }

    private void OnDisable()
    {
        FtueState.Deactivate(this);

        if (logStateChanges)
        {
            Debug.Log("[FtueDirector] Tutorial released.", this);
        }
    }
}
