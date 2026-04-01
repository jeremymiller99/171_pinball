// Generated with Cursor (Composer) by assistant on 2026-03-31.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns all round-goal calculation: exponential scaling, legacy list fallback,
/// and modifier adjustments. Extracted from GameRulesManager for single responsibility.
/// </summary>
[DisallowMultipleComponent]
public class GoalScaler : MonoBehaviour
{
    public enum GoalScalingMode { LegacyList = 0, Exponential = 1 }

    [Header("Level Goal Scaling")]
    [SerializeField] private GoalScalingMode goalScalingMode = GoalScalingMode.Exponential;
    [Min(0f)] [SerializeField] private float baseGoal = 500f;
    [Min(1f)] [SerializeField] private float goalGrowthPerLevel = 1.35f;
    [Tooltip("0 means no rounding. Otherwise, rounds the computed exponential goal to nearest step.")]
    [Min(0f)] [SerializeField] private float goalRoundingStep = 100f;

    [Header("Legacy level goals (optional)")]
    [SerializeField] private List<float> goalByRound =
        new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };

    private void Awake()
    {
        ServiceLocator.Register<GoalScaler>(this);

        if (goalByRound == null || goalByRound.Count == 0)
        {
            goalByRound =
                new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };
        }
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<GoalScaler>();
    }

    public float GetGoal(
        int roundIndex,
        RoundModifierDefinition activeModifier,
        RoundModifierController modCtrl)
    {
        if (roundIndex < 0) roundIndex = 0;

        float goal = goalScalingMode == GoalScalingMode.Exponential
            ? ComputeExponentialGoal(roundIndex)
            : ComputeLegacyGoal(roundIndex);

        if (activeModifier != null)
        {
            float goalMod = activeModifier.applyTwoRandomDevilModifiers
                ? (modCtrl?.EffectiveGoalModifierForRound ?? 0f)
                : activeModifier.goalModifier;

            if (!Mathf.Approximately(goalMod, 0f))
            {
                goal = Mathf.Max(0f, goal + goalMod);
            }
        }

        return goal;
    }

    private float ComputeExponentialGoal(int index)
    {
        float goal = Mathf.Max(0f, baseGoal)
            * Mathf.Pow(Mathf.Max(1f, goalGrowthPerLevel), index);

        if (goalRoundingStep > 0f && !Mathf.Approximately(goalRoundingStep, 1f))
        {
            goal = Mathf.Round(goal / goalRoundingStep) * goalRoundingStep;
        }
        else if (Mathf.Approximately(goalRoundingStep, 1f))
        {
            goal = Mathf.Round(goal);
        }

        return Mathf.Max(0f, goal);
    }

    private float ComputeLegacyGoal(int index)
    {
        if (goalByRound == null || goalByRound.Count == 0) return 0f;
        if (index >= goalByRound.Count) return goalByRound[goalByRound.Count - 1];
        return goalByRound[index];
    }
}
