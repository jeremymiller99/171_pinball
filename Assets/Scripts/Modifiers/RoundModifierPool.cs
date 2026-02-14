using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container that holds a pool of modifiers to randomly select from.
/// Create via Create > Pinball > Modifier Pool.
/// </summary>
[CreateAssetMenu(menuName = "Pinball/Modifier Pool", fileName = "New Modifier Pool")]
public class RoundModifierPool : ScriptableObject
{
    [Tooltip("List of modifiers in this pool.")]
    public List<RoundModifierDefinition> modifiers = new List<RoundModifierDefinition>();

    /// <summary>
    /// Returns a random modifier from the pool using the provided RNG.
    /// Returns null if the pool is empty.
    /// </summary>
    public RoundModifierDefinition GetRandomModifier(System.Random rng)
    {
        if (modifiers == null || modifiers.Count == 0)
        {
            return null;
        }

        // Filter out nulls
        var validModifiers = new List<RoundModifierDefinition>();
        foreach (var mod in modifiers)
        {
            if (mod != null)
            {
                validModifiers.Add(mod);
            }
        }

        if (validModifiers.Count == 0)
        {
            return null;
        }

        int index = rng.Next(validModifiers.Count);
        return validModifiers[index];
    }

    /// <summary>
    /// Returns a random modifier using Unity's default random.
    /// </summary>
    public RoundModifierDefinition GetRandomModifier()
    {
        if (modifiers == null || modifiers.Count == 0)
        {
            return null;
        }

        // Filter out nulls
        var validModifiers = new List<RoundModifierDefinition>();
        foreach (var mod in modifiers)
        {
            if (mod != null)
            {
                validModifiers.Add(mod);
            }
        }

        if (validModifiers.Count == 0)
        {
            return null;
        }

        int index = Random.Range(0, validModifiers.Count);
        return validModifiers[index];
    }

    /// <summary>
    /// Returns the count of valid (non-null) modifiers in the pool.
    /// </summary>
    public int ValidCount
    {
        get
        {
            if (modifiers == null) return 0;
            int count = 0;
            foreach (var mod in modifiers)
            {
                if (mod != null) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Returns up to `count` distinct random modifiers from the pool, excluding `exclude`.
    /// Used e.g. by "Unlucky Day" to get 2 random devils without picking itself.
    /// </summary>
    public List<RoundModifierDefinition> GetRandomModifiers(System.Random rng, int count, RoundModifierDefinition exclude = null)
    {
        var result = new List<RoundModifierDefinition>();
        if (modifiers == null || count <= 0) return result;

        var valid = new List<RoundModifierDefinition>();
        foreach (var mod in modifiers)
        {
            if (mod != null && mod != exclude)
                valid.Add(mod);
        }

        if (valid.Count == 0) return result;
        int take = Mathf.Min(count, valid.Count);

        for (int i = 0; i < take; i++)
        {
            int idx = rng.Next(valid.Count);
            result.Add(valid[idx]);
            valid.RemoveAt(idx);
        }
        return result;
    }
}
