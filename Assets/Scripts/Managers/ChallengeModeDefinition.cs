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

    [TextArea(2, 6)]
    public string description;

    [Header("Boards")]
    [Tooltip("Boards to play for this challenge mode (in order).")]
    public BoardDefinition[] boards;
}

