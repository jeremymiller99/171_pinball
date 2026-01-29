using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dev/testing helper: when clicked during an active round, bank enough points to meet the current round goal.
/// Intended to be wired to a UI Button in a debug panel.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class InstantWinButton : MonoBehaviour
{
    [Header("Optional (auto-found if omitted)")]
    [SerializeField] private GameRulesManager gameRules;
    [SerializeField] private ScoreManager scoreManager;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        _button.onClick.AddListener(HandleClick);

        // Resolve once; additive scenes may create these later, so we'll also resolve on click.
        if (gameRules == null)
            gameRules = FindFirstObjectByType<GameRulesManager>();
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (gameRules == null)
            gameRules = FindFirstObjectByType<GameRulesManager>();
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

        if (scoreManager == null)
        {
            Debug.LogWarning($"{nameof(InstantWinButton)}: Could not find {nameof(ScoreManager)} in loaded scenes.");
            return;
        }

        // Prefer rules manager goal if present; otherwise fall back to ScoreManager's stored goal.
        float goal = gameRules != null ? gameRules.CurrentGoal : scoreManager.Goal;
        if (goal <= 0f)
        {
            Debug.LogWarning($"{nameof(InstantWinButton)}: No valid goal set (goal={goal}). Are you in a round?");
            return;
        }

        // If we bank right now, this is what the round total would become.
        float mult = scoreManager.mult;
        if (mult <= 0f) mult = 1f;

        float liveIfBankedNow = scoreManager.roundTotal + (scoreManager.points * mult);
        float extraNeeded = goal - liveIfBankedNow;
        if (extraNeeded <= 0f)
        {
            // Already at/over goal (or would be if banked). Do nothing.
            return;
        }

        // Add just enough points so that banking reaches the goal, then bank immediately into roundTotal.
        // We write directly to ScoreManager fields so tier-scaling doesn't distort the cheat amount.
        scoreManager.points += extraNeeded / mult;
        scoreManager.BankCurrentBallScore(bankMultiplier: 1f);
    }
}

