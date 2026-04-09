using UnityEngine;

/// <summary>
/// Defines a single round modifier (angel or devil buff/debuff).
/// Create via Create > Pinball > Round Modifier.
/// </summary>
[CreateAssetMenu(menuName = "Pinball/Round Modifier", fileName = "New Round Modifier")]
public class RoundModifierDefinition : ScriptableObject
{
    [Header("Background")]
    [Tooltip("Unique background material for this modifier. If null, uses the default devil material.")]
    public Material backgroundMaterial;

    [Header("Display")]
    [Tooltip("Name shown on the round card.")]
    public string displayName = "New Modifier";

    [Tooltip("Description of what this modifier does.")]
    [TextArea(2, 4)]
    public string description = "";

    [Tooltip("Icon displayed on the round card.")]
    public Sprite icon;

    [Tooltip("If true, this round applies two random devil modifiers (from the devil pool) instead of this definition's own effects. Used for Unlucky Day.")]
    public bool applyTwoRandomDevilModifiers = false;

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

    [Header("Speed Effects")]
    [Tooltip("Multiplier for game/ball speed this round (Time.timeScale). 2 = twice as fast, 1 = normal.")]
    [Min(0.1f)]
    public float timeScaleMultiplier = 1f;

    [Header("Visibility (Special)")]
    [Tooltip("If true, the ball (and its trail) are hidden for part of each cycle this round.")]
    public bool cyclicHideBallEnabled = false;
    [Tooltip("Seconds the ball is visible before hiding. Only used when Cyclic Hide Ball is enabled.")]
    [Min(0.1f)]
    public float cyclicHideBallVisibleSeconds = 6f;
    [Tooltip("Seconds the ball (and trail) stay hidden. Only used when Cyclic Hide Ball is enabled.")]
    [Min(0.1f)]
    public float cyclicHideBallHiddenSeconds = 3f;

    /// <summary>
    /// Returns a formatted string describing all active effects.
    /// </summary>
    public string GetEffectsSummary()
    {
        var sb = new System.Text.StringBuilder();

        if (cyclicHideBallEnabled)
        {
            sb.AppendLine($"Ball hidden {cyclicHideBallHiddenSeconds:0}s every {cyclicHideBallVisibleSeconds + cyclicHideBallHiddenSeconds:0}s");
        }

        return sb.ToString().TrimEnd();
    }
}
