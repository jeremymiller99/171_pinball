using UnityEngine;

/// <summary>
/// Data for a board that can be loaded additively as a scene.
/// Create assets via: Create -> Pinball -> Board Definition.
/// </summary>
[CreateAssetMenu(menuName = "Pinball/Board Definition", fileName = "BoardDefinition")]
public sealed class BoardDefinition : ScriptableObject
{
    public enum ClearConditionKind
    {
        None = 0,
        LevelIndexAtLeast = 1,
        CoinsAtLeast = 2,
        TotalScoreAtLeast = 3
    }

    [Header("Identity")]
    public string displayName = "Board";

    [Tooltip("Scene name to load additively (must be in Build Settings). Example: Board_Alpha")]
    public string boardSceneName = "Board_Alpha";

    [Header("Board clear objective (checked when closing the shop)")]
    public ClearConditionKind clearCondition = ClearConditionKind.None;

    [Tooltip("Board is cleared when GameRulesManager.LevelIndex >= this value.")]
    public int targetRoundIndex = 3;

    [Tooltip("Board is cleared when GameRulesManager.Coins >= this value.")]
    public int targetCoins = 10;

    [Tooltip("Board is cleared when GameRulesManager.TotalScore >= this value.")]
    public float targetRoundTotal = 2000f;
}

