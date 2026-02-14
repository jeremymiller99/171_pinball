using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the type of a round.
/// </summary>
public enum RoundType
{
    Normal,
    Angel,
    Devil
}

/// <summary>
/// Stores the generated data for a single round in a run.
/// </summary>
[System.Serializable]
public class RoundData
{
    [Tooltip("Zero-based index of this round.")]
    public int roundIndex;

    [Tooltip("The type of this round (Normal, Angel, or Devil).")]
    public RoundType type;

    [Tooltip("The modifier applied to this round. Null for Normal rounds.")]
    public RoundModifierDefinition modifier;

    /// <summary>
    /// When the modifier is Unlucky Day (useTwoRandomDevilsFromPool), the two resolved devil modifiers from the pool.
    /// Set at round generation so the round card can show their names in the description.
    /// </summary>
    public List<RoundModifierDefinition> compositeModifiers;

    public RoundData()
    {
        roundIndex = 0;
        type = RoundType.Normal;
        modifier = null;
    }

    public RoundData(int index, RoundType roundType, RoundModifierDefinition roundModifier = null)
    {
        roundIndex = index;
        type = roundType;
        modifier = roundModifier;
    }

    /// <summary>
    /// Returns the display name for this round's modifier, or "Normal" if no modifier.
    /// </summary>
    public string GetModifierDisplayName()
    {
        if (modifier != null && !string.IsNullOrEmpty(modifier.displayName))
        {
            return modifier.displayName;
        }

        return type.ToString();
    }

    /// <summary>
    /// Returns the description for this round's modifier, or empty string if none.
    /// For Unlucky Day, includes the two resolved devil names when available.
    /// </summary>
    public string GetModifierDescription()
    {
        if (modifier != null)
        {
            if (modifier.useTwoRandomDevilsFromPool && compositeModifiers != null && compositeModifiers.Count > 0)
            {
                var names = new List<string>();
                foreach (var m in compositeModifiers)
                {
                    if (m != null && !string.IsNullOrEmpty(m.displayName))
                        names.Add(m.displayName);
                }
                if (names.Count > 0)
                    return "Two random devil modifiers this round: " + string.Join(", ", names) + ".";
            }
            if (!string.IsNullOrEmpty(modifier.description))
                return modifier.description;
            return modifier.GetEffectsSummary();
        }

        return "";
    }
}
