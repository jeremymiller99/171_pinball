using UnityEngine;

/// <summary>
/// One per board scene. Acts as the single entry point for the gameplay core to bind to a board.
/// </summary>
public sealed class BoardRoot : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private Transform spawnPoint;

    private BoardDefinition _definition;

    public Transform SpawnPoint => spawnPoint;
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
            case BoardDefinition.ClearConditionKind.RoundIndexAtLeast:
                return rules.RoundIndex >= _definition.targetRoundIndex;
            case BoardDefinition.ClearConditionKind.CoinsAtLeast:
                return rules.Coins >= _definition.targetCoins;
            case BoardDefinition.ClearConditionKind.RoundTotalAtLeast:
                return rules.RoundTotal >= _definition.targetRoundTotal;
            default:
                return false;
        }
    }
}

