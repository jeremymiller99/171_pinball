// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (FTUE ball trigger volume).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (one-way gate mode).
using System;
using UnityEngine;

/// <summary>
/// A trigger volume in the board that tells the director a ball has passed through it. Used for
/// the beat that catches the ball on its way down to the flippers.
///
/// Armed rather than always-live, and it disarms itself the moment it fires: the beat should
/// happen once, on the way down, not every time the ball crosses the same plane on a later
/// bounce. The director re-arms it if the lesson still has not been given — a ball that drains
/// straight down an outlane never passes through, and the beat must not be lost because of it.
///
/// Optionally also acts as a one-way gate — see <see cref="solidAfterPass"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class FtueBallTrigger : MonoBehaviour
{
    [Header("One-way gate")]
    [Tooltip("Turns this volume into a wall the ball can only cross outwards. It starts as a "
        + "trigger, goes solid once the ball is clear of it, and opens again on the next launch — "
        + "so the ball can leave the lane but cannot wander back up it.")]
    [SerializeField] private bool solidAfterPass;

    /// <summary>Fires once per arming, when a ball first enters the volume.</summary>
    public event Action BallEntered;

    private Collider col;
    private bool armed;

    // Counted rather than a bool: with more than one ball on the board the wall must not close
    // while a second one is still standing in the doorway.
    private int ballsInside;

    /// <summary>True between <see cref="Arm"/> and the ball arriving.</summary>
    public bool IsArmed => armed;

    /// <summary>True while the gate is solid. Always false unless <see cref="solidAfterPass"/>.</summary>
    public bool IsGateClosed => solidAfterPass && col != null && !col.isTrigger;

    private void Awake()
    {
        col = GetComponent<Collider>();

        if (col == null)
        {
            Debug.LogError($"[{nameof(FtueBallTrigger)}] '{name}' has no Collider, so no ball can "
                + "ever enter it.", this);
            return;
        }

        // In gate mode OnEnable forces it open, so how the scene was saved does not matter and the
        // complaint below would be noise.
        if (!col.isTrigger && !solidAfterPass)
        {
            Debug.LogError($"[{nameof(FtueBallTrigger)}] '{name}' has a Collider that is not set "
                + "to Is Trigger — the ball will bounce off it instead of passing through.", this);
        }
    }

    // Subscribed unconditionally, so toggling solidAfterPass in the inspector at runtime does not
    // leave the gate without its reopen signal. Static event: an unsubscribe missed here would keep
    // a destroyed trigger alive and firing for the rest of the session.
    private void OnEnable()
    {
        PinballLauncher.BallLaunched += OnBallLaunched;

        if (solidAfterPass) OpenGate();
    }

    private void OnDisable()
    {
        PinballLauncher.BallLaunched -= OnBallLaunched;
    }

    public void Arm()
    {
        armed = true;
    }

    public void Disarm()
    {
        armed = false;
    }

    /// <summary>
    /// Back to a trigger, and forgets who was standing in it. Clearing the count matters: a ball
    /// despawned while inside the volume never raises OnTriggerExit, and a count stuck above zero
    /// would mean the gate never closed again.
    /// </summary>
    public void OpenGate()
    {
        ballsInside = 0;

        if (col == null) return;
        col.isTrigger = true;
    }

    /// <summary>Solid. The ball bounces off it like any other wall.</summary>
    public void CloseGate()
    {
        if (col == null) return;
        col.isTrigger = false;
    }

    private void OnBallLaunched(GameObject launched)
    {
        if (!solidAfterPass) return;

        OpenGate();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        // Counted before the armed check, deliberately: the gate is not part of the beat and has to
        // work on a trigger the director never arms.
        if (solidAfterPass) ballsInside++;

        if (!armed) return;

        armed = false;
        BallEntered?.Invoke();
    }

    /// <summary>
    /// Closed on exit rather than on entry. Going solid the instant the ball touched the volume
    /// would trap it inside its own collider and fire it back out in whatever direction physics
    /// picked to depenetrate.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!solidAfterPass) return;
        if (!other.CompareTag("Ball")) return;

        ballsInside = Mathf.Max(0, ballsInside - 1);
        if (ballsInside > 0) return;

        CloseGate();
    }
}
