using UnityEngine;

/// <summary>
/// Defines a single round modifier (angel or devil buff/debuff).
/// Create via Create > Pinball > Round Modifier.
/// </summary>
[CreateAssetMenu(menuName = "Pinball/Round Modifier", fileName = "New Round Modifier")]
public class RoundModifierDefinition : ScriptableObject
{
    public enum ModifierType
    {
        Angel,
        Devil
    }

    [Header("Display")]
    [Tooltip("Name shown on the round card.")]
    public string displayName = "New Modifier";

    [Tooltip("Description of what this modifier does.")]
    [TextArea(2, 4)]
    public string description = "";

    [Tooltip("Icon displayed on the round card.")]
    public Sprite icon;

    [Tooltip("Whether this is an Angel (buff) or Devil (debuff) modifier.")]
    public ModifierType type = ModifierType.Angel;

    [Header("Score Effects")]
    [Tooltip("Multiplier applied to all points earned. 1.5 = +50%, 0.75 = -25%.")]
    [Min(0f)]
    public float scoreMultiplier = 1f;

    [Tooltip("If true, the ball's multiplier is locked at 1x for this round.")]
    public bool disableMultiplier = false;

    [Header("Goal Effects")]
    [Tooltip("Amount added to the round goal. Positive = harder, negative = easier.")]
    public float goalModifier = 0f;

    [Header("Coin Effects")]
    [Tooltip("Multiplier applied to coins earned at round end. 2 = double coins.")]
    [Min(0f)]
    public float coinMultiplier = 1f;

    [Header("Ball Effects")]
    [Tooltip("Number of balls added or removed for this round. Positive = more balls.")]
    public int ballModifier = 0;

    /// <summary>
    /// Returns a formatted string describing all active effects.
    /// </summary>
    public string GetEffectsSummary()
    {
        var sb = new System.Text.StringBuilder();

        if (!Mathf.Approximately(scoreMultiplier, 1f))
        {
            float percent = (scoreMultiplier - 1f) * 100f;
            string sign = percent >= 0 ? "+" : "";
            sb.AppendLine($"{sign}{percent:0}% Score");
        }

        if (disableMultiplier)
        {
            sb.AppendLine("Multiplier Disabled");
        }

        if (!Mathf.Approximately(goalModifier, 0f))
        {
            string sign = goalModifier >= 0 ? "+" : "";
            sb.AppendLine($"{sign}{goalModifier:0} Goal");
        }

        if (!Mathf.Approximately(coinMultiplier, 1f))
        {
            float percent = (coinMultiplier - 1f) * 100f;
            string sign = percent >= 0 ? "+" : "";
            sb.AppendLine($"{sign}{percent:0}% Coins");
        }

        if (ballModifier != 0)
        {
            string sign = ballModifier > 0 ? "+" : "";
            sb.AppendLine($"{sign}{ballModifier} Ball{(Mathf.Abs(ballModifier) != 1 ? "s" : "")}");
        }

        return sb.ToString().TrimEnd();
    }
}
