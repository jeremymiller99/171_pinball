using UnityEngine;

/// <summary>
/// One per board scene. Acts as the single entry point for the gameplay core to bind to a board.
/// </summary>
public sealed class BoardRoot : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private Transform spawnPoint;

    [Header("Optional (hand path / track)")]
    [Tooltip("If assigned, the hand balls will be laid out along a path starting here.")]
    [SerializeField] private Transform handPathStart;

    [Tooltip("Optional corner/guide points for the hand path (in order).")]
    [SerializeField] private Transform[] handPathWaypoints;

    private BoardDefinition _definition;

    public Transform SpawnPoint => spawnPoint;
    public Transform HandPathStart => handPathStart;
    public Transform[] HandPathWaypoints => handPathWaypoints;
    public BoardDefinition Definition => _definition;

    public void Initialize(BoardDefinition definition)
    {
        _definition = definition;
    }

    public bool IsCleared(GameRulesManager rules)
    {
        if (_definition == null || rules == null)
        {
            return false;
        }

        switch (_definition.clearCondition)
        {
            case BoardDefinition.ClearConditionKind.None:
                return false;
            case BoardDefinition.ClearConditionKind.LevelIndexAtLeast:
                return rules.LevelIndex >= _definition.targetRoundIndex;
            case BoardDefinition.ClearConditionKind.CoinsAtLeast:
                return rules.Coins >= _definition.targetCoins;
            case BoardDefinition.ClearConditionKind.TotalScoreAtLeast:
                return rules.TotalScore >= _definition.targetRoundTotal;
            default:
                return false;
        }
    }
}

