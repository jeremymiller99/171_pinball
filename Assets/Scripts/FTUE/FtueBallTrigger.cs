// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (FTUE ball trigger volume).
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
/// </summary>
[DisallowMultipleComponent]
public sealed class FtueBallTrigger : MonoBehaviour
{
    /// <summary>Fires once per arming, when a ball first enters the volume.</summary>
    public event Action BallEntered;

    private bool armed;

    /// <summary>True between <see cref="Arm"/> and the ball arriving.</summary>
    public bool IsArmed => armed;

    private void Awake()
    {
        var col = GetComponent<Collider>();

        if (col == null)
        {
            Debug.LogError($"[{nameof(FtueBallTrigger)}] '{name}' has no Collider, so no ball can "
                + "ever enter it.", this);
            return;
        }

        if (!col.isTrigger)
        {
            Debug.LogError($"[{nameof(FtueBallTrigger)}] '{name}' has a Collider that is not set "
                + "to Is Trigger — the ball will bounce off it instead of passing through.", this);
        }
    }

    public void Arm()
    {
        armed = true;
    }

    public void Disarm()
    {
        armed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!armed) return;
        if (!other.CompareTag("Ball")) return;

        armed = false;
        BallEntered?.Invoke();
    }
}
