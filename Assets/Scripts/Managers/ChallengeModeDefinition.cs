using UnityEngine;

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

    [Tooltip("Icon shown on the run select card.")]
    public Sprite icon;

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
    [Tooltip("Pool of devil (debuff) modifiers to randomly select from.")]
    public RoundModifierPool devilPool;

    [Header("Run Ranking Thresholds")]
    [Tooltip(
        "Score needed to reach C- rank. "
        + "Below this is D tier.")]
    [Min(0)]
    public int cRankThreshold = 5000;

    [Tooltip("Score needed to reach B- rank.")]
    [Min(0)]
    public int bRankThreshold = 15000;

    [Tooltip("Score needed to reach A- rank.")]
    [Min(0)]
    public int aRankThreshold = 35000;

    [Tooltip("Score needed to reach S- rank.")]
    [Min(0)]
    public int sRankThreshold = 60000;

    [Tooltip(
        "Score at or above which the player "
        + "earns S+ rank.")]
    [Min(0)]
    public int sPlusThreshold = 100000;

    /// <summary>
    /// Returns true if this challenge has any
    /// modifier pools configured.
    /// </summary>
    public bool HasModifierPools =>
        (devilPool != null
            && devilPool.ValidCount > 0);
}

