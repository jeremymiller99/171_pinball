using System.Collections.Generic;

/// <summary>
/// Central gate for which balls / board components are eligible to appear in a
/// run's draw pools (shop offers, mystery-ball resolution, component upgrades).
///
/// On top of the progression unlock check, a run can be narrowed by the active
/// ship and/or mission via their allow-lists. An item is eligible only if BOTH
/// the active ship and the active mission allow it (intersection). An empty
/// allow-list on either source means "no restriction from that source".
/// </summary>
public static class RunPoolFilter
{
    /// <summary>
    /// True if the active ship AND active mission both allow this ball. A missing
    /// session, ship, or mission imposes no restriction from that source.
    /// </summary>
    public static bool IsBallAllowed(BallDefinition def)
    {
        if (def == null)
        {
            return false;
        }

        GameSession session = GameSession.Instance;
        if (session == null)
        {
            return true;
        }

        PlayerShipDefinition ship = session.ActiveShip;
        if (ship != null && !ship.AllowsBall(def))
        {
            return false;
        }

        ChallengeModeDefinition mission = session.ActiveChallenge;
        if (mission != null && !mission.AllowsBall(def))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// True if the active ship AND active mission both allow this board
    /// component. A missing session, ship, or mission imposes no restriction.
    /// </summary>
    public static bool IsComponentAllowed(BoardComponentDefinition def)
    {
        if (def == null)
        {
            return false;
        }

        GameSession session = GameSession.Instance;
        if (session == null)
        {
            return true;
        }

        PlayerShipDefinition ship = session.ActiveShip;
        if (ship != null && !ship.AllowsComponent(def))
        {
            return false;
        }

        ChallengeModeDefinition mission = session.ActiveChallenge;
        if (mission != null && !mission.AllowsComponent(def))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the ship/mission-allowed subset of <paramref name="unlocked"/>.
    /// If the restriction would empty an otherwise non-empty pool (e.g. a
    /// non-overlapping ship+mission configuration), the original unlocked pool is
    /// returned instead, so callers that MUST yield an item never get nothing.
    /// </summary>
    public static List<BallDefinition> FilterBallsWithFallback(
        List<BallDefinition> unlocked)
    {
        if (unlocked == null || unlocked.Count == 0)
        {
            return unlocked;
        }

        var filtered = new List<BallDefinition>(unlocked.Count);
        for (int i = 0; i < unlocked.Count; i++)
        {
            if (IsBallAllowed(unlocked[i]))
            {
                filtered.Add(unlocked[i]);
            }
        }

        return filtered.Count > 0 ? filtered : unlocked;
    }

    /// <summary>
    /// Component counterpart to <see cref="FilterBallsWithFallback"/>: returns the
    /// allowed subset, or the original unlocked pool if filtering empties it.
    /// </summary>
    public static List<BoardComponentDefinition> FilterComponentsWithFallback(
        List<BoardComponentDefinition> unlocked)
    {
        if (unlocked == null || unlocked.Count == 0)
        {
            return unlocked;
        }

        var filtered = new List<BoardComponentDefinition>(unlocked.Count);
        for (int i = 0; i < unlocked.Count; i++)
        {
            if (IsComponentAllowed(unlocked[i]))
            {
                filtered.Add(unlocked[i]);
            }
        }

        return filtered.Count > 0 ? filtered : unlocked;
    }
}
