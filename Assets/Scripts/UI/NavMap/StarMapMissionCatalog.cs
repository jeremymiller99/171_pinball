using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sources the real board / mission / ship assets a star map hands out, and
/// assigns them to stars deterministically from the map seed.
///
/// Assets live under Resources (BoardDefinitions, PlayerShipDefinitions), so the
/// catalog loads itself when no explicit list is supplied in the inspector.
/// </summary>
public static class StarMapMissionCatalog
{
    /// <summary>A board paired with one of its missions. Mission may be null — see BuildAssignments.</summary>
    public struct Assignment
    {
        public BoardDefinition Board;
        public ChallengeModeDefinition Mission;

        public bool IsPlayable { get { return Board != null && Mission != null; } }
    }

    public const string BoardResourcePath = "BoardDefinitions";
    public const string ShipResourcePath = "PlayerShipDefinitions";

    public static List<BoardDefinition> LoadBoards(IList<BoardDefinition> overrides)
    {
        var boards = new List<BoardDefinition>();

        if (overrides != null && overrides.Count > 0)
        {
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i] != null) boards.Add(overrides[i]);

            if (boards.Count > 0) return boards;
        }

        BoardDefinition[] loaded = Resources.LoadAll<BoardDefinition>(BoardResourcePath);
        for (int i = 0; i < loaded.Length; i++)
            if (loaded[i] != null) boards.Add(loaded[i]);

        // Resource order isn't guaranteed stable across platforms; sort so a
        // given seed assigns the same board to the same star everywhere.
        boards.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return boards;
    }

    public static List<PlayerShipDefinition> LoadShips(IList<PlayerShipDefinition> overrides)
    {
        var ships = new List<PlayerShipDefinition>();

        if (overrides != null && overrides.Count > 0)
        {
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i] != null) ships.Add(overrides[i]);

            if (ships.Count > 0) return ships;
        }

        PlayerShipDefinition[] loaded = Resources.LoadAll<PlayerShipDefinition>(ShipResourcePath);
        for (int i = 0; i < loaded.Length; i++)
            if (loaded[i] != null) ships.Add(loaded[i]);

        ships.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return ships;
    }

    /// <summary>
    /// Flattens boards into one entry per playable (board, mission) pair.
    ///
    /// A board with an empty missions array still yields one entry with a null
    /// mission, so it shows up on the map as an un-runnable survey site rather
    /// than vanishing. That surfaces unpopulated board data instead of hiding it.
    /// </summary>
    public static List<Assignment> BuildAssignments(IList<BoardDefinition> boards)
    {
        var assignments = new List<Assignment>();
        if (boards == null) return assignments;

        for (int i = 0; i < boards.Count; i++)
        {
            BoardDefinition board = boards[i];
            if (board == null) continue;

            ChallengeModeDefinition[] missions = board.missions;
            if (missions == null || missions.Length == 0)
            {
                assignments.Add(new Assignment { Board = board, Mission = null });
                continue;
            }

            bool added = false;
            for (int m = 0; m < missions.Length; m++)
            {
                if (missions[m] == null) continue;
                assignments.Add(new Assignment { Board = board, Mission = missions[m] });
                added = true;
            }

            if (!added)
                assignments.Add(new Assignment { Board = board, Mission = null });
        }

        return assignments;
    }

    /// <summary>Stable pick, so the same seed always puts the same mission on the same star.</summary>
    public static Assignment Pick(IList<Assignment> assignments, int seed, int territoryIndex, int starIndex)
    {
        if (assignments == null || assignments.Count == 0) return default(Assignment);

        int hash = Hash(seed, territoryIndex * 397 + starIndex);
        return assignments[hash % assignments.Count];
    }

    /// <summary>
    /// True when the ship is available to fly. Falls back to unlocked when the
    /// progression service isn't running, so the map still works in isolation.
    /// </summary>
    public static bool IsShipUnlocked(PlayerShipDefinition ship)
    {
        if (ship == null) return false;

        ProgressionService progression = ProgressionService.Instance;
        if (progression == null) return true;

        return progression.IsShipUnlocked(ship.Id);
    }

    /// <summary>
    /// Coarse difficulty band. ChallengeModeDefinition has no difficulty field,
    /// so this reads the S-rank score threshold as a proxy.
    /// </summary>
    public static string DifficultyLabel(ChallengeModeDefinition mission)
    {
        if (mission == null) return "UNSURVEYED";

        int threshold = mission.sRankThreshold;
        if (threshold <= 0) return "UNRATED";
        if (threshold < 30000) return "LOW THREAT";
        if (threshold < 70000) return "MODERATE";
        if (threshold < 120000) return "HIGH THREAT";
        return "EXTREME";
    }

    static int Hash(int seed, int value)
    {
        unchecked
        {
            int h = seed ^ (value * 668265263);
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return h & 0x7FFFFFFF;
        }
    }
}
