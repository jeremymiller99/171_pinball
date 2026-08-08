// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-07 (FTUE shared state contract).
using UnityEngine;

/// <summary>
/// The one piece of state shared gameplay systems consult to tell whether the tutorial is
/// running. Every edit this feature makes to a shared file is a guard on <see cref="Active"/>,
/// so the normal boards keep their existing behaviour verbatim.
///
/// Ownership rather than a bare bool, mirroring <see cref="GameplayInputGate"/>: the flag is
/// derived from a live reference to the <see cref="FtueDirector"/>, which lives in the
/// Board_FTUE scene. Unloading that board destroys the director, and Unity reports a destroyed
/// object as null, so the tutorial flag falls to false on its own.
///
/// That matters more than it looks. The dangerous case is not a domain reload — it is FTUE ->
/// main menu -> a normal run inside one session, where a plain static bool would survive and
/// leak tutorial behaviour into a real board. Deriving the flag from an object that dies with
/// the board means the normal path cannot inherit a stale flag even if the tutorial exits
/// through a path nobody wrote cleanup for (a Quit button, an exception, a scene load).
/// </summary>
public static class FtueState
{
    private static Object owner;
    private static bool roundFailureSuppressed;

    /// <summary>True while the FTUE board is loaded and its director is alive.</summary>
    public static bool Active => ResolveOwner() != null;

    /// <summary>
    /// True while losing the ball must not end the run. Read by
    /// <c>GameRulesManager.ShowRoundFailed</c>. Always false outside the tutorial, so it cannot
    /// make a normal board unlosable.
    /// </summary>
    public static bool SuppressRoundFailure => ResolveOwner() != null && roundFailureSuppressed;

    /// <summary>
    /// Marks the tutorial as running, owned by <paramref name="ftueOwner"/> (the director).
    /// Round-failure suppression starts on, since the tutorial is unlosable from its first ball.
    /// </summary>
    public static void Activate(Object ftueOwner)
    {
        if (ftueOwner == null) return;

        owner = ftueOwner;
        roundFailureSuppressed = true;
    }

    /// <summary>
    /// Releases <paramref name="ftueOwner"/>'s claim. Ignores a caller that is not the current
    /// owner, so a late teardown from a previous director cannot switch off a live tutorial.
    /// </summary>
    public static void Deactivate(Object ftueOwner)
    {
        if (ftueOwner == null) return;
        if (owner != ftueOwner) return;

        Reset();
    }

    /// <summary>
    /// Lets the director hand control of round failure back to the game — used at the end of the
    /// tutorial, so the final moments behave normally.
    /// </summary>
    public static void SetRoundFailureSuppressed(bool suppressed)
    {
        roundFailureSuppressed = suppressed;
    }

    /// <summary>
    /// Drops every trace of the tutorial. The ownership check above already covers the normal
    /// exits; this is the explicit valve for completion and for anything defensive.
    /// </summary>
    public static void Reset()
    {
        owner = null;
        roundFailureSuppressed = false;
    }

    /// <summary>
    /// Returns the owner, clearing it first if Unity has destroyed it. Without this a board torn
    /// down without a clean <see cref="Deactivate"/> would keep the tutorial flag raised for the
    /// rest of the session.
    /// </summary>
    private static Object ResolveOwner()
    {
        // Unity's overloaded == reports a destroyed object as null while the C# reference is
        // still live, so this both answers the question and drops the dead reference.
        if (owner == null)
        {
            owner = null;
            roundFailureSuppressed = false;
        }

        return owner;
    }
}
