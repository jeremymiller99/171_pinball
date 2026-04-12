using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One per board scene. Acts as the single entry point for the gameplay core to bind to a board.
/// </summary>
public sealed class BoardRoot : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private Transform spawnPoint;

    [Header("Hand slots")]
    [Tooltip("Ordered slot GameObjects for the player's ball inventory. " +
             "Index 0 should be closest to the launch point. Each slot needs a BallHandSlot " +
             "+ BoxCollider (trigger) for shop drop targeting.")]
    [SerializeField] private List<BallHandSlot> handSlots = new List<BallHandSlot>();

    private BoardDefinition _definition;

    public Transform SpawnPoint => spawnPoint;
    public IReadOnlyList<BallHandSlot> HandSlots => handSlots;
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
