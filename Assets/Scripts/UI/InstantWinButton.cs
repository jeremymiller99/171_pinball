using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updated with Antigravity by jjmil on 2026-04-07 (removed bankMultiplier).
/// Generated with Cursor (GPT-5.3-codex) by OpenAI assistant for jjmil on 2026-02-26.
/// Dev/testing helper: when clicked during an active round, bank enough points to meet the current level goal.
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
            gameRules = ServiceLocator.Get<GameRulesManager>();
        if (scoreManager == null)
            scoreManager = ServiceLocator.Get<ScoreManager>();
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (gameRules == null)
            gameRules = ServiceLocator.Get<GameRulesManager>();
        if (scoreManager == null)
            scoreManager = ServiceLocator.Get<ScoreManager>();

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

        // AddRawPoints bypasses mult scaling, so we
        // divide by current mult to get the raw amount
        // that will become the correct scaled value.
        float currentMult = scoreManager.Mult;
        if (currentMult <= 0f) currentMult = 1f;

        float extraNeeded =
            goal - scoreManager.LiveLevelProgress;

        if (extraNeeded > 0f)
            scoreManager.AddRawPoints(
                extraNeeded / currentMult);

        scoreManager.BankCurrentBallScore();
    }
}

