using UnityEngine;

/// <summary>
/// Defines how angel/devil rounds are distributed.
/// </summary>
public enum RoundDistributionMode
{
    /// <summary>Each round has a random chance to be angel or devil.</summary>
    Probability,
    /// <summary>A fixed number of angel and devil rounds are guaranteed.</summary>
    Guaranteed
}

/// <summary>
/// Defines a selectable "Challenge Mode" from the main menu.
/// A challenge mode is a (usually short) curated list of boards.
/// Create assets via: Create -> Pinball -> Challenge Mode.
/// </summary>
[CreateAssetMenu(menuName = "Pinball/Challenge Mode", fileName = "ChallengeMode")]
public sealed class ChallengeModeDefinition : ScriptableObject
{
    [Header("UI")]
    public string displayName = "Challenge";

    [TextArea(2, 6)]
    public string description;

    [TextArea(1, 4)]
    [Tooltip("Human-readable summary of how to win this challenge mode.")]
    public string winConditionDescription;

    [Header("Boards")]
    [Tooltip("Boards to play for this challenge mode (in order).")]
    public BoardDefinition[] boards;

    [Header("Rounds")]
    [Tooltip("Total number of rounds in this challenge. If 0, uses the default from GameRulesManager.")]
    [Min(0)]
    public int totalRounds = 7;

    [Header("Round Modifiers")]
    [Tooltip("Pool of angel (buff) modifiers to randomly select from.")]
    public RoundModifierPool angelPool;

    [Tooltip("Pool of devil (debuff) modifiers to randomly select from.")]
    public RoundModifierPool devilPool;

    [Tooltip("How angel/devil rounds are distributed.")]
    public RoundDistributionMode distributionMode = RoundDistributionMode.Probability;

    [Header("Probability Mode Settings")]
    [Tooltip("Chance for each round to be an angel round (0-1).")]
    [Range(0f, 1f)]
    public float angelChance = 0.2f;

    [Tooltip("Chance for each round to be a devil round (0-1).")]
    [Range(0f, 1f)]
    public float devilChance = 0.2f;

    [Header("Guaranteed Mode Settings")]
    [Tooltip("Number of guaranteed angel rounds.")]
    [Min(0)]
    public int guaranteedAngels = 2;

    [Tooltip("Number of guaranteed devil rounds.")]
    [Min(0)]
    public int guaranteedDevils = 2;

    /// <summary>
    /// Returns the effective total rounds, using the provided default if totalRounds is 0.
    /// </summary>
    public int GetTotalRounds(int defaultRounds)
    {
        return totalRounds > 0 ? totalRounds : defaultRounds;
    }

    /// <summary>
    /// Returns true if this challenge has any modifier pools configured.
    /// </summary>
    public bool HasModifierPools => (angelPool != null && angelPool.ValidCount > 0) ||
                                     (devilPool != null && devilPool.ValidCount > 0);
}

