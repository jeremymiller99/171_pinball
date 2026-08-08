// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (block board input under modal UI).
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks whether a blocking full-screen panel (pause, round failed, win, leaderboard) is open.
/// Board input reads <see cref="IsBlocked"/> and stands down while it is true: the cursor stops
/// hovering and clicking components through the panel, and the flippers, plunger and shop key
/// stop responding.
///
/// A panel that pauses time is not enough on its own — <c>Update</c> still runs at
/// <c>timeScale</c> 0, and the round-failed panel does not pause at all — so each input site
/// has to opt out explicitly.
///
/// Blockers are held as objects rather than a bare count so a panel that is destroyed mid-block
/// (scene load from a Quit button) releases itself instead of stranding the gate closed.
/// </summary>
public static class GameplayInputGate
{
    private static readonly HashSet<Object> Blockers = new HashSet<Object>();

    /// <summary>True while any registered panel is open. Board input should idle.</summary>
    public static bool IsBlocked
    {
        get
        {
            PruneDeadBlockers();
            return Blockers.Count > 0;
        }
    }

    /// <summary>Registers <paramref name="owner"/> as blocking. Safe to call repeatedly.</summary>
    public static void Block(Object owner)
    {
        if (owner == null) return;
        Blockers.Add(owner);
    }

    /// <summary>Releases <paramref name="owner"/>'s block. Safe to call when it never blocked.</summary>
    public static void Unblock(Object owner)
    {
        if (owner == null) return;
        Blockers.Remove(owner);
    }

    /// <summary>
    /// Drops blockers Unity has destroyed. Without this a panel torn down by a scene load would
    /// keep the gate shut for the rest of the session.
    /// </summary>
    private static void PruneDeadBlockers()
    {
        if (Blockers.Count == 0) return;

        // RemoveWhere walks the buckets rather than hashing the entry to find it, so it does not
        // depend on a destroyed object still hashing the way it did when it was added.
        Blockers.RemoveWhere(o => o == null);
    }
}
