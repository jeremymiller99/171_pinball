using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves a <see cref="MysteryBallDefinition"/> to a concrete unlocked ball
/// of the requested rarity at purchase time. Mystery definitions are excluded
/// from the candidate pool so a mystery never resolves to another mystery.
/// </summary>
public static class MysteryBallResolver
{
    /// <summary>
    /// Picks a random unlocked, non-mystery ball of <paramref name="rarity"/>
    /// from <paramref name="catalog"/>. Returns null if the pool is empty.
    /// </summary>
    public static BallDefinition Resolve(
        BallRarity rarity,
        IList<BallDefinition> catalog)
    {
        if (catalog == null || catalog.Count == 0)
        {
            return null;
        }

        List<BallDefinition> pool = BuildCandidatePool(rarity, catalog);

        if (pool.Count == 0)
        {
            Debug.LogWarning(
                "[MysteryBallResolver] No unlocked balls of rarity " + rarity
                + "; mystery purchase cannot be resolved.");
            return null;
        }

        int pick = Random.Range(0, pool.Count);

        return pool[pick];
    }

    private static List<BallDefinition> BuildCandidatePool(
        BallRarity rarity,
        IList<BallDefinition> catalog)
    {
        bool hasProgression = ProgressionService.Instance != null;
        var pool = new List<BallDefinition>();

        for (int i = 0; i < catalog.Count; i++)
        {
            BallDefinition def = catalog[i];

            if (def == null || !def.IsValid())
            {
                continue;
            }

            if (def is MysteryBallDefinition)
            {
                continue;
            }

            if (def.Rarity != rarity)
            {
                continue;
            }

            if (hasProgression
                && !ProgressionService.Instance.IsBallUnlocked(def.Id))
            {
                continue;
            }

            pool.Add(def);
        }

        return pool;
    }
}
