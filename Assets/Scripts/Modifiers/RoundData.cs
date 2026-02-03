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
    /// </summary>
    public string GetModifierDescription()
    {
        if (modifier != null)
        {
            if (!string.IsNullOrEmpty(modifier.description))
            {
                return modifier.description;
            }
            return modifier.GetEffectsSummary();
        }

        return "";
    }
}
