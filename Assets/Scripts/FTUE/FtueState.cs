// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-07 (FTUE shared state contract).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (level goals + shop pool override).
using System.Collections.Generic;
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

    private static IReadOnlyList<float> levelGoals;

    // Null means "no override"; an empty list means "nothing of this kind is offered". The two
    // are different answers and the shop needs to be able to say either.
    private static IReadOnlyList<BallDefinition> allowedBalls;
    private static IReadOnlyList<BoardComponentDefinition> allowedComponents;
    private static bool shopOverrideSet;

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
    /// Authored score targets for each level-up, replacing the normal goal curve. The shipped
    /// curve starts at 1000 and climbs steeply, which is far too much for a first-time player who
    /// is still learning what a flipper does.
    /// </summary>
    public static void SetLevelGoals(IReadOnlyList<float> goals)
    {
        levelGoals = goals;
    }

    /// <summary>
    /// The tutorial's goal for <paramref name="roundIndex"/>. False when the tutorial is not
    /// running or has authored no goals, in which case the normal curve applies untouched.
    ///
    /// Indices past the end clamp to the last authored goal rather than falling back to the normal
    /// curve — the tutorial ends on power surges, not on a level count, so a player who overruns
    /// the table should keep the gentle target rather than hit a cliff.
    /// </summary>
    public static bool TryGetLevelGoal(int roundIndex, out float goal)
    {
        goal = 0f;

        if (ResolveOwner() == null) return false;
        if (levelGoals == null || levelGoals.Count == 0) return false;

        int index = Mathf.Clamp(roundIndex, 0, levelGoals.Count - 1);
        goal = Mathf.Max(0f, levelGoals[index]);

        return true;
    }

    /// <summary>
    /// Narrows what the shop may offer for the current visit. Authoritative while set: it replaces
    /// the ship and mission allow-lists rather than intersecting with them, and items named here
    /// bypass the progression unlock check, since the tutorial has to be able to hand out an item
    /// a brand-new profile has not earned.
    ///
    /// Pass an empty list to offer nothing of that kind. Pass null for both to clear.
    /// </summary>
    public static void SetShopOverride(
        IReadOnlyList<BallDefinition> balls,
        IReadOnlyList<BoardComponentDefinition> components)
    {
        allowedBalls = balls;
        allowedComponents = components;
        shopOverrideSet = balls != null || components != null;
    }

    public static void ClearShopOverride()
    {
        allowedBalls = null;
        allowedComponents = null;
        shopOverrideSet = false;
    }

    /// <summary>True while a visit-specific pool is in force.</summary>
    public static bool HasShopOverride => ResolveOwner() != null && shopOverrideSet;

    /// <summary>Only meaningful while <see cref="HasShopOverride"/>.</summary>
    public static bool AllowsBall(BallDefinition def)
    {
        return ListContains(allowedBalls, def);
    }

    /// <summary>Only meaningful while <see cref="HasShopOverride"/>.</summary>
    public static bool AllowsComponent(BoardComponentDefinition def)
    {
        return ListContains(allowedComponents, def);
    }

    /// <summary>
    /// IReadOnlyList has no Contains, and this is called once per catalogue entry while the shop
    /// builds its pool, so it walks the list directly rather than allocating a LINQ enumerator.
    /// </summary>
    private static bool ListContains<T>(IReadOnlyList<T> list, T value) where T : class
    {
        if (value == null || list == null) return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], value)) return true;
        }

        return false;
    }

    /// <summary>
    /// Drops every trace of the tutorial. The ownership check above already covers the normal
    /// exits; this is the explicit valve for completion and for anything defensive.
    /// </summary>
    public static void Reset()
    {
        owner = null;
        roundFailureSuppressed = false;
        levelGoals = null;
        ClearShopOverride();
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
            levelGoals = null;
            ClearShopOverride();
        }

        return owner;
    }
}
