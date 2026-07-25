using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for the FULL ball / board-component catalogs a run can
/// draw from. Definitions live under Resources (BallDefinitions,
/// BoardComponentDefinitions), so the catalog loads itself instead of relying on
/// a hand-maintained inspector list that silently goes stale when new
/// definitions are authored.
///
/// This is the *unfiltered* catalog. Unlock state
/// (<see cref="ProgressionService.IsBallUnlocked"/>) and ship / mission
/// allow-lists (<see cref="RunPoolFilter"/>) are applied per draw by the
/// consumers, so items unlocked mid-run show up in the next shop visit.
/// </summary>
public static class RunItemCatalog
{
    public const string BallResourcePath = "BallDefinitions";
    public const string ComponentResourcePath =
        "BoardComponentDefinitions";

    /// <summary>
    /// Every ball definition under Resources, or <paramref name="overrides"/>
    /// verbatim when that list has any entries (inspector escape hatch for
    /// tests / curated setups).
    /// </summary>
    public static List<BallDefinition> LoadBalls(
        IList<BallDefinition> overrides)
    {
        var balls = new List<BallDefinition>();

        if (overrides != null && overrides.Count > 0)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                if (overrides[i] != null) balls.Add(overrides[i]);
            }

            if (balls.Count > 0) return balls;
        }

        BallDefinition[] loaded =
            Resources.LoadAll<BallDefinition>(BallResourcePath);

        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null) balls.Add(loaded[i]);
        }

        // Resource order isn't guaranteed stable across platforms; sort so shop
        // shelves lay items out in the same order everywhere.
        balls.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return balls;
    }

    /// <summary>
    /// Component counterpart to <see cref="LoadBalls"/>.
    /// </summary>
    public static List<BoardComponentDefinition> LoadComponents(
        IList<BoardComponentDefinition> overrides)
    {
        var components = new List<BoardComponentDefinition>();

        if (overrides != null && overrides.Count > 0)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                if (overrides[i] != null) components.Add(overrides[i]);
            }

            if (components.Count > 0) return components;
        }

        BoardComponentDefinition[] loaded =
            Resources.LoadAll<BoardComponentDefinition>(
                ComponentResourcePath);

        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null) components.Add(loaded[i]);
        }

        components.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return components;
    }
}
